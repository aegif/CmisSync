using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if __MonoCS__
using Mono.Data.Sqlite;
#else
using System.Data.SQLite;
#endif

using Newtonsoft.Json;
using log4net;

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
    /// Base class for classes that migrate CmisSync databases from a version to another.
    /// </summary>
    public class DatabaseMigrationBase
    {
        /// <summary>
        /// Log.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(DatabaseMigration));


        /// <summary>
        /// Gets the Database connection.
        /// </summary>
        /// <returns>Database connection.</returns>
        /// <param name="filePath">File path.</param>
        protected static SQLiteConnection GetConnection(string filePath)
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
        protected static SQLiteConnection GetConnection(string filePath, SQLiteConnection connection)
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
        protected static int GetDatabaseVersion(SQLiteConnection connection)
        {
            var objUserVersion = ExecuteSQLFunction(connection, "PRAGMA user_version;", null);
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
        protected static void SetDatabaseVersion(SQLiteConnection connection, int version)
        {
            string command = "PRAGMA user_version=" + version.ToString();
            ExecuteSQLAction(connection, command, null);
        }



        /// <summary>
        /// Helper method to get column Names
        /// </summary>
        /// <param name="connection">database connection</param>
        /// <param name="table">table name</param>
        /// <returns>array of column name</returns>
        protected static string[] GetColumnNames(SQLiteConnection connection, string table)
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
                catch (SQLiteException e)
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
        protected static void ExecuteSQLAction(SQLiteConnection connection, string text, Dictionary<string, object> parameters)
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
        protected static object ExecuteSQLFunction(SQLiteConnection connection, string text, Dictionary<string, object> parameters)
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
        protected static void ComposeSQLCommand(SQLiteCommand command, string text, Dictionary<string, object> parameters)
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
