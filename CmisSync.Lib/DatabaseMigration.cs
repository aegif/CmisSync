using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
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

using CmisSync.Auth;
using DotCMIS.Client;

namespace CmisSync.Lib.Cmis
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
    /// Database migration.
    /// </summary>
    public static class DatabaseMigration
    {
        /// <summary>
        /// Log.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Database));
       



        /// <summary>
        /// Migrate the specified Database file to current version.
        /// </summary>
        /// <param name="filePath">Database File path.</param>
        /// <param name="currentDatabaseVersion">Current database version.</param>
        public static void Migrate(string filePath, int currentDbVersion)
        {
            try
            {
                Logger.Info(String.Format("Checking whether database {0} exists", filePath));
                if (!File.Exists(filePath))
                {
                    Logger.Info(string.Format("Database file {0} not exists.", filePath));
                    return;
                }
                    
                var connection = GetConnection(filePath);
                int dbVersion = GetDatabaseVersion(connection);

                if (dbVersion >= currentDbVersion)
                {
                    return;     // migration is not needed
                }

                Logger.DebugFormat("Current Database Schema must be update from {0} to {0}", dbVersion, currentDbVersion);

                switch (dbVersion)
                {
                    case 0:
                        MigrateFromVersion0(filePath, connection, currentDbVersion);
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unexpected database version: {0}.", dbVersion)); 
                        break;
                }
                        
                Logger.Debug("Database migration successful");
            }
            catch (Exception e)
            {
                Logger.Error("Error migrating database: " + e.Message, e);
                throw;
            }
        }

        /// <summary>
        /// Migrates from Database version0.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <param name="connection">Connection.</param>
        private static void MigrateFromVersion0(string filePath, SQLiteConnection connection, int currentVersion)
        {
            var filesTableColumns = GetColumnNames(GetConnection(filePath, connection), "files");
            if (!filesTableColumns.Contains("localPath"))
            {
                ExecuteSQLAction(GetConnection(filePath, connection), 
                    "ALTER TABLE files ADD COLUMN localPath TEXT;", null);
            }
            if (!filesTableColumns.Contains("id"))
            {
                ExecuteSQLAction(GetConnection(filePath, connection), 
                    "ALTER TABLE files ADD COLUMN id TEXT;", null);
            }

            var foldersTableColumns = GetColumnNames(connection, "files");
            if (!foldersTableColumns.Contains("localPath"))
            {
                ExecuteSQLAction(GetConnection(filePath, connection), 
                    "ALTER TABLE folders ADD COLUMN localPath TEXT;", null);
            }
            if (!foldersTableColumns.Contains("id"))
            {
                ExecuteSQLAction(GetConnection(filePath, connection), 
                    "ALTER TABLE folders ADD COLUMN id TEXT;", null);
            }

            var parameters = new Dictionary<string, object>();
            parameters.Add("prefix", ConfigManager.CurrentConfig.FoldersPath);
            ExecuteSQLAction(GetConnection(filePath, connection),
                "INSERT OR IGNORE INTO general (key, value) VALUES (\"PathPrefix\", @prefix);", parameters);
            SetDatabaseVersion(GetConnection(filePath, connection), currentVersion);
        }


        /// <summary>
        /// Gets the Database connection.
        /// </summary>
        /// <returns>Database connection.</returns>
        /// <param name="filePath">File path.</param>
        private static SQLiteConnection GetConnection(string filePath)
        {
            var connection = new SQLiteConnection("Data Source=" + filePath + ";PRAGMA journal_mode=WAL;");
            connection.Open();

            return connection;
        }


        /// <summary>
        /// Gets the Database connection.
        /// Check the exsisting connection and get if necessary
        /// </summary>
        /// <returns>check exsisting connection and get if necessary</returns>
        /// <param name="filePath">File path.</param>
        /// <param name="connection">exsisting connection.</param>
        private static SQLiteConnection GetConnection(string filePath, SQLiteConnection connection)
        {
            if (connection == null || connection.State == System.Data.ConnectionState.Broken)
            {
                connection = GetConnection(filePath);
            }

            return connection;
        }

        /// <summary>
        /// Gets the database version.
        /// </summary>
        /// <returns>The database version. If DB not recorded, return 0. </returns>
        /// <param name="connection">Connection.</param>
        private static int GetDatabaseVersion(SQLiteConnection connection)
        {
            var objUserVersion = ExecuteSQLFunction(connection ,"PRAGMA user_version;", null);
            if (objUserVersion != null)
            {
                return (int)objUserVersion;
            }
            return 0;
        }

        /// <summary>
        /// Sets the database version.
        /// </summary>
        /// <param name="connection">Connection.</param>
        /// <param name="version">database version</param> 
        private static void SetDatabaseVersion(SQLiteConnection connection, int version)
        {
            string command = "PRAGMA user_version=" + version.ToString();
            ExecuteSQLAction(connection ,command, null);
        }



        /// <summary>
        /// Helper method to get column Names
        /// </summary>
        /// <param name="connection">database connection</param>
        /// <param name="table">table name</param>
        /// <returns>array of column name</returns>
        private static string[] GetColumnNames(SQLiteConnection connection, string table)
        {
            using (var command = new SQLiteCommand(connection))
            {
                string sql = String.Format("PRAGMA table_info('{0}');", table);
                command.CommandText = sql;
                SQLiteDataReader dataReader;

                try
                {
                    dataReader = command.ExecuteReader();
                }
                catch (SQLiteException e)
                {
                    Logger.Error(String.Format("Could not execute SQL: {0};", sql), e);
                    throw;
                }

                int nameOrdinal = dataReader.GetOrdinal("name");
                var columnList = new List<string>();
                while (dataReader.Read())
                {
                    columnList.Add((string)dataReader[nameOrdinal]);
                }
                return columnList.ToArray();
            }
        }


        /// <summary>
        /// Helper method to execute an SQL command that does not return anything.
        /// </summary>
        /// <param name="connection">database connection</param>
        /// <param name="text">SQL query, optionnally with @something parameters.</param>
        /// <param name="parameters">Parameters to replace in the SQL query.</param>
        private static void ExecuteSQLAction(SQLiteConnection connection, string text, Dictionary<string, object> parameters)
        {
            using (var command = new SQLiteCommand(connection))
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
        /// <param name="connection">database connection</param>
        /// <param name="text">SQL query, optionnally with @something parameters.</param>
        /// <param name="parameters">Parameters to replace in the SQL query.</param>
        private static object ExecuteSQLFunction(SQLiteConnection connection, string text, Dictionary<string, object> parameters)
        {
            using (var command = new SQLiteCommand(connection))
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
        /// Helper method to fill the parameters inside an SQL command.
        /// </summary>
        /// <param name="command">The SQL command object to fill. This method modifies it.</param>
        /// <param name="text">SQL query, optionnally with @something parameters.</param>
        /// <param name="parameters">Parameters to replace in the SQL query.</param>
        private static void ComposeSQLCommand(SQLiteCommand command, string text, Dictionary<string, object> parameters)
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

        /// <summary>
        /// Fills the object identifier.
        /// </summary>
        /// <param name="dbFilePath">Db file path.</param>
        /// <param name="folderName">Folder name.</param>
        public static void FillObjectId(string dbFilePath, string folderName)
        {
            var configFolder = ConfigManager.CurrentConfig.getFolder(folderName);
            var session = Auth.Auth.GetCmisSession(
                              ((Uri)configFolder.RemoteUrl).ToString(),
                              configFolder.UserName,
                              Crypto.Deobfuscate(configFolder.ObfuscatedPassword),
                              configFolder.RepositoryId);

            var filters = new HashSet<string>();
            filters.Add("cmis:objectId");
            session.DefaultContext = session.CreateOperationContext(filters, false, true, false, DotCMIS.Enums.IncludeRelationshipsFlag.None, null, true, null, true, 100);
            string remoteRootFolder = configFolder.RemotePath;
            string localRootFolder = configFolder.LocalPath.Substring(ConfigManager.CurrentConfig.FoldersPath.Length + 1);

            string newDbFile = dbFilePath + ".new";
            if (File.Exists(newDbFile))
            {
                File.Delete(newDbFile);
            }

            try
            {
                File.Copy(dbFilePath, newDbFile);

                ExecuteSQLAction(GetConnection(newDbFile), "DROP TABLE IF EXISTS files;", null);
                ExecuteSQLAction(GetConnection(newDbFile),
                    @"CREATE TABLE IF NOT EXISTS files (
                        path TEXT PRIMARY KEY,
                        localPath TEXT, /* Local path is sometimes different due to local filesystem constraints */
                        id TEXT,
                        serverSideModificationDate DATE,
                        metadata TEXT,
                        checksum TEXT);   /* Checksum of both data and metadata */", null);

                using (var newConnection = GetConnection(newDbFile))
                using (var command = new SQLiteCommand(GetConnection(dbFilePath)))
                {
                    string sql = "SELECT * FROM files;";
                    command.CommandText = sql;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            //int id = Convert.ToInt32(reader["id"]);
                            string path = reader["path"].ToString();
                            var modDate = (DateTime)reader["serverSideModificationDate"];
                            string metadata = reader["metadata"].ToString();
                            string checksum = reader["checksum"].ToString();

                            string remotePath = remoteRootFolder + path.Substring(localRootFolder.Length);
                            string id = null;
                            try
                            {
                                id = session.GetObjectByPath(remotePath).Id;
                            }
                            catch (DotCMIS.Exceptions.CmisObjectNotFoundException e)
                            {
                                Logger.Info(String.Format("File Not Found: \"{0}\"", remotePath), e);
                                id = null;
                            }
                            catch (DotCMIS.Exceptions.CmisPermissionDeniedException e)
                            {
                                Logger.Info(String.Format("PermissionDenied: \"{0}\"", remotePath), e); 
                                id = null;
                            }

                            var parameters = new Dictionary<string, object>();
                            parameters.Add("@path", path);
                            parameters.Add("@id", id);
                            parameters.Add("@modDate", modDate);
                            parameters.Add("@metadata", metadata);
                            parameters.Add("@checksum", checksum);
                            ExecuteSQLAction(newConnection, "INSERT INTO files (path,id,serverSideModificationDate,metadata,checksum) VALUES (@path, @id, @modDate, @metadata, @checksum);", parameters);

                        }
                    }
                }

                ExecuteSQLAction(GetConnection(newDbFile), "DROP TABLE IF EXISTS folder;", null);
                ExecuteSQLAction(GetConnection(newDbFile),
                    @"CREATE TABLE IF NOT EXISTS folders (
                            path TEXT PRIMARY KEY,
                            localPath TEXT, /* Local path is sometimes different due to local filesystem constraints */
                            id TEXT,
                            serverSideModificationDate DATE,
                            metadata TEXT,
                            checksum TEXT);   /* Checksum of metadata */", null);

                using (var newConnection = GetConnection(newDbFile))
                using (var command = new SQLiteCommand(GetConnection(dbFilePath)))
                {
                    string sql = "SELECT * FROM folders;";
                    command.CommandText = sql;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            //int id = Convert.ToInt32(reader["id"]);
                            var path = reader["path"].ToString();
                            var modDate = (DateTime)reader["serverSideModificationDate"];
                            var metadata = reader["metadata"];
                            var checksum = reader["checksum"];

                            string remotePath = remoteRootFolder + path.Substring(localRootFolder.Length);
                            string id = null;
                            try
                            {
                                id = session.GetObjectByPath(remotePath).Id;
                            }
                            catch (DotCMIS.Exceptions.CmisObjectNotFoundException e)
                            {
                                Logger.Info(String.Format("File Not Found: \"{0}\"", remotePath), e);
                                id = null;
                            }
                            catch (DotCMIS.Exceptions.CmisPermissionDeniedException e)
                            {
                                Logger.Info(String.Format("PermissionDenied: \"{0}\"", remotePath), e); 
                                id = null;
                            }

                            var parameters = new Dictionary<string, object>();
                            parameters.Add("@path", path);
                            parameters.Add("@id", id);
                            parameters.Add("@modDate", modDate);
                            parameters.Add("@metadata", metadata);
                            parameters.Add("@checksum", checksum);
                            ExecuteSQLAction(newConnection, "INSERT INTO folders (path,id,serverSideModificationDate,metadata,checksum) VALUES (@path, @id, @modDate, @metadata, @checksum);", parameters);

                        }
                    }
                }

                string oldDbFile = dbFilePath + ".old";
                if (File.Exists(oldDbFile))
                {
                    File.Delete(oldDbFile);
                }
                File.Move(newDbFile, dbFilePath);
            }
            catch(Exception e)
            {
                Logger.Info("Failed to fills object id.", e);
                throw;
            }
        }
    }
}

