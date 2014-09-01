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
            var syncFolder = ConfigManager.CurrentConfig.Folder.Find((f) => f.GetRepoInfo().CmisDatabase == dbFile);

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
                            new DatabaseMigrationToVersion2().Migrate(syncFolder, connection, currentDbVersion);
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