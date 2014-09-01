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
        private static readonly ILog Logger = LogManager.GetLogger(typeof(DatabaseMigration));

        /// <summary>
        /// Migrate all Database file in current configuration
        /// </summary>
        /// <param name="dbFile">Database file.</param>
        public static void Migrate(string dbFile)
        {
            var syncFolder = ConfigManager.CurrentConfig.Folder.Find((f) => f.GetRepoInfo().CmisDatabase == dbFile);
            Migrate(syncFolder);
        }


        /// <summary>
        /// Migrate the specified Database file to current version.
        /// </summary>
        /// <param name="syncFolder">target syncFolder</param>
        public static void Migrate(Config.SyncConfig.Folder syncFolder)
        {
            int currentDbVersion = Database.SchemaVersion;
            string dbPath = syncFolder.GetRepoInfo().CmisDatabase;

            try
            {
                Logger.Info(String.Format("Checking whether database {0} exists", dbPath));
                if (!File.Exists(dbPath))
                {
                    Logger.Info(string.Format("Database file {0} not exists.", dbPath));
                    return;
                }

                using (var connection = GetConnection(dbPath))
                {

                    int dbVersion = GetDatabaseVersion(connection);

                    if (dbVersion >= currentDbVersion)
                    {
                        return;     // migration is not needed
                    }

                    Logger.DebugFormat("Current Database Schema must be updated from {0} to {0}", dbVersion, currentDbVersion);

                    switch (dbVersion)
                    {
                        case 0:
                            MigrateFromVersion0(syncFolder, connection, currentDbVersion);
                            break;
                        default:
                            throw new NotSupportedException(String.Format("Unexpected database version: {0}.", dbVersion));
                    }
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
        /// <param name="currentVersion">Current database schema version.</param>
        private static void MigrateFromVersion0(Config.SyncConfig.Folder syncFolder, SQLiteConnection connection, int currentVersion)
        {
            // Add columns
            var filesTableColumns = GetColumnNames(connection, "files");
            if (!filesTableColumns.Contains("localPath"))
            {
                ExecuteSQLAction(connection, 
                    @"ALTER TABLE files ADD COLUMN localPath TEXT;", null);
            }
            if (!filesTableColumns.Contains("id"))
            {
                ExecuteSQLAction(connection, 
                    @"ALTER TABLE files ADD COLUMN id TEXT;", null);
            }

            var foldersTableColumns = GetColumnNames(connection, "folders");
            if (!foldersTableColumns.Contains("localPath"))
            {
                ExecuteSQLAction(connection, 
                    @"ALTER TABLE folders ADD COLUMN localPath TEXT;", null);
            }
            if (!foldersTableColumns.Contains("id"))
            {
                ExecuteSQLAction(connection, 
                    @"ALTER TABLE folders ADD COLUMN id TEXT;", null);
            }

            // Create indices
            ExecuteSQLAction(connection,
                @"CREATE INDEX IF NOT EXISTS files_localPath_index ON files (localPath);
                  CREATE INDEX IF NOT EXISTS files_id_index ON files (id);
                  CREATE INDEX IF NOT EXISTS folders_localPath_index ON folders (localPath);
                  CREATE INDEX IF NOT EXISTS folders_id_index ON folders (id);", null);

            // Create tables
            ExecuteSQLAction(connection,
                @"CREATE TABLE IF NOT EXISTS downloads (
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
                    deleteMessage TEXT);",
                null);

            var parameters = new Dictionary<string, object>();
            parameters.Add("prefix", ConfigManager.CurrentConfig.FoldersPath);
            ExecuteSQLAction(connection,
                "INSERT OR IGNORE INTO general (key, value) VALUES (\"PathPrefix\", @prefix);", parameters);

			FillObjectId(syncFolder, connection);

			// If everything has succeded, upgrade database version number.
			SetDatabaseVersion(connection, currentVersion);
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
                return (int)(long)objUserVersion;
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

                try
                {
                    using (var dataReader = command.ExecuteReader())
                    {
                        int nameOrdinal = dataReader.GetOrdinal("name");
                        var columnList = new List<string>();
                        while (dataReader.Read())
                        {
                            columnList.Add((string)dataReader[nameOrdinal]);
                        }

                        return columnList.ToArray();
                    }
                }
                catch(SQLiteException e)
                {
                    Logger.Error(String.Format("Could not execute SQL: {0};", sql), e);
                    throw;
                }
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
        public static void FillObjectId(Config.SyncConfig.Folder syncFolder, SQLiteConnection connection)
        {
            Utils.NotifyUser("CmisSync needs to upgrade its own local data. Please stay on the network for a few minutes.");

            var session = Auth.Auth.GetCmisSession(
                              ((Uri)syncFolder.RemoteUrl).ToString(),
                              syncFolder.UserName,
                              Crypto.Deobfuscate(syncFolder.ObfuscatedPassword),
                              syncFolder.RepositoryId);

            var filters = new HashSet<string>();
            filters.Add("cmis:objectId");
            //session.DefaultContext = session.CreateOperationContext(filters, false, true, false, DotCMIS.Enums.IncludeRelationshipsFlag.None, null, true, null, true, 100);
            string remoteRootFolder = syncFolder.RemotePath;
            string localRootFolder = syncFolder.LocalPath.Substring(ConfigManager.CurrentConfig.FoldersPath.Length + 1);

            try
            {
                using (var command = new SQLiteCommand(connection))
                {
                    // Fill missing columns of all files.
                    command.CommandText = "SELECT path FROM files WHERE id IS NULL;";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Example: "old-db-1.0.13/テスト・テスト/テスト用ファイル.pptx"
                            string legacyPath = reader["path"].ToString();

                            // Example: /Sites/cmissync/documentLibrary/tests/テスト・テスト/テスト用ファイル.pptx
                            string remotePath = remoteRootFolder + legacyPath.Substring(localRootFolder.Length);

                            // Example: テスト・テスト/テスト用ファイル.pptx
                            string localPath = legacyPath.Substring(localRootFolder.Length + 1);

                            string id = null;
                            try
                            {
                                id = session.GetObjectByPath(remotePath).Id;
                            }
                            catch (DotCMIS.Exceptions.CmisObjectNotFoundException e)
                            {
                                Logger.Info(String.Format("File Not Found: \"{0}\"", remotePath), e);
                            }
                            catch (DotCMIS.Exceptions.CmisPermissionDeniedException e)
                            {
                                Logger.Info(String.Format("PermissionDenied: \"{0}\"", remotePath), e);
                            }

                            var parameters = new Dictionary<string, object>();
                            parameters.Add("@id", id);
                            parameters.Add("@localPath", localPath);
                            parameters.Add("@path", legacyPath);
                            ExecuteSQLAction(connection, "UPDATE files SET id = @id, localPath = @localPath WHERE path = @path;", parameters);
                        }
                    }

                    // Fill missing columns of all folders.
                    command.CommandText = "SELECT path FROM folders WHERE id IS NULL;";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string legacyPath = reader["path"].ToString();
                            string remotePath = remoteRootFolder + legacyPath.Substring(localRootFolder.Length);
                            string localPath = legacyPath.Substring(localRootFolder.Length + 1);
                            string id = null;
                            try
                            {
                                id = session.GetObjectByPath(remotePath).Id;
                            }
                            catch (DotCMIS.Exceptions.CmisObjectNotFoundException e)
                            {
                                Logger.Info(String.Format("File Not Found: \"{0}\"", remotePath), e);
                            }
                            catch (DotCMIS.Exceptions.CmisPermissionDeniedException e)
                            {
                                Logger.Info(String.Format("PermissionDenied: \"{0}\"", remotePath), e);
                            }

                            var parameters = new Dictionary<string, object>();
                            parameters.Add("@id", id);
                            parameters.Add("@localPath", localPath);
                            parameters.Add("@path", legacyPath);
                            ExecuteSQLAction(connection, "UPDATE folders SET id = @id, localPath = @localPath WHERE path = @path;", parameters);
                        }
                    }

                    {
                        // Replace repository path prefix.
                        // Before: C:\Users\myuser\CmisSync
                        // After:  C:\Users\myuser\CmisSync\myfolder

                        // Read existing prefix.

                        string newPrefix = syncFolder.LocalPath;

                        var parameters = new Dictionary<string, object>();
                        parameters.Add("prefix", newPrefix);
                        ExecuteSQLAction(connection, "INSERT OR REPLACE INTO general (key, value) VALUES (\"PathPrefix\", @prefix)", parameters);
                    }
                }
            }
            catch(Exception e)
            {
                Logger.Info("Failed to fills object id.", e);
                throw;
            }

            Utils.NotifyUser("CmisSync has finished upgrading its own local data for this folder.");
        }
    }
}