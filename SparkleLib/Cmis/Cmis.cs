using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using DotCMIS.Client;
using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Exceptions;
using DotCMIS.Enums;
using System.ComponentModel;
using System.Collections;

namespace SparkleLib.Cmis
{
    class Cmis
    {
        private bool BIDIRECTIONAL = false;

        // At which degree the repository supports Change Logs.
        // See http://docs.oasis-open.org/cmis/CMIS/v1.0/os/cmis-spec-v1.0.html#_Toc243905424
        // Possible values: none, objectidsonly, properties, all
        bool ChangeLogCapability;

        // Session to the CMIS repository.
        ISession session;

        // Canonical name for this checkout.
        // config.xml: name
        string canonical_name;

        // Local folder where the changes are synchronized to.
        string localRootFolder;

        // Path of the root in the remote repository.
        string remoteFolderPath;

        // State. true if syncing is being performed right now.
        // TODO use is_syncing variable in parent
        bool syncing = true;

        // Parameters for CMIS requests.
        Dictionary<string, string> cmisParameters;

        private string databaseFilename;

        // SQLite connection to store modification dates.
        SQLiteConnection sqliteConnection;

        /**
         * Called by SparkleFetcher (when a new CMIS folder is first added)
         */
        public Cmis(string canonical_name, string localPath, string remoteFolderPath, string url, string user, string password, string repositoryId)
        {
            // Set local root folder.
            this.localRootFolder = Path.Combine(SparkleFolder.ROOT_FOLDER, canonical_name);

            databaseFilename = canonical_name + ".s3db";

            // Get path on remote repository.
            this.remoteFolderPath = /*"/" +*/ remoteFolderPath;

            cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url;
            cmisParameters[SessionParameter.User] = user;
            cmisParameters[SessionParameter.Password] = password;
            cmisParameters[SessionParameter.RepositoryId] = repositoryId;

            syncing = false;
        }

        /**
         * Called by SparkleRepo (at every launch of CmisSync)
         */
        public Cmis(string localPath, SparkleConfig config)
        {
            // Set local root folder.
            this.localRootFolder = Path.Combine(SparkleFolder.ROOT_FOLDER, localPath);

            databaseFilename = localRootFolder + ".cmissync";

            // Get path on remote repository.
            remoteFolderPath = config.GetFolderOptionalAttribute(Path.GetFileName(localRootFolder), "remoteFolder");

            cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = config.GetUrlForFolder(Path.GetFileName(localRootFolder));
            cmisParameters[SessionParameter.User] = config.GetFolderOptionalAttribute(Path.GetFileName(localRootFolder), "user");
            cmisParameters[SessionParameter.Password] = config.GetFolderOptionalAttribute(Path.GetFileName(localRootFolder), "password");
            cmisParameters[SessionParameter.RepositoryId] = config.GetFolderOptionalAttribute(Path.GetFileName(localRootFolder), "repository");

            syncing = false;
        }

        public void Connect()
        {
            // Create session factory.
            SessionFactory factory = SessionFactory.NewInstance();
            try
            {
                // Get the list of repositories. There should be only one, because we specified RepositoryId.
                IList<IRepository> repositories = factory.GetRepositories(cmisParameters);
                if (repositories.Count != 1)
                    SparkleLogger.LogInfo("Sync", "Unexpected number of matching repositories: " + repositories.Count);
                // Get the repository.
                IRepository repository = factory.GetRepositories(cmisParameters)[0];
                // Detect whether the repository has the ChangeLog capability.
                ChangeLogCapability = repository.Capabilities.ChangesCapability == CapabilityChanges.All
                    || repository.Capabilities.ChangesCapability == CapabilityChanges.ObjectIdsOnly;
                session = repository.CreateSession();
                SparkleLogger.LogInfo("Sync", "Created CMIS session: " + session.ToString());
            }
            catch (CmisRuntimeException e)
            {
                SparkleLogger.LogInfo("Sync", "Exception: " + e.Message + ", error content: " + e.ErrorContent);
            }
        }


