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
using DotCMIS.Data.Impl;

namespace SparkleLib.Cmis
{
    /**
     * Synchronization with a particular CMIS folder.
     */
    public class Cmis
    {
        /**
         * Whether sync is bidirectional or only from server to client.
         * TODO make it a CMIS folder - specific setting
         */
        private bool BIDIRECTIONAL = true;

        /**
         * At which degree the repository supports Change Logs.
         * See http://docs.oasis-open.org/cmis/CMIS/v1.0/os/cmis-spec-v1.0.html#_Toc243905424
         * Possible values: none, objectidsonly, properties, all
         */
        bool ChangeLogCapability;

        /**
         * Session to the CMIS repository.
         */
        ISession session;

        /**
         * Local folder where the changes are synchronized to.
         * Example: "C:\CmisSync"
         */
        string localRootFolder;

        /**
         * Path of the root in the remote repository.
         * Example: "/User Homes/nicolas.raoul/demos"
         */
        string remoteFolderPath;

        /**
         * Syncing lock.
         * true if syncing is being performed right now.
         * TODO use is_syncing variable in parent
         */
        bool syncing = true;

        /**
         * Parameters to use for all CMIS requests.
         */
        Dictionary<string, string> cmisParameters;

        /**
         * Database to cache remote information from the CMIS server.
         */
        CmisDatabase database;


        /**
         * Constructor for SparkleFetcher (when a new CMIS folder is first added)
         */
        public Cmis(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Set local root folder.
            this.localRootFolder = Path.Combine(SparkleFolder.ROOT_FOLDER, canonical_name);

            database = new CmisDatabase(localRootFolder);

            // Get path on remote repository.
            this.remoteFolderPath = remoteFolderPath;

            cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url;
            cmisParameters[SessionParameter.User] = user;
            cmisParameters[SessionParameter.Password] = password;
            cmisParameters[SessionParameter.RepositoryId] = repositoryId;

            syncing = false;
        }


        /**
         * Constructor for SparkleRepo (at every launch of CmisSync)
         */
        public Cmis(string localPath, SparkleConfig config)
        {
            // Set local root folder.
            this.localRootFolder = Path.Combine(SparkleFolder.ROOT_FOLDER, localPath);

            database = new CmisDatabase(localRootFolder);

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


        /**
         * Connect to the CMIS repository.
         */
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


        /**
         * Sync in the background.
         */
        public void SyncInBackground()
        {
            if (syncing)
                return;
            syncing = true;

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += new DoWorkEventHandler(
                delegate(Object o, DoWorkEventArgs args)
                {
                    SparkleLogger.LogInfo("Sync", "Launching sync in background, so that the UI stays available.");
                    //try
                    //{
                        Sync();
                    /*}
                    catch (CmisBaseException e)
                    {
                        SparkleLogger.LogInfo("Sync", "CMIS exception while syncing:" + e.Message);
                        SparkleLogger.LogInfo("Sync", e.StackTrace);
                        SparkleLogger.LogInfo("Sync", e.ErrorContent);
                    }*/
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


        /**
         * Synchronize between CMIS folder and local folder.
         */
        public void Sync()
        {
            // If not connected, connect.
            if (session == null)
                Connect();

            IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath);

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


        /**
         * Download all content from a CMIS folder.
         */
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
                    database.AddFolder(localSubFolder, remoteFolder.LastModificationDate);

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
         * This strategy is used if the CMIS server does not support the ChangeLog feature.
         * 
         * for all remote folders:
         *     if exists locally:
         *       recurse
         *     else
         *       if in database                   // if BIDIRECTIONAL
         *         delete recursively
         *       else
         *         download recursively
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
         *   else:
         *     if has changed locally:
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
            // Lists of files/folders, to delete those that have been removed on the server.
            IList remoteFiles = new ArrayList();
            IList remoteSubfolders = new ArrayList();

            // Crawl remote children.
            crawlRemote(remoteFolder, localFolder, remoteFiles, remoteSubfolders);

            // Crawl local files.
            crawlLocalFiles(localFolder, remoteFolder, remoteFiles);

            // Crawl local folders.
            crawlLocalFolders(localFolder, remoteFolder, remoteSubfolders);
        }


        /** 
         * Crawl remote content, syncing down if needed.
         * Meanwhile, cache remoteFiles and remoteFolders, they are output parameters that are used in crawlLocalFiles/crawlLocalFolders
         */
        private void crawlRemote(IFolder remoteFolder, string localFolder, IList remoteFiles, IList remoteFolders)
        {
            foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
            {
                if (cmisObject is DotCMIS.Client.Impl.Folder)
                {
                    // It is a CMIS folder.
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

                        if (database.ContainsFolder(localSubFolder))
                        {
                            // If there was previously a folder with this name, it means that
                            // the user has deleted it voluntarily, so delete it from server too.
                            
                            // Delete the folder from the remote server.
                            remoteSubFolder.DeleteTree(true, null, true);

                            // Delete the folder from database.
                            database.RemoveFolder(localFolder);
                        }
                        else
                        {
                            // The folder has been recently created on server, so download it.

                            // Create local folder.
                            Directory.CreateDirectory(localSubFolder);

                            // Create database entry for this folder.
                            database.AddFolder(localSubFolder, remoteFolder.LastModificationDate);

                            // Recursive copy of the whole folder.
                            RecursiveFolderCopy(remoteSubFolder, localSubFolder);
                        }
                    }
                }
                else
                {
                    // It is a CMIS document.
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
                        DateTime? lastDatabaseUpdate = database.GetServerSideModificationDate(filePath);
                        if (lastDatabaseUpdate == null)
                        {
                            SparkleLogger.LogInfo("Sync", "Downloading file absent from database: " + remoteDocumentFileName);
                            DownloadFile(remoteDocument, localFolder);
                        }
                        else
                        {
                            // If the file has been modified since last time we downloaded it, then download again.
                            if (serverSideModificationDate > lastDatabaseUpdate)
                            {
                                SparkleLogger.LogInfo("Sync", "Downloading modified file: " + remoteDocumentFileName);
                                DownloadFile(remoteDocument, localFolder);
                            }

                            // Change modification date in database
                            database.SetFileServerSideModificationDate(filePath, serverSideModificationDate);
                        }
                    }
                    else
                    {
                        SparkleLogger.LogInfo("Sync", "Downloading new file: " + remoteDocumentFileName);
                        DownloadFile(remoteDocument, localFolder);
                    }
                }
            }
        }
        
        
        /**
         * Crawl local files in a given directory (not recursive).
         */
        private void crawlLocalFiles(string localFolder, IFolder remoteFolder, IList remoteFiles)
        {
            foreach (string filePath in Directory.GetFiles(localFolder, "*.*"))
            {
                string fileName = Path.GetFileName(filePath);
                if (!remoteFiles.Contains(fileName))
                {
                    // This local file is not on the CMIS server now, so
                    // check whether it used to exist on server or not.
                    if (database.ContainsFile(filePath))
                    {
                        // File has been deleted on server, so delete it locally.
                        SparkleLogger.LogInfo("Sync", "Removing remotely deleted file: " + filePath);
                        File.Delete(filePath);

                        // Delete file from database.
                        database.RemoveFile(filePath);
                    }
                    else
                    {
                        if (BIDIRECTIONAL)
                        {
                            // New file, sync up.
                            UploadFile(filePath, remoteFolder);
                        }
                    }
                }
                else
                {
                    // The file exists both on server and locally.
                    if(database.LocalFileHasChanged(filePath))
                    {
                        if (BIDIRECTIONAL)
                        {
                            // Upload new version of file content.
                            UpdateFile(filePath, remoteFolder);
                        }
                    }
                }
            }
        }

        /**
         * Crawl local folders in a given directory (not recursive).
         */
        private void crawlLocalFolders(string localFolder, IFolder remoteFolder, IList remoteFolders)
        {
            foreach (string folderPath in Directory.GetDirectories(localFolder, "*.*"))
            {
                string folderName = Path.GetFileName(folderPath);
                if (!remoteFolders.Contains(folderName))
                {
                    // This local folder is not on the CMIS server now, so
                    // check whether it used to exist on server or not.
                    if(database.ContainsFolder(folderPath))
                    {
                        // File has been deleted on server, delete it locally too.
                        SparkleLogger.LogInfo("Sync", "Removing remotely deleted folder: " + folderPath);
                        Directory.Delete(folderPath, true);

                        // Delete folder from database.
                        database.RemoveFolder(folderPath);
                    }
                    else
                    {
                        if (BIDIRECTIONAL)
                        {
                            // New folder, sync up.
                            Dictionary<string, object> properties = new Dictionary<string, object>();
                            properties.Add(PropertyIds.Name, folderName);
                            properties.Add(PropertyIds.ObjectTypeId, "cmis:folder");
                            IFolder folder = remoteFolder.CreateFolder(properties);

                            // Create database entry for this folder.
                            database.AddFolder(folderPath, folder.LastModificationDate);
                        }
                    }
                }
            }
        }


        /**
         * Download a single file from the CMIS server.
         */
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

            SparkleLogger.LogInfo("Sync", "Downloading " + filePath);
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
            database.AddFile(filePath, remoteDocument.LastModificationDate);
        }


