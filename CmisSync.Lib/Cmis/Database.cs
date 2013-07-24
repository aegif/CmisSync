using System;
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
        /// Length of the prefix to remove before storing paths.
        /// </summary>
        private int pathPrefixSize;


        /// <summary>
        /// Constructor.
        /// </summary>
        public Database(string dataPath)
        {
            this.databaseFileName = dataPath;
            pathPrefixSize = ConfigManager.CurrentConfig.FoldersPath.Length + 1;
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

                    if (createDatabase)
                    {
                        string command = 
                            @"CREATE TABLE files (
                            path TEXT PRIMARY KEY,
                            serverSideModificationDate DATE,
                            metadata TEXT,
                            checksum TEXT);   /* Checksum of both data and metadata */
                        CREATE TABLE folders (
                            path TEXT PRIMARY KEY,
                            serverSideModificationDate DATE,
                            metadata TEXT,
                            checksum TEXT);   /* Checksum of metadata */
                        CREATE TABLE general (
                            key TEXT PRIMARY KEY,
                            value TEXT);";    /* Other data such as ChangeLog token */
                        ExecuteSQLAction(command, null);
                        Logger.Info("Database created");
                    }
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
            // Remove path prefix
            path = path.Substring(pathPrefixSize, path.Length - pathPrefixSize);
            // Normalize all slashes to forward slash
            path = path.Replace('\\', '/');
            return path;
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
                    StringBuilder formatted = new StringBuilder(2 * hash.Length);
                    foreach (byte b in hash)
                    {
                        formatted.AppendFormat("{0:X2}", b);
                    }
                    return formatted.ToString();
                }
            }
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
        /// Add a file to the database.
        /// </summary>
        public void AddFile(string path, DateTime? serverSideModificationDate,
            Dictionary<string, string[]> metadata)
        {
            Logger.Debug("Starting database file addition for file: " + path);
            string normalizedPath = Normalize(path);
            string checksum = String.Empty;

            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate)
            {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime();
            }

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
                return;
            }

            if (String.IsNullOrEmpty(checksum))
            {
                Logger.Warn("Bad checksum for " + path);
                return;
            }

            // Insert into database.
            string command =
                @"INSERT OR REPLACE INTO files (path, serverSideModificationDate, metadata, checksum)
                    VALUES (@path, @serverSideModificationDate, @metadata, @checksum)";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", normalizedPath);
            parameters.Add("serverSideModificationDate", serverSideModificationDate);
            parameters.Add("metadata", Json(metadata));
            parameters.Add("checksum", checksum);
            ExecuteSQLAction(command, parameters);
            Logger.Debug("Completed database file addition for file: " + path);
        }


        /// <summary>
        /// Add a folder to the database.
        /// </summary>
        public void AddFolder(string path, DateTime? serverSideModificationDate)
        {
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // See http://www.sqlite.org/datatype3.html
            if (null != serverSideModificationDate)
            {
                serverSideModificationDate = ((DateTime)serverSideModificationDate).ToUniversalTime();
            }
            path = Normalize(path);

            string command =
                @"INSERT OR REPLACE INTO folders (path, serverSideModificationDate)
                    VALUES (@path, @serverSideModificationDate)";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("path", path);
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
        }


        /// <summary>
        /// Remove a folder from the database.
        /// </summary>
        public void RemoveFolder(string path)
        {
            path = Normalize(path);

            // Remove folder itself
            ExecuteSQLAction("DELETE FROM folders WHERE path='" + path + "'", null);

            // Remove all folders under this folder
            ExecuteSQLAction("DELETE FROM folders WHERE path LIKE '" + path + "/%'", null);

            // Remove all files under this folder
            ExecuteSQLAction("DELETE FROM files WHERE path LIKE '" + path + "/%'", null);
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


        // 

        /// <summary>
        /// Set the last modification date of a file.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// 
        /// TODO Combine this method and the next in a new method ModifyFile, and find out if GetServerSideModificationDate is really needed.
        /// </summary>
        public void SetFileServerSideModificationDate(string path, DateTime? serverSideModificationDate)
        {
            // Make sure that the modification date is always UTC, because sqlite has no concept of Time-Zones.
            // See http://www.sqlite.org/datatype3.html
            if ((null != serverSideModificationDate) && (((DateTime)serverSideModificationDate).Kind != DateTimeKind.Utc)) {
                throw new ArgumentException("serverSideModificationDate is not UTC");
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
        /// Recalculate the checksum of a file and save it to database.
        /// </summary>
        public void RecalculateChecksum(string path)
        {
            string checksum = Checksum(path);
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
    }
}