        public void RecreateDatabaseIfNeeded()
        {
            if (!File.Exists(databaseFilename))
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
                + "    serverSideModificationDate DATE);"
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
                sqliteConnection = new SQLiteConnection("Data Source=" + databaseFilename);
                sqliteConnection.Open();
            }
        }

        public void SyncInBackground()
        {
            if (syncing)
                return;
            syncing = true;

            //SparkleLogger.LogInfo("Sync", "Syncing " + RemoteUrl + " " + local_config.GetFolderOptionalAttribute("repository", LocalPath));

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += new DoWorkEventHandler(
                delegate(Object o, DoWorkEventArgs args)
                {
                    SparkleLogger.LogInfo("Sync", "Launching sync in background, so that the UI stays available.");
                    try
                    {
                        Sync();
                    }
                    catch (CmisBaseException e)
                    {
                        SparkleLogger.LogInfo("Sync", "CMIS exception while syncing:" + e.Message);
                        SparkleLogger.LogInfo("Sync", e.StackTrace);
                        SparkleLogger.LogInfo("Sync", e.ErrorContent);
                    }
                    //catch (Exception e)
                    //{
                    //    SparkleLogger.LogInfo("Sync", "Exception while syncing:" + e.Message);
                    //}
                }
            );
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                delegate(object o, RunWorkerCompletedEventArgs args)
                {
                    syncing = false;
                }
            );
            bw.RunWorkerAsync();
        }

        public void Sync()
        {
            RecreateDatabaseIfNeeded();
            ConnectToSqliteIfNeeded();

            // If not connected, connect.
            if (session == null)
                Connect();

            // Get the root folder.
            //IFolder remoteRootFolder = session.GetRootFolder();
            
            IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath/*.Substring(1,remoteFolderPath.Length - 1)*/); // Works with FileNet
            // IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath.Substring(1,remoteFolderPath.Length - 1)); // Remove extra slash // Works with Alfresco

            if (ChangeLogCapability)
            {
                // Get last change log token from server.
                // TODO
                if (true /* TODO if no locally saved CMIS change log token */)
                {
                    RecursiveFolderCopy(remoteFolder, localRootFolder);
                }
                else
                {
                    // Check which files/folders have changed.
                    // TODO session.GetContentChanges(changeLogToken, includeProperties, maxNumItems);

                    // Download/delete files/folders accordingly.
                    // TODO
                }
                // Save change log token locally.
                // TODO
            }
            else
            {
                // No ChangeLog capability, so we have to crawl remote and local folders.
                CrawlSync(remoteFolder, localRootFolder);
            }
        }

        private void RecursiveFolderCopy(IFolder remoteFolder, string localFolder)
        {
            // List all children.
            foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
            {
                if (cmisObject is DotCMIS.Client.Impl.Folder)
                {
                    IFolder remoteSubFolder = (IFolder)cmisObject;
                    string localSubFolder = localFolder + Path.DirectorySeparatorChar + cmisObject.Name;

                    // Create local folder.
                    Directory.CreateDirectory(localSubFolder);

                    // Create database entry for this folder.
                    DateTime? serverSideModificationDate = remoteFolder.LastModificationDate;
                    try
                    {
                        SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                        command.CommandText =
                            "INSERT OR REPLACE INTO folders (path, serverSideModificationDate)"
                            + " VALUES (@path, @serverSideModificationDate)";
                        command.Parameters.AddWithValue("path", localSubFolder);
                        command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                        command.ExecuteReader();
                    }
                    catch (SQLiteException e)
                    {
                        SparkleLogger.LogInfo("Sync", "Error writing folder to database. " + e.Message);
                    }

                    // Recurse into folder.
                    RecursiveFolderCopy(remoteSubFolder, localSubFolder);
                }
                else
                {
                    // It is a file, just download it.
                    DownloadFile((IDocument)cmisObject, localFolder);
                }
            }
        }

        /**
         * Synchronize by checking all folders/files one-by-one.
         * 
         * for all remote folders:
         *     if exists locally:
         *       recurse
         *     else
         *       create local folder
         * for all remote files:
         *     if exists locally:
         *       if remote is more recent than local:
         *         download
         *       else
         *         upload                         // if BIDIRECTIONAL
         *     else:
         *       download
         * for all local files:
         *   if not present remotely:
         *     if in database:
         *       delete
         *     else:
         *       upload                           // if BIDIRECTIONAL
         * for all local folders:
         *   if not present remotely:
         *     if in database:
         *       delete recursively from local
         *     else:
         *       upload recursively               // if BIDIRECTIONAL
         */
        private void CrawlSync(IFolder remoteFolder, string localFolder)
        {
            //String id = new Random().Next(0, 1000000).ToString();

            // Sync down.

            // Lists of files/folders, to delete those that have been removed on the server.
            IList remoteFiles = new ArrayList();
            IList remoteFolders = new ArrayList();

            // List all children.
            foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
            {
                if (cmisObject is DotCMIS.Client.Impl.Folder)
                {
                    IFolder remoteSubFolder = (IFolder)cmisObject;
                    remoteFolders.Add(remoteSubFolder.Name);
                    string localSubFolder = localFolder + Path.DirectorySeparatorChar + remoteSubFolder.Name;

                    // Check whether local folder exists.
                    if (Directory.Exists(localSubFolder))
                    {
                        // Recurse into folder.
                        CrawlSync(remoteSubFolder, localSubFolder);
                    }
                    else
                    {
                        // If there was previously a file with this name, delete it.
                        // TODO warn if local changes in the file.
                        if (File.Exists(localSubFolder))
                        {
                            File.Delete(localSubFolder);
                        }

                        // Create local folder.
                        Directory.CreateDirectory(localSubFolder);

                        // Create database entry for this folder.
                        DateTime? serverSideModificationDate = remoteFolder.LastModificationDate;
                        try
                        {
                            SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                            command.CommandText =
                                "INSERT OR REPLACE INTO folders (path, serverSideModificationDate)"
                                + " VALUES (@path, @serverSideModificationDate)";
                            command.Parameters.AddWithValue("path", localSubFolder);
                            command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                            command.ExecuteReader();
                        }
                        catch (SQLiteException e)
                        {
                            SparkleLogger.LogInfo("Sync", "Error writing folder to database. " + e.Message);
                        }

                        // Recursive copy of the whole folder.
                        RecursiveFolderCopy(remoteSubFolder, localSubFolder);
                    }
                }
                else
                {
                    // It is a file, check whether it exists and has the same modifica download it.
                    IDocument remoteDocument = (IDocument)cmisObject;
                    remoteFiles.Add(remoteDocument.Name);

                    string remoteDocumentFileName = remoteDocument.ContentStreamFileName;
                    // If this file does not have a filename, ignore it.
                    // It sometimes happen on IBM P8 CMIS server, not sure why.
                    if (remoteDocumentFileName == null)
                    {
                        SparkleLogger.LogInfo("Sync", "Skipping download of file with null content stream in " + localFolder);
                        continue;
                    }

                    string filePath = localFolder + Path.DirectorySeparatorChar + remoteDocumentFileName;


                    if (File.Exists(filePath))
                    {
                        // Check modification date stored in database and download if remote modification date if different.
                        DateTime? serverSideModificationDate = remoteDocument.LastModificationDate;
                        try
                        {
                            SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                            command.CommandText =
                                "SELECT serverSideModificationDate FROM files WHERE path=@filePath";
                            command.Parameters.AddWithValue("filePath", filePath);
                            object obj = command.ExecuteScalar();
                            if (obj == null)
                            {
                                SparkleLogger.LogInfo("Sync", "Downloading file absent from database: " + remoteDocumentFileName);
                                DownloadFile(remoteDocument, localFolder);
                            }
                            else
                            {
                                DateTime clientSideModificationDate = (DateTime)obj;
                                // If the file has been modified since last time we downloaded it, then download again.
                                if (serverSideModificationDate > clientSideModificationDate)
                                {
                                    SparkleLogger.LogInfo("Sync", "Downloading modified file: " + remoteDocumentFileName);
                                    DownloadFile(remoteDocument, localFolder);
                                }
                            }
                        }
                        catch (SQLiteException e)
                        {
                            SparkleLogger.LogInfo("Sync", e.Message);
                        }
                    }
                    else
                    {
                        SparkleLogger.LogInfo("Sync", "Downloading new file: " + remoteDocumentFileName);
                        DownloadFile(remoteDocument, localFolder);
                    }
                }
            }

            // Delete files that have been removed on the server.
            foreach (string filePath in Directory.GetFiles(localFolder, "*.*"))
            {
                string fileName = Path.GetFileName(filePath);
                if (!remoteFiles.Contains(fileName))
                {
                    // This local file is not on the CMIS server now, so
                    // check whether it used to exist on server or not.
                    SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                    command.CommandText =
                        "SELECT serverSideModificationDate FROM files WHERE path=@filePath";
                    command.Parameters.AddWithValue("filePath", filePath);
                    object obj = command.ExecuteScalar();
                    if (obj == null)
                    {
                        if (BIDIRECTIONAL)
                        {
                            // New file, sync up.
                            // TODO
                        }
                    }
                    else
                    {
                        // File has been deleted on server, so delete it locally.
                        SparkleLogger.LogInfo("Sync", "Removing remotely deleted file: " + filePath);
                        File.Delete(filePath);

                        // Remove from database.
                        // TODO
                    }
                }
            }

            // Delete folders that have been removed on the server.
            foreach (string folderPath in Directory.GetDirectories(localFolder, "*.*"))
            {
                string folderName = Path.GetFileName(folderPath);
                if (!remoteFolders.Contains(folderName))
                {
                    // This local folder is not on the CMIS server now, so
                    // check whether it used to exist on server or not.
                    SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                    command.CommandText =
                        "SELECT serverSideModificationDate FROM folders WHERE path=@folderPath";
                    command.Parameters.AddWithValue("folderPath", folderPath);
                    object obj = command.ExecuteScalar();
                    if (obj == null)
                    {
                        // New folder, sync up.
                        // TODO
                    }
                    else
                    {
                        // File has been deleted on server, delete it locally too.
                        SparkleLogger.LogInfo("Sync", "Removing remotely deleted folder: " + folderPath);
                        Directory.Delete(folderPath, true);
                    }
                }
            }
        }


        private void DownloadFile(IDocument remoteDocument, string localFolder)
        {
            DotCMIS.Data.IContentStream contentStream = remoteDocument.GetContentStream();

            // If this file does not have a content stream, ignore it.
            // Even 0 bytes files have a contentStream.
            // null contentStream sometimes happen on IBM P8 CMIS server, not sure why.
            if (contentStream == null)
            {
                SparkleLogger.LogInfo("Sync", "Skipping download of file with null content stream: " + remoteDocument.ContentStreamFileName);
                return;
            }

            // Download.
            string filePath = localFolder + Path.DirectorySeparatorChar + contentStream.FileName;

            // If there was previously a directory with this name, delete it.
            // TODO warn if local changes inside the folder.
            if (Directory.Exists(filePath))
            {
                Directory.Delete(filePath);
            }

            SparkleLogger.LogInfo("Sync", /*id +*/ "Downloading " + filePath);
            Stream file = File.OpenWrite(filePath);
            byte[] buffer = new byte[8 * 1024];
            int len;
            while ((len = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                file.Write(buffer, 0, len);
            }
            file.Close();
            contentStream.Stream.Close();
            SparkleLogger.LogInfo("Sync", "Downloaded");

            // Create database entry for this file.
            DateTime? serverSideModificationDate = remoteDocument.LastModificationDate;
            try
            {
                SQLiteCommand command = new SQLiteCommand(sqliteConnection);
                command.CommandText =
                    "INSERT OR REPLACE INTO files (path, serverSideModificationDate)"
                    + " VALUES (@filePath, @serverSideModificationDate)";
                command.Parameters.AddWithValue("filePath", filePath);
                command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                command.ExecuteReader();
            }
            catch (SQLiteException e)
            {
                SparkleLogger.LogInfo("Sync", "Error writing file to database. " + e.Message);
            }
        }
    }
}