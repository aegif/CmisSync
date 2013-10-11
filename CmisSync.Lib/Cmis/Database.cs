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
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using log4net;

namespace CmisSync.Lib.Cmis
{
#if __MonoCS__
    // Mono's SQLite ADO implementation uses pure CamelCase (Sqlite vs. SQLite)
    // so we define some aliases here
    using SQLiteConnection = SqliteConnection;
    using SQLiteCommand = SqliteCommand;
    using SQLiteException = SqliteException;
#endif

    /// <summary>
    /// Database to cache remote information from the CMIS server.
    /// Implemented with SQLite.
    /// </summary>
    public class Database : IDisposable
    {
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
        private string pathPrefix;


        /// <summary>
        /// Length of the prefix to remove before storing paths.
        /// </summary>
        private int pathPrefixSize;


        /// <summary>
        /// Constructor.
        /// </summary>
        public Database(string dataPath)
        {
            this.databaseFileName = dataPath;
            pathPrefix = GetPathPrefix();
            pathPrefixSize = pathPrefix.Length+1;
        }


        /// <summary>
        /// Destructor.
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

                    sqliteConnection = new SQLiteConnection("Data Source=" + databaseFileName + ";PRAGMA journal_mode=WAL;");
                    sqliteConnection.Open();

                        string command =
                            @"CREATE TABLE IF NOT EXISTS files (
                            path TEXT PRIMARY KEY,
                            id TEXT,
                            serverSideModificationDate DATE,
                            metadata TEXT,
                            checksum TEXT);   /* Checksum of both data and metadata */
                        CREATE TABLE IF NOT EXISTS folders (
                            path TEXT PRIMARY KEY,
                            id TEXT,
                            serverSideModificationDate DATE,
                            metadata TEXT,
                            checksum TEXT);   /* Checksum of metadata */
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
                    if (createDatabase)
                    {
                        command = "INSERT INTO general (key, value) VALUES (\"PathPrefix\", @prefix)";
                        Dictionary<string, object> parameters = new Dictionary<string, object>();
                        parameters.Add("prefix", ConfigManager.CurrentConfig.FoldersPath);
                        ExecuteSQLAction(command, parameters);
                        Logger.Info("Database created");
                    }
                    Logger.Debug("Database migration successful");

                }
                catch (Exception e)
                {
                    Logger.Error("Error creating database: " + Utils.ToLogString(e));
                    throw;
                }
            }
            return sqliteConnection;
        }


        /// <summary>
        /// Normalize a path.
        /// All paths stored in database must be normalized.
        /// Goals:
        /// - Make data smaller in database
        /// - Reduce OS-specific differences
        /// </summary>
        private string Normalize(string path)
        {
            if (path.StartsWith(pathPrefix))
            {
                // Remove path prefix
                path = path.Substring(pathPrefixSize, path.Length - pathPrefixSize);
                // Normalize all slashes to forward slash
                path = path.Replace('\\', '/');
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
            return Path.Combine(ConfigManager.CurrentConfig.FoldersPath, path).Replace('/', Path.DirectorySeparatorChar);
        }


        /// <summary>
        /// Calculate the SHA1 checksum of a file.
        /// Code from http://stackoverflow.com/a/1993919/226958
        /// </summary>
        private string Checksum(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
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
        private string ChecksumToString(byte[] hash)
        {
            if(hash == null || hash.Length == 0) return String.Empty;
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
        /// Add a file to the database. And calculate the checksum of the file
        /// </summary>
        [Obsolete("Adding a file without a filehash could produce wrong behaviour, please use AddFile(string path, DateTime? serverSideModificationDate, Dictionary<string, string[]> metadata, byte[] filehash) instead")]
        public void AddFile(string path, string objectId, DateTime? serverSideModificationDate,
            Dictionary<string, string[]> metadata)
        {
            AddFile(path, objectId, serverSideModificationDate, metadata, null);
        }

        /// <summary>
        /// Add a file to the database.
        /// If checksum is not null, it will be used for the database entry
        /// </summary>
        public void AddFile(string path, string objectId, DateTime? serverSideModificationDate,
            Dictionary<string, string[]> metadata, byte[] filehash)
        {
            Logger.Debug("Starting database file addition for file: " + path);
            string normalizedPath = Normalize(path);
            string checksum = ChecksumToString(filehash);
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate)
            {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime();
            }

            if(String.IsNullOrEmpty(checksum))
            {
            // Calculate file checksum.
                try
                {
                    checksum = Checksum(path);
                }
                catch (IOException e)
                {
                    Logger.Warn("IOException while calculating checksum of " + path
                        + " , The file was removed while reading. Just skip it, as it does not need to be added anymore. "
                        + Utils.ToLogString(e));
                }
            }

            if (String.IsNullOrEmpty(checksum))
            {
                Logger.Warn("Bad checksum for " + path);
                return;
            }

            // Insert into database.
            string command =
                @"INSERT OR REPLACE INTO files (path, id, serverSideModificationDate, metadata, checksum)
                    VALUES (@path, @id, @serverSideModificationDate, @metadata, @checksum)";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", normalizedPath);
            parameters.Add("id", objectId);
            parameters.Add("serverSideModificationDate", serverSideModificationDate);
            parameters.Add("metadata", Json(metadata));
            parameters.Add("checksum", checksum);
            ExecuteSQLAction(command, parameters);
            Logger.Debug("Completed database file addition for file: " + path);
        }


        /// <summary>
        /// Add a folder to the database.
        /// </summary>
        public void AddFolder(string path, string objectId, DateTime? serverSideModificationDate)
        {
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate)
            {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime();
            }
            path = Normalize(path);

            string command =
                @"INSERT OR REPLACE INTO folders (path, id, serverSideModificationDate)
                    VALUES (@path, @id, @serverSideModificationDate)";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", path);
            parameters.Add("id", objectId);
            parameters.Add("serverSideModificationDate", serverSideModificationDate);
            ExecuteSQLAction(command, parameters);
        }


        /// <summary>
        /// Remove a file from the database.
        /// </summary>
        public void RemoveFile(string path)
        {
            path = Normalize(path);

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", path);
            ExecuteSQLAction("DELETE FROM files WHERE path=@path", parameters);

            parameters = new Dictionary<string, object>();
            parameters.Add("path", path);
            ExecuteSQLAction("DELETE FROM downloads WHERE path=@path", parameters);
        }


        /// <summary>
        /// move a file from the database.
        /// </summary>
        public void MoveFile(string oldPath, string newPath)
        {
            oldPath = Normalize(oldPath);
            newPath = Normalize(newPath);

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("oldpath", oldPath);
            parameters.Add("newpath", newPath);
            ExecuteSQLAction("UPDATE files SET path=@newpath WHERE path=@oldpath", parameters);
        }


        /// <summary>
        /// Remove a folder from the database.
        /// </summary>
        public void RemoveFolder(string path)
        {
            path = Normalize(path);

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", path);

            // Remove folder itself
            ExecuteSQLAction("DELETE FROM folders WHERE path=@path", parameters);

            // Remove all folders under this folder
            ExecuteSQLAction("DELETE FROM folders WHERE path LIKE '" + path + "/%'", null);

            // Remove all files under this folder
            ExecuteSQLAction("DELETE FROM files WHERE path LIKE '" + path + "/%'", null);
            ExecuteSQLAction("DELETE FROM downloads WHERE path LIKE '" + path + "/%'", null);
        }


        /// <summary>
        /// move a folder from the database.
        /// </summary>
        public void MoveFolder(string oldPath, string newPath)
        {
            oldPath = Normalize(oldPath);
            newPath = Normalize(newPath);

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("oldpath", oldPath);
            parameters.Add("newpath", newPath);
            ExecuteSQLAction("UPDATE folders SET path=@newpath WHERE path=@oldpath", parameters);

            string path = null;

            while(null != (path = (string)ExecuteSQLFunction("SELECT path FROM files WHERE path LIKE '" + oldPath + "/%'", null)))
            {
                string newFilePath = Path.Combine(newPath, path.Substring(oldPath.Length + 1)).Replace('\\', '/');
                Logger.Info("File Move");
                Logger.Info(oldPath);
                Logger.Info(newPath);
                Logger.Info(path);
                Logger.Info(newFilePath);
                Dictionary<string, object> parametersSub = new Dictionary<string, object>();
                parametersSub.Add("oldpath", path);
                parametersSub.Add("newpath", newFilePath);
                ExecuteSQLAction("UPDATE files SET path=@newpath WHERE path=@oldpath", parametersSub);
            }
            
            while (null != (path = (string)ExecuteSQLFunction("SELECT path FROM folders WHERE path LIKE '" + oldPath + "/%'", null)))
            {
                string newFolderPath = Path.Combine(newPath, path.Substring(oldPath.Length + 1)).Replace('\\', '/');
                Logger.Info("Folder Move");
                Logger.Info(oldPath);
                Logger.Info(newPath);
                Logger.Info(path);
                Logger.Info(newFolderPath);
                Dictionary<string, object> parametersSub = new Dictionary<string, object>();
                parametersSub.Add("oldpath", path);
                parametersSub.Add("newpath", newFolderPath);
                ExecuteSQLAction("UPDATE folders SET path=@newpath WHERE path=@oldpath", parametersSub);
            }
        }


        /// <summary>
        /// Get the time at which the file was last modified.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// </summary>
        public DateTime? GetServerSideModificationDate(string path)
        {
            path = Normalize(path);

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", path);
            object obj = ExecuteSQLFunction("SELECT serverSideModificationDate FROM files WHERE path=@path", parameters);
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


        /// <summary>
        /// Set the last modification date of a file.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// 
        /// TODO Combine this method and the next in a new method ModifyFile, and find out if GetServerSideModificationDate is really needed.
        /// </summary>
        public void SetFileServerSideModificationDate(string path, DateTime? serverSideModificationDate)
        {
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate)
            {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime();
            }
            path = Normalize(path);

            string command = @"UPDATE files
                    SET serverSideModificationDate=@serverSideModificationDate
                    WHERE path=@path";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("serverSideModificationDate", serverSideModificationDate);
            parameters.Add("path", path);
            ExecuteSQLAction(command, parameters);
        }


        /// <summary>
        /// Get the date at which the file was last download.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// </summary>
        public DateTime? GetDownloadServerSideModificationDate(string path)
        {
            path = Normalize(path);

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", path);
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


        /// <summary>
        /// Set the last download date of a file.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// </summary>
        public void SetDownloadServerSideModificationDate(string path, DateTime? serverSideModificationDate)
        {
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate)
            {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime();
            }
            path = Normalize(path);

            string command = @"INSERT OR REPLACE INTO downloads (path, serverSideModificationDate)
                    VALUES (@path, @serverSideModificationDate)";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("serverSideModificationDate", serverSideModificationDate);
            parameters.Add("path", path);
            ExecuteSQLAction(command, parameters);
        }

        /// <summary>
        /// Gets the upload retry counter.
        /// </summary>
        /// <returns>
        /// The upload retry counter.
        /// </returns>
        /// <param name='path'>
        /// Path of the local file.
        /// </param>
        public long GetOperationRetryCounter(string path, OperationType type)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("date", File.GetLastWriteTimeUtc(path));
            parameters.Add("path", Normalize(path));
            object result = ExecuteSQLFunction(String.Format("SELECT {0}Counter FROM failedoperations WHERE path=@path AND lastLocalModificationDate=@date", operationTypeToString(type)), parameters);
            if( result != null && !(result is DBNull))
            { return (long) result; }
            else
            { return 0; }
        }

        /// <summary>
        /// Sets the upload retry counter.
        /// </summary>
        /// <param name='path'>
        /// Path of the local file.
        /// </param>
        /// <param name='counter'>
        /// Counter.
        /// </param>
        public void SetOperationRetryCounter(string path, long counter, OperationType type)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("date", File.GetLastWriteTimeUtc(path));
            parameters.Add("path", Normalize(path));
            parameters.Add("counter", (counter>=0) ? counter:0);
            string uploadCounter = "(SELECT CASE WHEN lastLocalModificationDate=@date THEN uploadCounter ELSE '' END FROM failedoperations WHERE path=@path)";
            string downloadCounter = "(SELECT CASE WHEN lastLocalModificationDate=@date THEN downloadCounter ELSE '' END FROM failedoperations WHERE path=@path)";
            string changeCounter = "(SELECT CASE WHEN lastLocalModificationDate=@date THEN changeCounter ELSE '' END FROM failedoperations WHERE path=@path)";
            string deleteCounter = "(SELECT CASE WHEN lastLocalModificationDate=@date THEN deleteCounter ELSE '' END FROM failedoperations WHERE path=@path)";
            switch(type)
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
                            )",uploadCounter,downloadCounter,changeCounter,deleteCounter);
            ExecuteSQLAction(command, parameters);
        }

        /// <summary>
        /// Deletes the upload retry counter.
        /// </summary>
        /// <param name='path'>
        /// Path of the local file.
        /// </param>
        public void DeleteAllFailedOperations(string path)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", Normalize(path));
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
        public void RecalculateChecksum(string path)
        {
            string checksum;
            try
            {
                checksum = Checksum(path);
            }
            catch (IOException)
            {
                Logger.Error("IOException while reading file checksum: " + path);
                return;
            }

            path = Normalize(path);

            string command = @"UPDATE files
                    SET checksum=@checksum
                    WHERE path=@path";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("checksum", checksum);
            parameters.Add("path", path);
            ExecuteSQLAction(command, parameters);
        }


        /// <summary>
        /// Checks whether the database contains a given file.
        /// </summary>
        public bool ContainsFile(string path)
        {
            path = Normalize(path);

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", path);
            return null != ExecuteSQLFunction("SELECT serverSideModificationDate FROM files WHERE path=@path", parameters);
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
        /// Checks whether the database contains a given folder.
        /// </summary>
        public bool ContainsFolder(string path)
        {
            path = Normalize(path);

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", path);
            return null != ExecuteSQLFunction("SELECT serverSideModificationDate FROM folders WHERE path=@path", parameters);
        }


        /// <summary>
        /// <returns>path field in folders table for <paramref name="id"/></returns>
        /// </summary>
        public string GetFolderPath(string id)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("id", id);
            return Denormalize((string)ExecuteSQLFunction("SELECT path FROM folders WHERE id=@id", parameters));
        }


        /// <summary>
        /// Check whether a file's content has changed locally since it was last synchronized.
        /// This happens when the user edits a file on the local computer.
        /// This method does not communicate with the CMIS server, it just checks whether the checksum has changed.
        /// </summary>
        public bool LocalFileHasChanged(string path)
        {
            string normalizedPath = Normalize(path);

            // Calculate current checksum.
            string currentChecksum = null;
            try
            {
                currentChecksum = Checksum(path);
            }
            catch (IOException)
            {
                Logger.Error("IOException while reading file checksum: " + path);
                return true;
            }

            // Read previous checksum from database.
            string previousChecksum = null;
            string command = "SELECT checksum FROM files WHERE path=@path";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", normalizedPath);
            previousChecksum = (string)ExecuteSQLFunction(command, parameters);

            if (!currentChecksum.Equals(previousChecksum))
                Logger.Info("Checksum of " + path + " has changed from " + previousChecksum + " to " + currentChecksum);
            return !currentChecksum.Equals(previousChecksum);
        }


        /// <summary>
        /// Get the ChangeLog token that was stored at the end of the last successful CmisSync synchronization.
        /// </summary>
        public string GetChangeLogToken()
        {
            return (string)ExecuteSQLFunction("SELECT value FROM general WHERE key=\"ChangeLogToken\"", null);
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
        }

        /// <summary>
        /// Gets the path prefix.
        /// If no prefix has been found, the db will be migrated and the old one will be returned
        /// </summary>
        /// <returns>
        /// The path prefix.
        /// </returns>
        private string GetPathPrefix()
        {
            object result = ExecuteSQLFunction("SELECT value FROM general WHERE key=\"PathPrefix\"", null);
            // Migration of databases, which do not have any prefix safed
            if(result == null) {
                string oldprefix = Path.Combine(ConfigManager.CurrentConfig.HomePath, "DataSpace Sync");
                SetPathPrefix(oldprefix);
                return oldprefix;
            }else {
                return (string) result;
            }
        }

        /// <summary>
        /// Sets the path prefix.
        /// </summary>
        /// <param name='pathprefix'>
        /// Pathprefix.
        /// </param>
        private void SetPathPrefix(string pathprefix)
        {
            string command = "INSERT OR REPLACE INTO general (key, value) VALUES (\"PathPrefix\", @prefix)";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("prefix", pathprefix);
            ExecuteSQLAction(command, parameters);
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
                    Logger.Error(e.Message);
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
                    Logger.Error(e.Message);
                    throw;
                }
            }
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

        public enum OperationType 
        {
            UPLOAD, DOWNLOAD, CHANGE, DELETE
        }

        private string operationTypeToString(OperationType type)
        {
            switch(type) {
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
}
