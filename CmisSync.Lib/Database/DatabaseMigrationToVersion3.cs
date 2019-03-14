using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if __MonoCS__
using Mono.Data.Sqlite;
#else
using System.Data.SQLite;
#endif

using log4net;
using CmisSync.Auth;
using CmisSync.Lib.Config;
using CmisSync.Lib.Utilities.PathConverter;
using CmisSync.Lib.UserNotificationListener;

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
    /// Migrate from database version 0,2 to version 3.
    /// </summary>
    public class DatabaseMigrationToVersion3 : DatabaseMigrationBase
    {
        /// <summary>
        /// Log.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(DatabaseMigrationToVersion3));

        /// <summary>
        /// Migrate from database version 0,2 to version 3.
        /// </summary>
        /// <param name="syncFolder">File path.</param>
        /// <param name="connection">Connection.</param>
        /// <param name="currentVersion">Current database schema version.</param>
        public void Migrate(Config.CmisSyncConfig.SyncConfig.Folder syncFolder, SQLiteConnection connection, int currentVersion)
        {
            // Add columns and other database schema manipulation.
            MigrateSchema(syncFolder, connection);

            // Fill the data which is missing due to new columns in the database.
            FillMissingData(syncFolder, connection);

            // If everything has succeded, upgrade database version number.
            SetDatabaseVersion(connection, currentVersion);
        }

        /// <summary>
        /// Add columns and other database schema manipulation.
        /// </summary>
        /// <param name="syncFolder">Folder name.</param>
        /// <param name="connection"></param>
        public static void MigrateSchema(Config.CmisSyncConfig.SyncConfig.Folder syncFolder, SQLiteConnection connection)
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
        }


        /// <summary>
        /// Fill the data which is missing due to new columns in the database.
        /// </summary>
        public static void FillMissingData(Config.CmisSyncConfig.SyncConfig.Folder syncFolder, SQLiteConnection connection)
        {
            UserNotificationListenerUtil.NotifyUser("CmisSync needs to upgrade its own local data for folder \"" + syncFolder.RepositoryId +
                "\".\nPlease stay on the network during that time, sorry for the inconvenience." +
                "\nIt can take up to HOURS if you have many files, thank you for your patience." +
                "\nA notification will pop up when it is done.");

            var session = Auth.Authentication.GetCmisSession(
                              ((Uri)syncFolder.RemoteUrl).ToString(),
                              syncFolder.UserName,
                              Crypto.Deobfuscate(syncFolder.ObfuscatedPassword),
                              syncFolder.RepositoryId);

            var filters = new HashSet<string>();
            filters.Add("cmis:objectId");
            string remoteRootFolder = syncFolder.RemotePath;
            string localRootFolder = syncFolder.LocalPath.Substring(ConfigManager.CurrentConfig.FoldersPath.Length + 1);

            try
            {
                using (var command = new SQLiteCommand(connection))
                {
                    // Fill missing columns of all files.
                    command.CommandText = "SELECT path FROM files WHERE id IS NULL or localPath IS NULL;";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Example: "old-db-1.0.13/テスト・テスト/テスト用ファイル.pptx"
                            string legacyPath = reader["path"].ToString();

                            // Example:  テスト・テスト/テスト用ファイル.pptx
                            string remoteRelativePath = legacyPath.Substring(localRootFolder.Length + 1);

                            // Example: /Sites/cmissync/documentLibrary/tests/テスト・テスト/テスト用ファイル.pptx
                            string remotePath = remoteRootFolder + "/" + remoteRelativePath;

                            // Example: テスト・テスト/テスト用ファイル.pptx
                            string localPath = PathRepresentationConverterUtil.RemoteToLocal(legacyPath.Substring(localRootFolder.Length + 1));

                            string id = null;
                            try
                            {
                                id = session.GetObjectByPath(remotePath, true).Id;
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
                            parameters.Add("@remotePath", remoteRelativePath);
                            parameters.Add("@localPath", localPath);
                            parameters.Add("@path", legacyPath);
                            ExecuteSQLAction(connection, "UPDATE files SET id = @id, path = @remotePath, localPath = @localPath WHERE path = @path;", parameters);
                        }
                    }

                    // Fill missing columns of all folders.
                    command.CommandText = "SELECT path FROM folders WHERE id IS NULL or localPath IS NULL;";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string legacyPath = reader["path"].ToString();
                            string remoteRelativePath = legacyPath.Substring(localRootFolder.Length + 1);
                            string remotePath = remoteRootFolder + "/" + remoteRelativePath;
                            string localPath = PathRepresentationConverterUtil.RemoteToLocal(legacyPath.Substring(localRootFolder.Length + 1));
                            string id = null;
                            try
                            {
                                id = session.GetObjectByPath(remotePath, true).Id;
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
                            parameters.Add("@remotePath", remoteRelativePath);
                            parameters.Add("@localPath", localPath);
                            parameters.Add("@path", legacyPath);
                            ExecuteSQLAction(connection, "UPDATE folders SET id = @id, path = @remotePath, localPath = @localPath WHERE path = @path;", parameters);
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
            catch (Exception e)
            {
                Logger.Info("Failed to migrate \"" + syncFolder.RepositoryId + "\".", e);
                UserNotificationListenerUtil.NotifyUser("Failure while migrating folder \"" + syncFolder.RepositoryId + "\".");
                throw;
            }

            UserNotificationListenerUtil.NotifyUser("CmisSync has finished upgrading its own local data for folder \"" + syncFolder.RepositoryId + "\".");
        }
    }
}
