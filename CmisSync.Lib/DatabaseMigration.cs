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
        public static void Migrate(string filePath, int currentDatabaseVersion)
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

                if (dbVersion >= currentDatabaseVersion)
                {
                    return;     // migration is not needed
                }

                if (dbVersion == 0 && currentDatabaseVersion == 1)
                {
                    MigrateToVersion1(filePath, connection);
                }
                else
                {
                    throw new NotSupportedException(String.Format("Unexpected database version: {0}.", dbVersion)); 
                }

                Logger.Debug("Database migration successful");
            }
            catch (Exception e)
            {
                Logger.Error("Error migrating database: " + e.Message, e);
                throw;
            }
        }


        private static void MigrateToVersion1(string filePath, SQLiteConnection connection)
        {
            var filesTableColumns = GetColumns(GetConnection(filePath, connection), "files");
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

            var foldersTableColumns = GetColumns(connection, "files");
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
            string sql = "SELECT value FROM general WHERE key=\"DatabaseVersion\";";
            object obj = ExecuteSQLFunction(connection, sql, null);
            if (obj != null)
            {
                return int.Parse((string)obj);
            }
            return 0;
        }

        /// <summary>
        /// Helper method to get columns
        /// </summary>
        /// <param name="connection">database connection</param>
        /// <param name="table">table name</param>
        /// <returns>array of column name</returns>
        private static string[] GetColumns(SQLiteConnection connection, string table)
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

    }
}

