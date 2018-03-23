using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if __MonoCS__
using Mono.Data.Sqlite;
#else
using System.Data.SQLite;
#endif
using System.Data.Common;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using log4net;

using CmisSync.Lib.Utilities.PathConverter;
using CmisSync.Lib.Sync.SynchronizeItem;
using CmisSync.Lib.Sync.SyncTriplet;


namespace CmisSync.Lib.Database
{
#if __MonoCS__
    // Mono's SQLite ADO implementation uses pure CamelCase (Sqlite vs. SQLite)
    // so we define some aliases here
    using SQLiteConnection = SqliteConnection;
    using SQLiteCommand = SqliteCommand;
    using SQLiteException = SqliteException;
    using SQLiteDataReader = SqliteDataReader;
#endif

    /// <summary>
    /// Database to cache remote information from the CMIS server.
    /// Implemented with SQLite.
    /// </summary>
    public class Database : IDisposable
    {
        /// <summary>
        /// The current database schema version.
        /// </summary>
        public const int SchemaVersion = 3;

        /// <summary>
        /// Log.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Database));


        /// <summary>
        /// Name of the SQLite database file.
        /// </summary>
        private string databaseFileName;


        /// <summary>
        ///  SQLite connection to the underlying database.
        /// </summary>
        private SQLiteConnection sqliteConnection;


        /// <summary>
        /// Track whether <c>Dispose</c> has been called.
        /// </summary>
        private bool disposed = false;


        /// <summary>
        /// the prefix to remove before storing paths.
        /// </summary>
        private string localPathPrefix;


        /// <summary>
        /// Length of the prefix to remove before storing paths.
        /// </summary>
        private int localPathPrefixSize;

        /// <summary>
        /// The prefix to remove before storing remote paths.
        /// </summary>
        private string remotePathPrefix;

        /// <summary>
        /// Length of the prefix to remove before storing remote paths.
        /// </summary>
        private int remotePathPrefixSize;


