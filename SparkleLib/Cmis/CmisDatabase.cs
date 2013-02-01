using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace SparkleLib.Cmis
{
    /**
     * Database to cache remote information from the CMIS server.
     * Implemented with SQLite.
     */
    public class CmisDatabase
    {
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
        public CmisDatabase(string dataPath)
        {
            this.databaseFileName = dataPath + ".cmissync";
            pathPrefixSize = dataPath.Length + 1; // +1 for the slash
        }


        /**
         * Connection to the database.
         * The sqliteConnection must not be used directly, used this method instead.
         */
        public SQLiteConnection GetSQLiteConnection()
        {
            if (sqliteConnection == null || sqliteConnection.State == System.Data.ConnectionState.Broken)
            {
                bool createDatabase = ! File.Exists(databaseFileName);
                sqliteConnection = new SQLiteConnection("Data Source=" + databaseFileName);
                sqliteConnection.Open();
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
                    }
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
        private string Json(Dictionary<string, string> dictionary)
        {
            return JsonConvert.SerializeObject(dictionary);
            /*StringBuilder json = new StringBuilder();
            json.Append("{\n");
            foreach (var entry in dictionary)
            {
                json.Append("\"");
                json.Append(entry.Key);
                json.Append("\" : \"");
                json.Append(entry.Value);
                json.Append("\"\n");
                json.Append(",");
                json.Append("\n");
            }
            json.Append("}");
            return json.ToString();*/
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
            Dictionary<string, string> metadata)
        {
            string normalizedPath = Normalize(path);

            try
            {
                string checksum = Checksum(path);
            }
            catch (IOException e) {
                SparkleLogger.LogInfo("CmisDatabase", "IOException while reading file checksum during addition: " + path);
                // The file was removed while reading. Just skip it, as it does not need to be added anymore.
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
                    command.Parameters.AddWithValue("checksum", Checksum(path));
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
                }
            }
        }


        public void AddFolder(string path, DateTime? serverSideModificationDate)
        {
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
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
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
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
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
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
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
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
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
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
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
                    return (DateTime?)obj;
                }
                catch (SQLiteException e)
                {
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
                    return null;
                }
            }
        }


        public void SetFileServerSideModificationDate(string path, DateTime? serverSideModificationDate)
        {
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
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
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
            try {
                currentChecksum = Checksum(path);
            }
            catch(IOException e) {
                SparkleLogger.LogInfo("CmisDatabase", "IOException while reading file checksum: " + path);
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

            return ! currentChecksum.Equals(previousChecksum);
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
