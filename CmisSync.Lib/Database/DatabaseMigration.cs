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
using CmisSync.Lib.Database;
using CmisSync.Lib.Config;

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
    /// Check a CmisSync database and if needed upgrade its version and schema.
    /// </summary>
    public class DatabaseMigration : DatabaseMigrationBase
    {
        /// <summary>
        /// Log.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(DatabaseMigration));


        /// <summary>
        /// Migrate a database file.
        /// </summary>
        /// <param name="dbFile">Database file.</param>
        public static void Migrate(string dbFile)
        {
            var syncFolder = ConfigManager.CurrentConfig.Folders.Find((f) => f.GetRepoInfo().CmisDatabase == dbFile);

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
                    // Check database version.
                    int dbVersion = GetDatabaseVersion(connection);

                    // Skip migration if up-to-date.
                    if (dbVersion >= currentDbVersion)
                    {
                        return;
                    }

                    // Migrate with various step according to the version.
                    Logger.DebugFormat("Current database schema must be updated from {0} to {0}", dbVersion, currentDbVersion);
                    switch (dbVersion)
                    {
                        case 0:
                            new DatabaseMigrationToVersion3().Migrate(syncFolder, connection, currentDbVersion);
                            break;
                        case 2: // Need to fill the localPath value.
                            new DatabaseMigrationToVersion3().Migrate(syncFolder, connection, currentDbVersion);
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
    }
}