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

    public class DatabaseMigration
    {
        /// <summary>
        /// Log.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Database));

        public DatabaseMigration()
        {

        }

        public void Migrate(string filePath)
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
                        localPath TEXT, /* Local path is sometimes different due to local filesystem constraints */
                        id TEXT,
                        serverSideModificationDate DATE,
                        metadata TEXT,
                        checksum TEXT);   /* Checksum of both data and metadata */
                    CREATE TABLE IF NOT EXISTS folders (
                        path TEXT PRIMARY KEY,
                        localPath TEXT, /* Local path is sometimes different due to local filesystem constraints */
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
                    Logger.Info("Database created");
                }

                bool oldVersion = false;
                var filesColumns = GetColumns("files");
                if (!filesColumns.Contains("localPath"))
                {
                    oldVersion = true;
                    ExecuteSQLAction("ALTER TABLE files ADD COLUMN localPath TEXT;", null);
                }
                if (!filesColumns.Contains("id"))
                {
                    oldVersion = true;
                    ExecuteSQLAction("ALTER TABLE files ADD COLUMN id TEXT;", null);
                }

                var foldersColumns = GetColumns("folders");
                if (!foldersColumns.Contains("localPath"))
                {
                    oldVersion = true;
                    ExecuteSQLAction("ALTER TABLE folders ADD COLUMN localPath TEXT;", null);
                }
                if (!foldersColumns.Contains("id"))
                {
                    oldVersion = true;
                    ExecuteSQLAction("ALTER TABLE folders ADD COLUMN id TEXT;", null);
                }

                if (createDatabase || oldVersion)
                {
                    command = "INSERT OR IGNORE INTO general (key, value) VALUES (\"PathPrefix\", @prefix);";
                    Dictionary<string, object> parameters = new Dictionary<string, object>();
                    parameters.Add("prefix", ConfigManager.CurrentConfig.FoldersPath);
                    ExecuteSQLAction(command, parameters);
                }
                Logger.Debug("Database migration successful");
            }
            catch (Exception e)
            {
                Logger.Error("Error creating database: " + e.Message, e);
                throw;
            }
        }
    }
}

