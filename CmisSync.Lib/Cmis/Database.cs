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

    /**
     * Database to cache remote information from the CMIS server.
     * Implemented with SQLite.
     */
    public class Database
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Database));

        /**
         * Name of the SQLite database file.
         */
        private string databaseFileName;

        /**
         * SQLite connection to the underlying database.
         */
        private SQLiteConnection sqliteConnection;

        /**
         * Length of the prefix to remove before storing paths.
         */
        private int pathPrefixSize;


        /**
         * Constructor.
         */
        public Database(string dataPath)
        {
            this.databaseFileName = dataPath;
            // pathPrefixSize = dataPath.Length + 1; // +1 for the slash
            // pathPrefixSize = Folder.ROOT_FOLDER.Length + 1;
            pathPrefixSize = ConfigManager.CurrentConfig.FoldersPath.Length + 1;
        }


        /**
         * Connection to the database.
         * The sqliteConnection must not be used directly, used this method instead.
         */
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

                    // Hidden database file - No more necessary because file is moved on a system folder
                    // File.SetAttributes(databaseFileName, FileAttributes.Hidden);

                    if (createDatabase)
                    {
                        using (var command = new SQLiteCommand(sqliteConnection))
                        {
                            command.CommandText =
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
                            command.ExecuteNonQuery();
                            Logger.Info("Database created");
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("Error creating database: " + Utils.ToLogString(e));
                }
            }
            return sqliteConnection;
        }


        /**
         * Normalize a path.
         * All paths stored in database must be normalized.
         * Goals:
         * - Make data smaller in database
         * - Reduce OS-specific differences
         */
        public string Normalize(string path)
        {
            // Remove path prefix
            path = path.Substring(pathPrefixSize, path.Length - pathPrefixSize);
            // Normalize all slashes to forward slash
            path = path.Replace(@"\", "/");
            return path;
        }


        /**
         * Calculate the SHA1 checksum of a file.
         * Code from http://stackoverflow.com/a/1993919/226958
         */
        private string Checksum(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open))
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

        /**
         * Put all the values of a dictionary into a JSON string.
         */
        private string Json(Dictionary<string, string[]> dictionary)
        {
            return JsonConvert.SerializeObject(dictionary);
        }


        /*
         *
         * 
         *
         * Database operations
         * 
         * 
         * 
         */

        public void AddFile(string path, DateTime? serverSideModificationDate,
            Dictionary<string, string[]> metadata)
        {
            Logger.Debug("Start adding data in db for: " + path);
            string normalizedPath = Normalize(path);
            string checksum = String.Empty;

            // Make shure, that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // see: http://www.sqlite.org/datatype3.html
            if ((null != serverSideModificationDate) && (((DateTime)serverSideModificationDate).Kind != DateTimeKind.Utc)) {
                throw new ArgumentException("serverSideModificationDate is not UTC");
            }

            try
            {
                checksum = Checksum(path);
            }
            catch (IOException e)
            {
                Logger.Warn("IOException while reading file checksum during addition: " + path);
                // The file was removed while reading. Just skip it, as it does not need to be added anymore.
                return;
            }

            if (String.IsNullOrEmpty(checksum))
            {
                Logger.Warn("Bad checksum for " + path);
                return;
            }

            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                try
                {
                    command.CommandText =
                        @"INSERT OR REPLACE INTO files (path, serverSideModificationDate, metadata, checksum)
                            VALUES (@path, @serverSideModificationDate, @metadata, @checksum)";
                    command.Parameters.AddWithValue("path", normalizedPath);
                    command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                    command.Parameters.AddWithValue("metadata", Json(metadata));
                    // Why re-checksum file ?
                    // command.Parameters.AddWithValue("checksum", Checksum(path));
                    command.Parameters.AddWithValue("checksum", checksum);
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    Logger.Error(e.Message);
                    throw e;
                }
            }
            Logger.Debug("Adding data in db for: " + path + " finished");
        }


        public void AddFolder(string path, DateTime? serverSideModificationDate)
        {
            // Make shure, that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // see: http://www.sqlite.org/datatype3.html
            if ((null != serverSideModificationDate) && (((DateTime)serverSideModificationDate).Kind != DateTimeKind.Utc)) {
                throw new ArgumentException("serverSideModificationDate is not UTC");
            }

            path = Normalize(path);
            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                try
                {
                    command.CommandText =
                        @"INSERT OR REPLACE INTO folders (path, serverSideModificationDate)
                            VALUES (@path, @serverSideModificationDate)";
                    command.Parameters.AddWithValue("path", path);
                    command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    Logger.Error(e.Message);
                    throw e;
                }
            }
        }


        public void RemoveFile(string path)
        {
            path = Normalize(path);
            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                try
                {
                    command.CommandText =
                        "DELETE FROM files WHERE path=@filePath";
                    command.Parameters.AddWithValue("filePath", path);
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    Logger.Error(e.Message);
                    throw e;
                }
            }
        }


        public void RemoveFolder(string path)
        {
            path = Normalize(path);

            // Remove folder itself
            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                try
                {
                    command.CommandText =
                        "DELETE FROM folders WHERE path='" + path + "'";
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    Logger.Error(e.Message);
                    throw e;
                }
            }

            // Remove all folders under this folder
            using (var command = new SQLiteCommand(connection))
            {
                try
                {
                    command.CommandText =
                        "DELETE FROM folders WHERE path LIKE '" + path + "/%'";
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    Logger.Error(e.Message);
                    throw e;
                }
            }

            // Remove all files under this folder
            using (var command = new SQLiteCommand(connection))
            {
                try
                {
                    command.CommandText =
                        "DELETE FROM files WHERE path LIKE '" + path + "/%'";
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    Logger.Error(e.Message);
                    throw e;
                }
            }
        }


        public DateTime? GetServerSideModificationDate(string path)
        {
            path = Normalize(path);
            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                try
                {
                    command.CommandText =
                        "SELECT serverSideModificationDate FROM files WHERE path=@path";
                    command.Parameters.AddWithValue("path", path);
                    object obj = command.ExecuteScalar();
                    // sqlite limitation for DateTime: http://www.sqlite.org/datatype3.html
                    if (null != obj) {
#if __MonoCS__
                        obj = DateTime.SpecifyKind((DateTime)obj, DateTimeKind.Utc);
#else
                        obj = ((DateTime)obj).ToUniversalTime();
#endif
                    }
                    return (DateTime?)obj;
                }
                catch (SQLiteException e)
                {
                    Logger.Error(e.Message);
                    return null;
                }
            }
        }


        // TODO Combine this method and the next in a new method ModifyFile, and find out if GetServerSideModificationDate is really needed.
        public void SetFileServerSideModificationDate(string path, DateTime? serverSideModificationDate)
        {
            // Make shure, that the modification date is always UTC, because sqlite has no concept of Time-Zones
            // see: http://www.sqlite.org/datatype3.html
            if ((null != serverSideModificationDate) && (((DateTime)serverSideModificationDate).Kind != DateTimeKind.Utc)) {
                throw new ArgumentException("serverSideModificationDate is not UTC");
            }

            path = Normalize(path);
            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                try
                {
                    command.CommandText =
                        @"UPDATE files
                            SET serverSideModificationDate=@serverSideModificationDate
                            WHERE path=@path";
                    command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                    command.Parameters.AddWithValue("path", path);
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    Logger.Error(e.Message);
                    throw e;
                }
            }
        }


        public void RecalculateChecksum(string path)
        {
            string checksum = Checksum(path);
            path = Normalize(path);
            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                try
                {
                    command.CommandText =
                        @"UPDATE files
                            SET checksum=@checksum
                            WHERE path=@path";
                    command.Parameters.AddWithValue("checksum", checksum);
                    command.Parameters.AddWithValue("path", path);
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    Logger.Error(e.Message);
                    throw e;
                }
            }
        }


        public bool ContainsFile(string path)
        {
            path = Normalize(path);
            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText =
                    "SELECT serverSideModificationDate FROM files WHERE path=@path";
                command.Parameters.AddWithValue("path", path);
                object obj = command.ExecuteScalar();
                return obj != null;
            }
        }


        public bool ContainsFolder(string path)
        {
            path = Normalize(path);
            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText =
                    "SELECT serverSideModificationDate FROM folders WHERE path=@path";
                command.Parameters.AddWithValue("path", path);
                object obj = command.ExecuteScalar();
                return obj != null;
            }
        }

        /**
         * Check whether a file's content has changed since it was last synchronized.
         */
        public bool LocalFileHasChanged(string path)
        {
            string normalizedPath = Normalize(path);

            // Calculate current checksum.
            string currentChecksum = null;
            try
            {
                currentChecksum = Checksum(path);
            }
            catch (IOException e)
            {
                Logger.Error("IOException while reading file checksum: " + path);
                return true;
            }

            // Read previous checksum from database.
            string previousChecksum = null;
            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText =
                    "SELECT checksum FROM files WHERE path=@path";
                command.Parameters.AddWithValue("path", normalizedPath);
                object obj = command.ExecuteScalar();
                previousChecksum = (string)obj;
            }

            if (!currentChecksum.Equals(previousChecksum))
                Logger.Info("Checksum of " + path + " has changed from " + previousChecksum + " to " + currentChecksum);
            return !currentChecksum.Equals(previousChecksum);
        }

        public string GetChangeLogToken()
        {
            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText =
                    "SELECT value FROM general WHERE key=\"ChangeLogToken\"";
                object obj = command.ExecuteScalar();
                return (string)obj;
            }
        }

        public void SetChangeLogToken(string token)
        {
            var connection = GetSQLiteConnection();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText =
                    "INSERT OR REPLACE INTO general (key, value) VALUES (\"ChangeLogToken\", @token)";
                command.Parameters.AddWithValue("token", token);
                command.ExecuteNonQuery();
            }
        }
    }
}