        /// <summary>
        /// Constructor.
        /// dataPath: Local path to the database file
        /// localPathPrefix: Path to the synchronized files, ex: C:\Users\win7pro32bit\CmisSync\agency
        /// remotePathPrefix: Path on the remote server, ex: /Sites/swsdp/documentLibrary/Agency Files
        /// </summary>
        public Database(string databaseFileName, string localPathPrefix, string remotePathPrefix)
        {
            this.databaseFileName = databaseFileName;
            this.localPathPrefix = localPathPrefix;
            
            this.localPathPrefixSize = localPathPrefix.Length;
            if ( ! remotePathPrefix.Equals(Cmis.CmisUtils.CMIS_FILE_SEPARATOR))
            {
                this.localPathPrefixSize += 1;
            }

            this.remotePathPrefix = remotePathPrefix;
            this.remotePathPrefixSize = remotePathPrefix.Length;
            if (! remotePathPrefix.EndsWith(Cmis.CmisUtils.CMIS_FILE_SEPARATOR.ToString() )) {
                this.remotePathPrefixSize += 1;
            }
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~Database()
        {
            Dispose(false);
        }


        /// <summary>
        /// Implement IDisposable interface. 
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (this.sqliteConnection != null)
                    {
                        this.sqliteConnection.Dispose();
                    }
                }
                this.disposed = true;
            }
        }


        /// <summary>
        ///  Connection to the database.
        /// The sqliteConnection must not be used directly, used this method instead.
        /// </summary>
        public SQLiteConnection GetSQLiteConnection()
        {
            if (sqliteConnection == null || sqliteConnection.State == System.Data.ConnectionState.Broken)
            {
                try
                {
                    Logger.Info(String.Format("Checking whether database {0} exists", databaseFileName));
                    bool createDatabase = !File.Exists(databaseFileName);

                    sqliteConnection = new SQLiteConnection("Data Source=" + databaseFileName + ";PRAGMA journal_mode=WAL");
                    sqliteConnection.Open();

                    if (createDatabase)
                    {
                        string command =
                       @"CREATE TABLE IF NOT EXISTS files (
                            path TEXT PRIMARY KEY, /* Remote path of the folder, on the CMIS server side */
                            localPath TEXT, /* Local path, sometimes different due to local filesystem constraints */
                            id TEXT,
                            serverSideModificationDate DATE,
                            metadata TEXT,
                            checksum TEXT);   /* Checksum of both data and metadata */
                        CREATE INDEX IF NOT EXISTS files_localPath_index ON files (localPath);
                        CREATE INDEX IF NOT EXISTS files_id_index ON files (id);
                        CREATE TABLE IF NOT EXISTS folders (
                            path TEXT PRIMARY KEY, /* Remote path of the folder, on the CMIS server side */
                            localPath TEXT, /* Local path, sometimes different due to local filesystem constraints */
                            id TEXT,
                            serverSideModificationDate DATE,
                            metadata TEXT,
                            checksum TEXT);   /* Checksum of metadata */
                        CREATE INDEX IF NOT EXISTS folders_localPath_index ON folders (localPath);
                        CREATE INDEX IF NOT EXISTS folders_id_index ON folders (id);
                        CREATE TABLE IF NOT EXISTS general (
                            key TEXT PRIMARY KEY,
                            value TEXT);      /* Other data such as ChangeLog token */
                        CREATE TABLE IF NOT EXISTS downloads (
                            PATH TEXT PRIMARY KEY,
                            serverSideModificationDate DATE);     /* Download */
                        CREATE TABLE IF NOT EXISTS failedoperations (
                            path TEXT PRIMARY KEY,
                            lastLocalModificationDate DATE,
                            uploadCounter INTEGER,
                            downloadCounter INTEGER,
                            changeCounter INTEGER,
                            deleteCounter INTEGER,
                            uploadMessage TEXT,
                            downloadMessage TEXT,
                            changeMessage TEXT,
                            deleteMessage TEXT);     /* Failed Operations*/
                        DROP TABLE IF EXISTS faileduploads; /* Drop old upload Counter Table*/";

                        ExecuteSQLAction(command, null);
                        ExecuteSQLAction("PRAGMA user_version=" + SchemaVersion.ToString(), null);
                        Logger.Info("Database created");
                    }
                    else
                    {
                        DatabaseMigration.Migrate(databaseFileName);
                    }

                }
                catch (Exception e)
                {
                    Logger.Error("Error creating database: " + e.Message, e);
                    throw;
                }
            }
            return sqliteConnection;
        }


        /// <summary>
        /// RemoveLocalPrefix a path.
        /// All paths stored in database must be normalized.
        /// Goals:
        /// - Make data smaller in database
        /// - Reduce OS-specific differences
        /// </summary>
        private string RemoveLocalPrefix(string path)
        {
            if (path.StartsWith(localPathPrefix))
            {
                // Remove path prefix
                path = path.Substring(localPathPrefixSize, path.Length - localPathPrefixSize);
            }
            return path;
        }


        /// <summary>
        /// Denormalize a path from the normalized one to a local path.
        /// </summary>
        private string Denormalize(string path)
        {
            if (null == path)
            {
                return null;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }
            // Insert path prefix
            return Path.Combine(
                localPathPrefix,
                path.Replace('/', Path.DirectorySeparatorChar)
            );
        }

        /// <summary>
        /// Normalizes a remote path.
        /// All remote paths in database must be normalized.
        /// </summary>
        /// <returns>normalized remote path.</returns>
        /// <param name="path">remote path.</param>
        private string RemoveRemotePrefix(string path)
        {
            if (path.StartsWith(remotePathPrefix))
            {
                // Remove path prefix
                path = path.Substring(remotePathPrefixSize, path.Length - remotePathPrefixSize);
                // RemoveLocalPrefix all slashes to forward slash
                // path = path.Replace('\\', '/');
            }
            return path;
        }

        /// <summary>
        /// Denormalizes a remote path from the normalized one to a remote path.
        /// </summary>
        /// <returns>The remote path.</returns>
        /// <param name="path">normalized remote path</param>
        private string DenormalizeRemotePath(string path)
        {
            if (null == path)
            {
                return null;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.Combine(remotePathPrefix, path);
        }

        /// <summary>
        /// Calculate the SHA1 checksum of a file.
        /// Code from http://stackoverflow.com/a/1993919/226958
        /// </summary>
        public static string Checksum(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var bs = new BufferedStream(fs))
            {
                using (var sha1 = new SHA1Managed())
                {
                    byte[] hash = sha1.ComputeHash(bs);
                    return ChecksumToString(hash);
                }
            }
        }

        /// <summary>
        /// Calculate the SHA1 checksum of a syncitem.
        /// Code from http://stackoverflow.com/a/1993919/226958
        /// </summary>
        /// <param name="item">sync item</param>
        public static string Checksum(SyncItem item)
        {
            using (var fs = new FileStream(item.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var bs = new BufferedStream(fs))
            {
                using (var sha1 = new SHA1Managed())
                {
                    byte[] hash = sha1.ComputeHash(bs);
                    return ChecksumToString(hash);
                }
            }
        }



        /// <summary>
        /// Transforms a given hash into a string
        /// </summary>
        private static string ChecksumToString(byte[] hash)
        {
            if (hash == null || hash.Length == 0) return String.Empty;
            StringBuilder formatted = new StringBuilder(2 * hash.Length);
            foreach (byte b in hash)
            {
                formatted.AppendFormat("{0:X2}", b);
            }
            return formatted.ToString();
        }

        /// <summary>
        /// Put all the values of a dictionary into a JSON string.
        /// </summary>
        private string Json(Dictionary<string, string[]> dictionary)
        {
            return JsonConvert.SerializeObject(dictionary);
        }


        //
        //
        // 
        //
        // Database operations.
        // 
        // 
        // 
        //

        /// <summary>
        /// Begins a Database transaction
        /// </summary>
        public DbTransaction BeginTransaction()
        {
            return GetSQLiteConnection().BeginTransaction();
        }


        /// <summary>
        /// Add a file to the database.
        /// If checksum is not null, it will be used for the database entry
        /// </summary>
        public void AddFile(SyncItem item, string objectId, DateTime? serverSideModificationDate,
            Dictionary<string, string[]> metadata, byte[] filehash)
        {
            Logger.Debug("Starting database file addition for file: " + item.LocalPath);
            string checksum = ChecksumToString(filehash);
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate)
            {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime();
            }

            if (String.IsNullOrEmpty(checksum))
            {
                // Calculate file checksum.
                try
                {
                    checksum = Checksum(item);
                }
                catch (IOException e)
                {
                    Logger.Warn("IOException while calculating checksum of " + item.LocalPath
                        + " , The file was removed while reading. Just skip it, as it does not need to be added anymore. ", e);
                }
            }

            if (String.IsNullOrEmpty(checksum))
            {
                Logger.Warn("Bad checksum for " + item.LocalPath);
                return;
            }

            // Insert into database.
            string command =
                @"INSERT OR REPLACE INTO files (path, localPath, id, serverSideModificationDate, metadata, checksum)
                    VALUES (@path, @localPath, @id, @serverSideModificationDate, @metadata, @checksum)";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", item.RemoteRelativePath);
            parameters.Add("localPath", item.LocalRelativePath);
            parameters.Add("id", objectId);
            parameters.Add("serverSideModificationDate", serverSideModificationDate);
            parameters.Add("metadata", Json(metadata));
            parameters.Add("checksum", checksum);
            ExecuteSQLAction(command, parameters);
            Logger.Debug("Completed database file addition for file: " + item.LocalPath);
        }

        public void AddFile (string localRelativePath, string remoteRelativePath, string checksum, string objectId, DateTime? serverSideModificationDate,
                             Dictionary<string, string []> metadata, byte [] filehash)
        {

            Logger.Debug ("Starting database file addition for file: " + localRelativePath);

            /*
            string checksum = null;
            try {
                checksum = triplet.LocalStorage.CheckSum;
            } catch (IOException e) {
                Logger.Warn ("IOException while calculating checksum of " + localRelativePath
                    + " , The file was removed while reading. Just skip it, as it does not need to be added anymore. ", e);
                return;
            }*/

            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate) {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime ();
            }


            // Insert into database.
            string command =
                @"INSERT OR REPLACE INTO files (path, localPath, id, serverSideModificationDate, metadata, checksum)
                    VALUES (@path, @localPath, @id, @serverSideModificationDate, @metadata, @checksum)";
            Dictionary<string, object> parameters = new Dictionary<string, object> ();
            parameters.Add ("path", remoteRelativePath);
            parameters.Add ("localPath", localRelativePath);
            parameters.Add ("id", objectId);
            parameters.Add ("serverSideModificationDate", serverSideModificationDate);
            parameters.Add ("metadata", Json (metadata));
            parameters.Add ("checksum", checksum);
            ExecuteSQLAction (command, parameters);
            Logger.Debug ("Completed database file addition for file: " + localRelativePath);
        }

        /// <summary>
        /// Add a folder to the database.
        /// </summary>
        public void AddFolder(SyncItem item, string objectId, DateTime? serverSideModificationDate)
        {
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate)
            {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime();
            }

            string command =
                @"INSERT OR REPLACE INTO folders (path, localPath, id, serverSideModificationDate)
                    VALUES (@path, @localPath, @id, @serverSideModificationDate)";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", item.RemoteRelativePath);
            parameters.Add("localPath", item.LocalRelativePath);
            parameters.Add("id", objectId);
            parameters.Add("serverSideModificationDate", serverSideModificationDate);
            ExecuteSQLAction(command, parameters);
        }
            

        public void AddFolder (string remoteRelativePath, string localRelativePath, string objectId, DateTime? serverSideModificationDate)
        {
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate) {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime ();
            }

            string command =
                @"INSERT OR REPLACE INTO folders (path, localPath, id, serverSideModificationDate)
                    VALUES (@path, @localPath, @id, @serverSideModificationDate)";
            Dictionary<string, object> parameters = new Dictionary<string, object> ();
            parameters.Add ("path", remoteRelativePath);
            parameters.Add ("localPath", localRelativePath);
            parameters.Add ("id", objectId);
            parameters.Add ("serverSideModificationDate", serverSideModificationDate);
            ExecuteSQLAction (command, parameters);
        }

        /// <summary>
        /// Remove a file from the database.
        /// </summary>
        public void RemoveFile(SyncItem item)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", item.RemoteRelativePath);
            ExecuteSQLAction("DELETE FROM files WHERE path=@path", parameters);

            parameters = new Dictionary<string, object>();
            parameters.Add("path", item.RemoteRelativePath);
            ExecuteSQLAction("DELETE FROM downloads WHERE path=@path", parameters);
        }

        /*
         * when to remove file, local or remote might not exist
         * use DBStorage 
         */
        public void RemoveFile (SyncTriplet triplet)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object> ();
            parameters.Add ("path", triplet.DBStorage.DBRemotePath);
            ExecuteSQLAction ("DELETE FROM files WHERE path=@path", parameters);

            parameters = new Dictionary<string, object> ();
            parameters.Add ("path", triplet.DBStorage.DBRemotePath);
            ExecuteSQLAction ("DELETE FROM downloads WHERE path=@path", parameters);
        }


        /// <summary>
        /// Remove a folder from the database.
        /// </summary>
        public void RemoveFolder(SyncItem item)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            // Remove folder itself
            parameters.Add("path", item.RemoteRelativePath);
            ExecuteSQLAction("DELETE FROM folders WHERE path=@path", parameters);

            // Remove all folders under this folder
            parameters.Clear();
            parameters.Add("path", item.RemoteRelativePath + "/%");
            ExecuteSQLAction("DELETE FROM folders WHERE path LIKE @path", parameters);

            // Remove all files under this folder
            parameters.Clear();
            parameters.Add("path", item.RemoteRelativePath + "/%");
            ExecuteSQLAction("DELETE FROM files WHERE path LIKE @path", parameters);
        }
        
        public void RemoveFolder(SyncTriplet triplet)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            // Remove folder itself
            parameters.Add("path", triplet.DBStorage.DBRemotePath);
            ExecuteSQLAction("DELETE FROM folders WHERE path=@path", parameters);

            // Remove all folders under this folder
            parameters.Clear();
            parameters.Add("path", triplet.DBStorage.DBRemotePath + "/%");
            ExecuteSQLAction("DELETE FROM folders WHERE path LIKE @path", parameters);

            // Remove all files under this folder
            parameters.Clear();
            parameters.Add("path", triplet.DBStorage.DBRemotePath + "/%");
            ExecuteSQLAction("DELETE FROM files WHERE path LIKE @path", parameters);
        }
         
        /// <summary>
        /// Move a file.
        /// </summary>
        public void MoveFile(SyncItem oldItem, SyncItem newItem)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("oldPath", oldItem.RemoteRelativePath);
            parameters.Add("newPath", newItem.RemoteRelativePath);
            parameters.Add("newLocalPath", newItem.LocalRelativePath);
            ExecuteSQLAction("UPDATE files SET path=@newPath, localPath=@newLocalPath WHERE path=@oldPath", parameters);
        }


        /// <summary>
        /// Move a folder.
        /// </summary>
        public void MoveFolder(SyncItem oldItem, SyncItem newItem)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("oldPath", oldItem.RemoteRelativePath);
            parameters.Add("oldPathLike", oldItem.RemoteRelativePath + "/%");
            parameters.Add("substringIndex", oldItem.RemoteRelativePath.Length + 1);
            parameters.Add("newPath", newItem.RemoteRelativePath);
            parameters.Add("newLocalPath", newItem.LocalRelativePath);

            // Update folder itself
            ExecuteSQLAction("UPDATE folders SET path=@newPath, localPath=@newLocalPath WHERE path=@oldPath", parameters);

            // UPdate all folders under this folder
            ExecuteSQLAction("UPDATE folders SET path=@newPath||SUBSTR(path, @substringIndex), localPath=@newLocalPath||SUBSTR(localPath, @substringIndex) WHERE path LIKE @oldPathLike", parameters);

            // Update all files under this folder
            ExecuteSQLAction("UPDATE files SET path=@newPath||SUBSTR(path, @substringIndex), localPath=@newLocalPath||SUBSTR(localPath, @substringIndex) WHERE path LIKE @oldPathLike", parameters);
        }


        /// <summary>
        /// Get the time at which the file was last modified.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// </summary>
        public DateTime? GetServerSideModificationDate(SyncItem item)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", item.RemoteRelativePath);
            object modifyDateObj = null;

            if (item.IsFolder)
            {
                modifyDateObj = ExecuteSQLFunction("SELECT serverSideModificationDate FROM folders WHERE path=@path", parameters);
            }
            else
            {
                modifyDateObj = ExecuteSQLFunction("SELECT serverSideModificationDate FROM files WHERE path=@path", parameters);
            }

            if (null != modifyDateObj)
            {
                #if __MonoCS__
                modifyDateObj = DateTime.SpecifyKind((DateTime)modifyDateObj, DateTimeKind.Utc);
                #else
                modifyDateObj = ((DateTime)modifyDateObj).ToUniversalTime();
                #endif
            }
            return (DateTime?)modifyDateObj;
        }
            
        public DateTime? GetServerSideModificationDate (string remoteRelativePath, bool isFolder)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object> ();
            parameters.Add ("path", remoteRelativePath);
            object modifyDateObj = null;

            if (isFolder) {
                modifyDateObj = ExecuteSQLFunction ("SELECT serverSideModificationDate FROM folders WHERE path=@path", parameters);
            } else {
                modifyDateObj = ExecuteSQLFunction ("SELECT serverSideModificationDate FROM files WHERE path=@path", parameters);
            }

            if (null != modifyDateObj) {
#if __MonoCS__
                modifyDateObj = DateTime.SpecifyKind((DateTime)modifyDateObj, DateTimeKind.Utc);
#else
                modifyDateObj = ((DateTime)modifyDateObj).ToUniversalTime ();
#endif
            }
            return (DateTime?)modifyDateObj;
        }


        /// <summary>
        /// Set the last modification date of a file.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// 
        /// TODO Combine this method and the next in a new method ModifyFile, and find out if GetServerSideModificationDate is really needed.
        /// </summary>
        public void SetFileServerSideModificationDate(SyncItem item, DateTime? serverSideModificationDate)
        {
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones.
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate)
            {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime();
            }
                
            string command = @"UPDATE files
                    SET serverSideModificationDate=@serverSideModificationDate
                    WHERE path=@path";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("serverSideModificationDate", serverSideModificationDate);
            parameters.Add("path", item.RemoteRelativePath);
            ExecuteSQLAction(command, parameters);
        }

        public void SetFileServerSideModificationDate (SyncTriplet triplet, DateTime? serverSideModificationDate)
        {
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones.
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate) {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime ();
            }

            string command = @"UPDATE files
                    SET serverSideModificationDate=@serverSideModificationDate
                    WHERE path=@path";
            Dictionary<string, object> parameters = new Dictionary<string, object> ();
            parameters.Add ("serverSideModificationDate", serverSideModificationDate);
            parameters.Add ("path", triplet.RemoteStorage.RelativePath);
            ExecuteSQLAction (command, parameters);
        }



        /// <summary>
        /// Get the date at which the file was last download.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// </summary>
        public DateTime? GetDownloadServerSideModificationDate(SyncItem item)
        {

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", item.RemoteRelativePath);
            object obj = ExecuteSQLFunction("SELECT serverSideModificationDate FROM downloads WHERE path=@path", parameters);
            if (null != obj)
            {
                #if __MonoCS__
                obj = DateTime.SpecifyKind((DateTime)obj, DateTimeKind.Utc);
                #else
                obj = ((DateTime)obj).ToUniversalTime();
                #endif
            }
            return (DateTime?)obj;
        }

        public DateTime? GetDownloadServerSideModificationDate (string remoteRelativePath)
        {

            Dictionary<string, object> parameters = new Dictionary<string, object> ();
            parameters.Add ("path", remoteRelativePath);
            object obj = ExecuteSQLFunction ("SELECT serverSideModificationDate FROM downloads WHERE path=@path", parameters);
            if (null != obj) {
                #if __MonoCS__
                obj = DateTime.SpecifyKind((DateTime)obj, DateTimeKind.Utc);
                #else
                obj = ((DateTime)obj).ToUniversalTime ();
                #endif
            }
            return (DateTime?)obj;
        }

        /// <summary>
        /// Set the last download date of a file.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// </summary>
        public void SetDownloadServerSideModificationDate(SyncItem item, DateTime? serverSideModificationDate)
        {
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate)
            {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime();
            }

            string command = @"INSERT OR REPLACE INTO downloads (path, serverSideModificationDate)
                    VALUES (@path, @serverSideModificationDate)";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("serverSideModificationDate", serverSideModificationDate);
            parameters.Add("path", item.RemoteRelativePath);
            ExecuteSQLAction(command, parameters);
        }

        /// <summary>
        /// Gets the upload retry counter.
        /// </summary>
        /// <param name="path">Path of the local file.</param>
        /// <param name="type"></param>
        /// <returns>The upload retry counter.</returns>
        public long GetOperationRetryCounter(string path, OperationType type)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", RemoveLocalPrefix(path));
            object result = null;
            switch (type)
            {
                case OperationType.DOWNLOAD:
                    goto case OperationType.DELETE;
                case OperationType.DELETE:
                    result = ExecuteSQLFunction(String.Format("SELECT {0}Counter FROM failedoperations WHERE path=@path", operationTypeToString(type)), parameters);
                    break;
                default:
                    parameters.Add("date", File.GetLastWriteTimeUtc(path));
                    result = ExecuteSQLFunction(String.Format("SELECT {0}Counter FROM failedoperations WHERE path=@path AND lastLocalModificationDate=@date", operationTypeToString(type)), parameters);
                    break;
            }
            if (result != null && !(result is DBNull))
            { return (long)result; }
            else
            { return 0; }
        }

        /// <summary>
        /// Gets the upload retry counter.
        /// </summary>
        /// <param name="item">Path of the local file.</param>
        /// <param name="type"></param>
        /// <returns>The upload retry counter.</returns>
        public long GetOperationRetryCounter(SyncItem item, OperationType type)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", item.RemoteRelativePath);
            object result = null;
            switch (type)
            {
                case OperationType.DOWNLOAD:
                    goto case OperationType.DELETE;
                case OperationType.DELETE:
                    result = ExecuteSQLFunction(String.Format("SELECT {0}Counter FROM failedoperations WHERE path=@path", operationTypeToString(type)), parameters);
                    break;
                default:
                    parameters.Add("date", File.GetLastWriteTimeUtc(item.LocalPath));
                    result = ExecuteSQLFunction(String.Format("SELECT {0}Counter FROM failedoperations WHERE path=@path AND lastLocalModificationDate=@date", operationTypeToString(type)), parameters);
                    break;
            }
            if (result != null && !(result is DBNull))
            { return (long)result; }
            else
            { return 0; }
        }

        /// <summary>
        /// Sets the upload retry counter.
        /// </summary>
        /// <param name="item">Path of the local file.</param>
        /// <param name="counter">Counter.</param>
        /// <param name="type"></param>
        public void SetOperationRetryCounter(SyncItem item, long counter, OperationType type)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            switch (type)
            {
                case OperationType.DOWNLOAD:
                    goto case OperationType.DELETE;
                case OperationType.DELETE:
                    parameters.Add("date", DateTime.Now.ToFileTimeUtc());
                    break;
                default:
                    parameters.Add("date", File.GetLastWriteTimeUtc(item.LocalPath));
                    break;
            }
            parameters.Add("path", item.RemoteRelativePath);
            parameters.Add("counter", (counter >= 0) ? counter : 0);
            string uploadCounter = "(SELECT CASE WHEN lastLocalModificationDate=@date THEN uploadCounter ELSE '' END FROM failedoperations WHERE path=@path)";
            string downloadCounter = "(SELECT CASE WHEN lastLocalModificationDate=@date THEN downloadCounter ELSE '' END FROM failedoperations WHERE path=@path)";
            string changeCounter = "(SELECT CASE WHEN lastLocalModificationDate=@date THEN changeCounter ELSE '' END FROM failedoperations WHERE path=@path)";
            string deleteCounter = "(SELECT CASE WHEN lastLocalModificationDate=@date THEN deleteCounter ELSE '' END FROM failedoperations WHERE path=@path)";
            switch (type)
            {
                case OperationType.UPLOAD:
                    uploadCounter = "@counter";
                    break;
                case OperationType.DOWNLOAD:
                    downloadCounter = "@counter";
                    break;
                case OperationType.CHANGE:
                    changeCounter = "@counter";
                    break;
                case OperationType.DELETE:
                    deleteCounter = "@counter";
                    break;
            }
            string command = String.Format(@"INSERT OR REPLACE INTO failedoperations (path, lastLocalModificationDate,
                                uploadCounter, downloadCounter, changeCounter, deleteCounter,
                                uploadMessage, downloadMessage, changeMessage, deleteCounter)
                                VALUES( @path, @date, {0},{1},{2},{3},
                                (SELECT CASE WHEN lastLocalModificationDate=@date THEN uploadMessage ELSE '' END FROM failedoperations WHERE path=@path ), 
                                (SELECT CASE WHEN lastLocalModificationDate=@date THEN downloadMessage ELSE '' END FROM failedoperations WHERE path=@path),
                                (SELECT CASE WHEN lastLocalModificationDate=@date THEN changeMessage ELSE '' END FROM failedoperations WHERE path=@path),
                                (SELECT CASE WHEN lastLocalModificationDate=@date THEN deleteMessage ELSE '' END FROM failedoperations WHERE path=@path)
                            )", uploadCounter, downloadCounter, changeCounter, deleteCounter);
            ExecuteSQLAction(command, parameters);
        }

        /// <summary>
        /// Deletes the upload retry counter.
        /// </summary>
        /// <param name="item">Path of the local file.</param>
        public void DeleteAllFailedOperations(SyncItem item)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", item.RemoteRelativePath);
            string command = @"DELETE FROM failedoperations WHERE path=@path";
            ExecuteSQLAction(command, parameters);
        }

        /// <summary>
        /// Deletes all failed upload counter.
        /// </summary>
        public void DeleteAllFailedOperations()
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            string command = @"DELETE FROM failedoperations";
            ExecuteSQLAction(command, parameters);
        }

        /// <summary>
        /// Recalculate the checksum of a file and save it to database.
        /// </summary>
        /// <param name="syncItem"></param>
        public void RecalculateChecksum(SyncItem syncItem)
        {
            string checksum;
            try
            {
                checksum = Checksum(syncItem.LocalPath);
            }
            catch (IOException)
            {
                Logger.Error("IOException while reading file checksum: " + syncItem.LocalPath);
                return;
            }

            string localPath = RemoveLocalPrefix(syncItem.LocalPath); // TODO use relative localpath method of SyncItem

            string command = @"UPDATE files
                    SET checksum=@checksum
                    WHERE localPath=@localPath";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("checksum", checksum);
            parameters.Add("localPath", localPath);
            ExecuteSQLAction(command, parameters);
        }

        public void SetChecksum (SyncTriplet triplet)
        {
            string command = @"UPDATE files
                    SET checksum=@checksum
                    WHERE localPath=@localPath";
            Dictionary<string, object> parameters = new Dictionary<string, object> ();
            parameters.Add ("checksum", triplet.LocalStorage.CheckSum);
            parameters.Add ("localPath", triplet.LocalStorage.RelativePath);
            ExecuteSQLAction (command, parameters);
        }

        /// <summary>
        /// Checks whether the database contains a given item.
        /// Local filename is often different from remote document name.
        /// </summary>
        public bool ContainsLocalFile(string localPath)
        {
            string normalizedLocalPath = RemoveLocalPrefix(localPath);
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("localPath", normalizedLocalPath);
            return null != ExecuteSQLFunction("SELECT serverSideModificationDate FROM files WHERE localPath=@localPath", parameters);
        }

        /// <summary>
        /// Checks whether the database contains a given item.
        /// </summary>
        public bool ContainsRemoteFile(string remoteRelativePath)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", remoteRelativePath);
            return null != ExecuteSQLFunction("SELECT serverSideModificationDate FROM files WHERE path=@path", parameters);
        }

        /// <summary>
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Path field in files table for <paramref name="id"/></returns>
        public string GetRemoteFilePath(string id)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("id", id);
            return Denormalize((string)ExecuteSQLFunction("SELECT path FROM files WHERE id=@id", parameters));
        }

        /// <summary>
        /// Return the remote path of an item identified by its local path.
        /// If no such item exist yet in database (ex: new local file), returns the remote path it should be written to.
        /// </summary>
        public string RemoteToLocal(string remotePath, bool isFolder)
        {
            string normalizedRemotePath = RemoveLocalPrefix(remotePath);
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("remotePath", normalizedRemotePath);
            string table = isFolder ? "folders" : "files";
            string localPath = (string)ExecuteSQLFunction("SELECT localPath FROM " + table + " WHERE path=@remotePath", parameters);
            if (string.IsNullOrEmpty(localPath))
            {
                return PathRepresentationConverterUtil.RemoteToLocal(remotePath);
            }
            return localPath;
        }

        /// <summary>
        /// Remote to local nullable.
        /// </summary>
        /// <returns>The remote to local.</returns>
        /// <param name="remotePath">Remote path.</param>
        /// <param name="isFolder">If set to <c>true</c> is folder.</param>
        public string NullableRemoteToLocal (string remotePath, bool isFolder)
        {
            string normalizedRemotePath = RemoveLocalPrefix (remotePath);
            Dictionary<string, object> parameters = new Dictionary<string, object> ();
            parameters.Add ("remotePath", normalizedRemotePath);
            string table = isFolder ? "folders" : "files";
            string localPath = (string)ExecuteSQLFunction ("SELECT localPath FROM " + table + " WHERE path=@remotePath", parameters);
            if (string.IsNullOrEmpty (localPath)) {
                return null;
            }
            return localPath;
        }

        /// <summary>
        /// Return the local path of an item identified by its remote path.
        /// If no such item exist yet in database (ex: new remote file), returns the local path it should be written to.
        /// </summary>
        public string LocalToRemote(string localPath, bool isFolder)
        {
            string normalizedLocalPath = RemoveLocalPrefix(localPath);
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("localPath", normalizedLocalPath);
            string table = isFolder ? "folders" : "files";
            string remotePath = (string)ExecuteSQLFunction("SELECT path FROM " + table + " WHERE localPath=@localPath", parameters);
            if (string.IsNullOrEmpty(remotePath))
            {
                return PathRepresentationConverterUtil.LocalToRemote(localPath);
            }
            return remotePath;
        }

        /// <summary>
        /// Locals to remote nullable.
        /// </summary>
        /// <returns>The to remote.</returns>
        /// <param name="localPath">Local path.</param>
        /// <param name="isFolder">If set to <c>true</c> is folder.</param>
        public string NullableLocalToRemote (string localPath, bool isFolder)
        {
            string normalizedLocalPath = RemoveLocalPrefix (localPath);
            Dictionary<string, object> parameters = new Dictionary<string, object> ();
            parameters.Add ("localPath", normalizedLocalPath);
            string table = isFolder ? "folders" : "files";
            string remotePath = (string)ExecuteSQLFunction ("SELECT path FROM " + table + " WHERE localPath=@localPath", parameters);
            if (string.IsNullOrEmpty (remotePath)) {
                return null;
            }
            return remotePath;
        }

        /// <summary>
        /// Gets the syncitem from id.
        /// </summary>
        /// <returns>syncitem.</returns>
        /// <param name="id">Identifier.</param>
        public SyncItem GetSyncItem(string id)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("id", id);
            var result = ExecuteOneRecordSQL("SELECT path, localPath FROM files WHERE id=@id", parameters);
            if (result.Count() > 0) {
            string remotePath = (string)result["path"];
            object localPathObj = result["localPath"];
            string localPath = (localPathObj is DBNull) ? remotePath : (string)localPathObj;
            return SyncItemFactory.CreateFromPaths(localPathPrefix, localPath, remotePathPrefix, remotePath, false);
            } else {
                var items = GetAllFoldersWithCmisId(id);

                var item = items.FirstOrDefault();
                return item == null ? null : item;
            }
        }

        /// <summary>
        /// Gets the path from id
        /// string[0] = local
        /// string[1] = remote
        /// </summary>
        /// <returns>The path by identifier.</returns>
        /// <param name="id">Identifier.</param>
        public string[] GetPathById(string id) 
        {
            Dictionary<string, object> parameters = new Dictionary<string, object> ();
            parameters.Add ("id", id);
            var result = ExecuteOneRecordSQL ("SELECT path, localPath FROM files WHERE id=@id", parameters);
            if (result.Count () > 0) {
                string [] res = new string [2];
                res [1] = (string)result ["path"];
                object localPathObj = result ["localPath"];
                res [0] = (localPathObj is DBNull) ? null : (string)localPathObj;
                return res;
            } else {
                var results = ExecuteMultiRecordSQL ("SELECT path , localPath FROM folders WHERE id=@id ORDER BY serverSideModificationDate DESC", parameters);

                return results.Select (p => {
                    var localPath = p ["localPath"] as string;
                    var remotePath = p ["path"] as string;
                    return new string [2] { localPath, remotePath };
                }).ToList ().FirstOrDefault ();
            }
        }

        /// <summary>
        /// Gets the syncitem from local path.
        /// </summary>
        /// <returns>syncitem. If the item is not included in the database, return null.</returns>
        /// <param name="localPath">Local path.</param>
        public SyncItem GetSyncItemFromLocalPath(string localPath)
        {
            string normalizedLocalPath = RemoveLocalPrefix(localPath);
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("localPath", normalizedLocalPath);

            // Try to find it in files database.
            string path = (string)ExecuteSQLFunction("SELECT path FROM files WHERE localPath=@localPath", parameters);
            if (string.IsNullOrEmpty(path))
            {
                // Try to find it in folders database.
                path = (string)ExecuteSQLFunction("SELECT path FROM folders WHERE localPath=@localPath", parameters);
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }
            }

            return SyncItemFactory.CreateFromPaths(localPathPrefix, normalizedLocalPath, remotePathPrefix, path, false);
        }

        /// <summary>
        /// Gets the syncitem from remote path.
        /// </summary>
        /// <returns>syncitem. If the item is not included in the database, return null.</returns>
        /// <param name="remotePath">Remote path.</param>
        public SyncItem GetSyncItemFromRemotePath(string remotePath)
        {
            //Logger.Debug("GetSyncItemFromRemotePath " + remotePath);
            if (remotePath.Equals(remotePathPrefix))
            {
                return SyncItemFactory.CreateFromPaths(localPathPrefix, localPathPrefix, remotePathPrefix, remotePathPrefix, false);
            }

            string normalizedRemotePath = RemoveRemotePrefix(remotePath);
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", normalizedRemotePath);
            string localPath = (string)ExecuteSQLFunction("SELECT localPath FROM files WHERE path=@path", parameters);
            if (string.IsNullOrEmpty(localPath))
            {
                return null;
            }

            return SyncItemFactory.CreateFromPaths(localPathPrefix, localPath, remotePathPrefix, normalizedRemotePath, false);
        }

        /// <summary>
        /// Gets the syncitem from local path.
        /// </summary>
        /// <returns>syncitem. If the item is not included in the database, return null.</returns>
        /// <param name="localPath">Local path.</param>
        public SyncItem GetFolderSyncItemFromLocalPath(string localPath)
        {
            string normalizedLocalPath = RemoveLocalPrefix(localPath);
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("localPath", normalizedLocalPath);
            string path = (string)ExecuteSQLFunction("SELECT path FROM folders WHERE localPath=@localPath", parameters);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return SyncItemFactory.CreateFromPaths(localPathPrefix, normalizedLocalPath, remotePathPrefix, path, true);
        }

        /// <summary>
        /// Gets the syncitem from remote path.
        /// </summary>
        /// <returns>syncitem. If the item is not included in the database, return null.</returns>
        /// <param name="remotePath">Remote path.</param>
        public SyncItem GetFolderSyncItemFromRemotePath(string remotePath)
        {
            if (remotePath.Equals(remotePathPrefix))
            {
                return SyncItemFactory.CreateFromPaths(localPathPrefix, localPathPrefix, remotePathPrefix, remotePathPrefix, true);
            }

            string normalizedRemotePath = RemoveRemotePrefix(remotePath);
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", normalizedRemotePath);
            string localPath = (string)ExecuteSQLFunction("SELECT localPath FROM folders WHERE path=@path", parameters);
            if (string.IsNullOrEmpty(localPath))
            {
                return null;
            }

            return SyncItemFactory.CreateFromPaths(localPathPrefix, localPath, remotePathPrefix, normalizedRemotePath, true);
        }

        /// <summary>
        /// Checks whether the database contains a given folder.
        /// </summary>
        public bool ContainsFolder(string path)
        {
            string localPath = RemoveLocalPrefix(path);

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("localPath", localPath);
            return null != ExecuteSQLFunction("SELECT serverSideModificationDate FROM folders WHERE localPath=@localPath", parameters);
        }

        /// <summary>
        /// Checks whether the database contains a given folder item.
        /// </summary>
        public bool ContainsFolder(SyncItem item)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            if (item is RemotePathSyncItem)
            {
                parameters.Add("path", item.RemoteRelativePath);
                return null != ExecuteSQLFunction("SELECT serverSideModificationDate FROM folders WHERE path=@path", parameters);
            }
            else
            {
                parameters.Add("localPath", item.LocalRelativePath);
                return null != ExecuteSQLFunction("SELECT serverSideModificationDate FROM folders WHERE localPath=@localPath", parameters);
            }
        }

        /// <summary>
        /// <returns>path field in files table for <paramref name="id"/></returns>
        /// </summary>
        public string GetFilePath(string id)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("id", id);
            return Denormalize((string)ExecuteSQLFunction("SELECT path FROM files WHERE id=@id", parameters));
        }

        /// <summary>
        /// <returns>path field in folders table for <paramref name="id"/></returns>
        /// </summary>
        public string GetFolderRemotePath(string id)
        {
            var items = GetAllFoldersWithCmisId(id);

            var item = items.FirstOrDefault();
            return item == null ? null : Denormalize(item.RemotePath);
        }

        public List<SyncItem> GetAllFoldersWithCmisId(string id)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("id", id);

            var results = ExecuteMultiRecordSQL("SELECT path , localPath FROM folders WHERE id=@id ORDER BY serverSideModificationDate DESC", parameters);

            return results.Select(p =>
            {
                var localPath = p["localPath"] as string;
                var remotePath = p["path"] as string;
                return SyncItemFactory.CreateFromPaths(localPathPrefix, localPath, remotePathPrefix, remotePath, true);

            }).ToList();
        }

        

        /// <summary>
        /// Check whether a file's content has changed locally since it was last synchronized.
        /// This happens when the user edits a file on the local computer.
        /// This method does not communicate with the CMIS server, it just checks whether the checksum has changed.
        /// </summary>
        public bool LocalFileHasChanged(string path)
        {
            // Calculate current checksum.
            string currentChecksum = null;
            try
            {
                currentChecksum = Checksum(path);
            }
            catch (IOException)
            {
                Logger.Warn("IOException while reading file checksum: " + path
                    + " File is probably being edited right now, so skip it. See https://github.com/aegif/CmisSync/issues/245");
                return false;
            }

            // Read previous checksum from database.
            string previousChecksum = GetChecksum(path);

            // Compare checksums.
            if (!currentChecksum.Equals(previousChecksum))
                Logger.Info("Checksum of " + path + " has changed from " + previousChecksum + " to " + currentChecksum);
            return !currentChecksum.Equals(previousChecksum);
        }

        /// <summary>
        /// Get checksum from database.
        /// Public for debugging purposes only.
        /// </summary>
        /// <returns></returns>
        public string GetChecksum(string path)
        {
            string localRelativePath = RemoveLocalPrefix(path);
            int length = localRelativePath.Length;

			string command = "SELECT checksum FROM files WHERE localPath=@localPath";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
			parameters.Add("localPath", localRelativePath);
            string res = (string)ExecuteSQLFunction(command, parameters);
            if(string.IsNullOrEmpty(res))
            {
                Logger.Debug("GetCheckSum returns null for path=" + path + ", localRelativePath=" + localRelativePath);
            }
            return res;
        }

        /// <summary>
        /// Get the ChangeLog token that was stored at the end of the last successful CmisSync synchronization.
        /// If no ChangeLog has ever been stored, return null.
        /// </summary>
        public string GetChangeLogToken()
        {
            var token = ExecuteSQLFunction("SELECT value FROM general WHERE key=\"ChangeLogToken\"", null);

            if (token is DBNull)
            {
                return null;
            }
            else
            {
                return (string)token;
            }
        }

        /// <summary>
        /// Set the stored ChangeLog token.
        /// This should be called after each successful CmisSync synchronization.
        /// </summary>
        public void SetChangeLogToken(string token)
        {
            string command = "INSERT OR REPLACE INTO general (key, value) VALUES (\"ChangeLogToken\", @token)";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("token", token);
            ExecuteSQLAction(command, parameters);
            Logger.Info("Database ChangeLog token set to: " + token);
        }

        const string RemotePathPrefixKey = "RemotePathPrefix";
        /// <summary>
        /// Gets the remote path prefix.
        /// </summary>
        /// <returns>
        /// The path prefix.
        /// </returns>
        private string GetRemotePathPrefix()
        {
            object result = GetGeneralTableValue(RemotePathPrefixKey);
            if (result == null)
            {
                var syncFolder = Config.ConfigManager.CurrentConfig.Folders.Find((f) => f.GetRepoInfo().CmisDatabase == this.databaseFileName);
                string oldprefix = syncFolder.RemotePath;
                SetRemotePathPrefix(oldprefix);
                return oldprefix;
            }
            else
            {
                return (string)result;
            }
        }

        /// <summary>
        /// Sets the reomte path prefix.
        /// </summary>
        /// <param name='pathprefix'>
        /// Pathprefix.
        /// </param>
        private void SetRemotePathPrefix(string pathprefix)
        {
            SetGeneralTableValue(RemotePathPrefixKey, pathprefix);
            this.remotePathPrefix = pathprefix;
            this.remotePathPrefixSize = pathprefix.Length + 1;
        }

        private object GetGeneralTableValue(string key)
        {
            string command = "SELECT value FROM general WHERE key=@key";
            var parameters = new Dictionary<string, object>();
            parameters.Add("key", key);
            object result = ExecuteSQLFunction(command, parameters);
            return result;
        }

        private void SetGeneralTableValue(string key, string value)
        {
            string command = "INSERT OR REPLACE INTO general (key, value) VALUES(@key, @value)";
            var parameters = new Dictionary<string, object>();
            parameters.Add("key", key);
            parameters.Add("value", value);
            ExecuteSQLAction(command, parameters);
        }

        /// <summary>
        /// Checks whether the database contains a given folders's id.
        /// </summary>
        /// <returns><c>true</c>, if folder identifier was containsed, <c>false</c> otherwise.</returns>
        /// <param name="path">Path.</param>
        public bool ContainsLocalPath(string localPath)
        {
            localPath = RemoveLocalPrefix(localPath);
            var parameters = new Dictionary<string, object>();
            parameters.Add("@localPath", localPath);
            return null != ExecuteSQLFunction("SELECT id FROM folders WHERE localPath = @localPath;", parameters);
        }


        public List<string> GetLocalFolders()
        {
            var folders = new List<string>();
            SQLiteCommand command = new SQLiteCommand("SELECT localPath FROM folders;", GetSQLiteConnection());
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string folder = (string)reader["localPath"];
                folders.Add(folder);
            }
            return folders;
        }

        public List<string[]> GetLocalFolders2 ()
        {
            var folders = new List<string[]> ();
            SQLiteCommand command = new SQLiteCommand ("SELECT localPath, path FROM folders;", GetSQLiteConnection ());
            SQLiteDataReader reader = command.ExecuteReader ();
            while (reader.Read ()) {
                string [] folder = new string [] { (string)reader ["localPath"], (string)reader ["path"] };
                folders.Add (folder);
            }
            return folders;
        }


        public List<ChecksummedFile> GetChecksummedFiles()
        {
            List<ChecksummedFile> result = new List<ChecksummedFile>();
            SQLiteCommand command = new SQLiteCommand("SELECT localPath, checksum FROM files;", GetSQLiteConnection());
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                ChecksummedFile file = new ChecksummedFile((string)reader["localPath"], (string)reader["checksum"]);
                result.Add(file);
            }
            return result;
        }

        public List<string[]> GetLocalFiles2()
        {
            List<string[]> result = new List<string[]> ();
            SQLiteCommand command = new SQLiteCommand ("SELECT localPath, path FROM files;", GetSQLiteConnection ());
            SQLiteDataReader reader = command.ExecuteReader ();
            while (reader.Read ()) {
                string [] file = new string [] { (string)reader ["localPath"], (string)reader ["path"] };
                result.Add (file);
            }
            return result;
        }

        /// <summary>
        /// Helper method to execute an SQL command that does not return anything.
        /// </summary>
        /// <param name="text">SQL query, optionnally with @something parameters.</param>
        /// <param name="parameters">Parameters to replace in the SQL query.</param>
        private void ExecuteSQLAction(string text, Dictionary<string, object> parameters)
        {
            using (var command = new SQLiteCommand(GetSQLiteConnection()))
            {
                try
                {
                    ComposeSQLCommand(command, text, parameters);
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    Logger.Error(String.Format("Could not execute SQL: {0}; {1}", text, JsonConvert.SerializeObject(parameters)), e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Helper method to execute an SQL command that returns something.
        /// </summary>
        /// <param name="text">SQL query, optionnally with @something parameters.</param>
        /// <param name="parameters">Parameters to replace in the SQL query.</param>
        private object ExecuteSQLFunction(string text, Dictionary<string, object> parameters)
        {
            using (var command = new SQLiteCommand(GetSQLiteConnection()))
            {
                try
                {
                    ComposeSQLCommand(command, text, parameters);
                    return command.ExecuteScalar();
                }
                catch (SQLiteException e)
                {
                    Logger.Error(String.Format("Could not execute SQL: {0}; {1}", text, JsonConvert.SerializeObject(parameters)), e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Executes the SQL and Return one record results.
        /// </summary>
        /// <returns>results</returns>
        /// <param name="text">SQL</param>
        /// <param name="parameters">Parameters.</param>
        private Dictionary<string, object> ExecuteOneRecordSQL(string text, Dictionary<string, object> parameters)
        {
            using (var command = new SQLiteCommand(GetSQLiteConnection()))
            {
                try
                {
                    ComposeSQLCommand(command, text, parameters);
                    using (var dataReader = command.ExecuteReader())
                    {
                        var results = new Dictionary<string, object>();
                        if (dataReader.Read())
                        {
                            for (int i = 0; i < dataReader.FieldCount; i++)
                            {
                                results.Add(dataReader.GetName(i), dataReader[i]);
                            }
                        }
                        return results;
                    }
                }
                catch(SQLiteException e)
                {
                    Logger.Error(String.Format("Could not execute SQL: {0};", sqliteConnection), e);
                    throw;
                }
            }
        }


        /// <summary>
        /// Executes the SQL and Return multiple results.
        /// </summary>
        /// <returns>results</returns>
        /// <param name="text">SQL</param>
        /// <param name="parameters">Parameters.</param>
        private List<Dictionary<string, object>> ExecuteMultiRecordSQL(string text, Dictionary<string, object> parameters)
        {
            using (var command = new SQLiteCommand(GetSQLiteConnection()))
            {
                try
                {
                    ComposeSQLCommand(command, text, parameters);
                    using (var dataReader = command.ExecuteReader())
                    {
                        var results = new List<Dictionary<string, object>>();
                        while (dataReader.Read())
                        {
                            var record = new Dictionary<string, object>();
                            for (int i = 0; i < dataReader.FieldCount; i++)
                            {
                                record.Add(dataReader.GetName(i), dataReader[i]);
                            }
                            results.Add(record);
                        }
                        return results;
                    }
                }
                catch (SQLiteException e)
                {
                    Logger.Error(String.Format("Could not execute SQL: {0};", sqliteConnection), e);
                    throw;
                }
            }
        }

        /// <summary></summary>
        public enum OperationType
        {
            /// <summary></summary>
            UPLOAD,
            /// <summary></summary>
            DOWNLOAD,
            /// <summary></summary>
            CHANGE,
            /// <summary></summary>
            DELETE
        }

        /// <summary>
        /// Helper method to fill the parameters inside an SQL command.
        /// </summary>
        /// <param name="command">The SQL command object to fill. This method modifies it.</param>
        /// <param name="text">SQL query, optionnally with @something parameters.</param>
        /// <param name="parameters">Parameters to replace in the SQL query.</param>
        private void ComposeSQLCommand(SQLiteCommand command, string text, Dictionary<string, object> parameters)
        {
            command.CommandText = text;
            if (null != parameters)
            {
                foreach (KeyValuePair<string, object> pair in parameters)
                {
                    command.Parameters.AddWithValue(pair.Key, pair.Value);
                }
            }
        }

        private string operationTypeToString(OperationType type)
        {
            switch (type)
            {
                case OperationType.UPLOAD:
                    return "upload";
                case OperationType.CHANGE:
                    return "change";
                case OperationType.DOWNLOAD:
                    return "download";
                case OperationType.DELETE:
                    return "delete";
                default:
                    return "";
            }
        }
    }


    /// <summary>
    /// A file and its checksum.
    /// </summary>
    public class ChecksummedFile
    {
        /// <summary>
        /// Path to the file, relative to the root of the synchronized folder.
        /// </summary>
        public string RelativePath { get; private set; }

        /// <summary>
        /// Checksum stored in the local database for that file.
        /// Thus it is the checksum of the file after the previous synchronization.
        /// </summary>
        private string checksum;

        public ChecksummedFile(string relativePath, string checksum)
        {
            this.RelativePath = relativePath;
            this.checksum = checksum;
        }

        public bool HasChanged(string rootFolder)
        {
            string newChecksum = Database.Checksum(Utils.PathCombine(rootFolder, RelativePath));
            return ! newChecksum.Equals(checksum);
        }
    }
}