        /**
         * Upload a single file to the CMIS server.
         */
        private void UploadFile(string filePath, IFolder remoteFolder)
        {
            // Prepare properties
            string fileName = Path.GetFileName(filePath);
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, fileName);
            properties.Add(PropertyIds.ObjectTypeId, "cmis:document");

            // Prepare content stream
            ContentStream contentStream = new ContentStream();
            contentStream.FileName = fileName;
            contentStream.MimeType = MimeType.GetMIMEType(fileName); // Should CmisSync try to guess?
            contentStream.Stream = File.OpenRead(filePath);

            // Upload
            IDocument remoteDocument = remoteFolder.CreateDocument(properties, contentStream, null);

            // Create database entry for this file.
            database.AddFile(filePath, remoteDocument.LastModificationDate);
        }

        /**
         * Upload new version of file content.
         */
        private void UpdateFile(string filePath, IFolder remoteFolder)
        {
            string fileName = Path.GetFileName(filePath);

            // Prepare content stream
            ContentStream contentStream = new ContentStream();
            contentStream.FileName = fileName;
            contentStream.MimeType = MimeType.GetMIMEType(fileName); // Should CmisSync try to guess?
            contentStream.Stream = File.OpenRead(filePath);

            IDocument document = null;
            bool found = false;
            foreach(ICmisObject obj in remoteFolder.GetChildren())
            {
                if (obj is IDocument)
                {
                    document = (IDocument)obj;
                    if (document.Name == fileName)
                    {
                        found = true;
                        break;
                    }
                }
            }

            // If not found, it means the document has been deleted, will be processed at the next sync cycle.
            if (!found)
            {
                return;
            }

            // Send content stream.
            IObjectId id = document.SetContentStream(contentStream, true, true);

            // Read new last modification date.
            // TODO document = (IDocument)id; // null. See DotCMIS 0.4 bug: CMIS-594
            //
            // Update timestamp in database.
            //database.SetFileServerSideModificationDate(filePath, document.LastModificationDate);
        }
    }
}