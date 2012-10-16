using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using System.IO;

namespace SparkleLib.Cmis
{
    public class CmisDatabase
    {
        private string databaseFileName;

        // SQLite connection to the underlying database.
        private SQLiteConnection sqliteConnection;

        public CmisDatabase(string databaseFileName)
        {
            this.databaseFileName = databaseFileName;
        }

        public void RecreateDatabaseIfNeeded()
        {
            if (!File.Exists(databaseFileName))
                CreateDatabase();
        }

        /**
         * Create database and tables, if it does not exist yet.
         */
        public void CreateDatabase()
        {
            ConnectToSqliteIfNeeded();
            SQLiteCommand command = new SQLiteCommand(sqliteConnection);
            command.CommandText =
                  "CREATE TABLE files ("
                + "    path TEXT PRIMARY KEY,"
                + "    serverSideModificationDate DATE,"
                + "    checksum TEXT);" // Checksum of both data and metadata
                + "CREATE TABLE folders ("
                + "    path TEXT PRIMARY KEY,"
                + "    serverSideModificationDate DATE);";
            SQLiteDataReader reader = command.ExecuteReader();
            reader.Close();
        }

        public void ConnectToSqliteIfNeeded()
        {
            if (sqliteConnection == null)
            {
                sqliteConnection = new SQLiteConnection("Data Source=" + databaseFileName);
                sqliteConnection.Open();
            }
        }

        public void AddFolder(string path, DateTime? serverSideModificationDate)
        {
            try
            {
                SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                command.CommandText =
                    "INSERT OR REPLACE INTO folders (path, serverSideModificationDate)"
                    + " VALUES (@path, @serverSideModificationDate)";
                command.Parameters.AddWithValue("path", path);
                command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                command.ExecuteReader();
            }
            catch (SQLiteException e)
            {
                SparkleLogger.LogInfo("CmisDatabase", e.Message);
            }
        }

        public void RemoveFile(string path)
        {
            try
            {
                SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                command.CommandText =
                    "DELETE FROM files WHERE path=@filePath";
                command.Parameters.AddWithValue("filePath", path);
                command.ExecuteReader();
            }
            catch (SQLiteException e)
            {
                SparkleLogger.LogInfo("CmisDatabase", e.Message);
            }
        }

        public void RemoveFolder(string path)
        {
            // Remove folder itself
            try
            {
                SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                command.CommandText =
                    "DELETE FROM folders WHERE path='" + path + "'";
                command.ExecuteReader();
            }
            catch (SQLiteException e)
            {
                SparkleLogger.LogInfo("CmisDatabase", e.Message);
            }

            // Remove all folders under this folder
            try
            {
                SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                command.CommandText =
                    "DELETE FROM folders WHERE path LIKE '" + path + "/%'";
                command.ExecuteReader();
            }
            catch (SQLiteException e)
            {
                SparkleLogger.LogInfo("CmisDatabase", e.Message);
            }

            // Remove all files under this folder
            try
            {
                SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                command.CommandText =
                    "DELETE FROM files WHERE path LIKE '" + path + "/%'";
                command.ExecuteReader();
            }
            catch (SQLiteException e)
            {
                SparkleLogger.LogInfo("CmisDatabase", e.Message);
            }
        }

        public void AddFile(string path, DateTime? serverSideModificationDate)
        {
            try
            {
                SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                command.CommandText =
                    "INSERT OR REPLACE INTO files (path, serverSideModificationDate)"
                    + " VALUES (@filePath, @serverSideModificationDate)";
                command.Parameters.AddWithValue("filePath", path);
                command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                command.ExecuteReader();
            }
            catch (SQLiteException e)
            {
                SparkleLogger.LogInfo("CmisDatabase", e.Message);
            }
        }

        public DateTime? GetServerSideModificationDate(string path)
        {
            try
            {
                SQLiteCommand command = new SQLiteCommand(sqliteConnection);
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

        public void SetFileServerSideModificationDate(string path, DateTime? serverSideModificationDate)
        {
            try
            {
                SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                command.CommandText =
                    "UPDATE files"
                    + " SET serverSideModificationDate=@serverSideModificationDate"
                    + " WHERE path=@path";
                command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                command.Parameters.AddWithValue("path", path);
                command.ExecuteReader();
            }
            catch (SQLiteException e)
            {
                SparkleLogger.LogInfo("CmisDatabase", e.Message);
            }
        }

        public bool ContainsFile(string path)
        {
            SQLiteCommand command = new SQLiteCommand(sqliteConnection);
            command.CommandText =
                "SELECT serverSideModificationDate FROM files WHERE path=@path";
            command.Parameters.AddWithValue("path", path);
            object obj = command.ExecuteScalar();
            return obj != null;
        }

        public bool ContainsFolder(string path)
        {
            SQLiteCommand command = new SQLiteCommand(sqliteConnection);
            command.CommandText =
                "SELECT serverSideModificationDate FROM folders WHERE path=@path";
            command.Parameters.AddWithValue("path", path);
            object obj = command.ExecuteScalar();
            return obj != null;
        }
    }
}
