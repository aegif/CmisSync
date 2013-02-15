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

using System.Net;

namespace SparkleLib.Cmis
{
    public enum RulesType { Folder, File };
    /**
     * Synchronization with a particular CMIS folder.
     */
    public class CmisDirectory
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
        private bool ChangeLogCapability;

        /**
         * Session to the CMIS repository.
         */
        private ISession session;

        /**
         * Local folder where the changes are synchronized to.
         * Example: "C:\CmisSync"
         */
        private string localRootFolder;

        /**
         * Path of the root in the remote repository.
         * Example: "/User Homes/nicolas.raoul/demos"
         */
        private string remoteFolderPath;

        /**
         * Syncing lock.
         * true if syncing is being performed right now.
         * TODO use is_syncing variable in parent
         */
        private bool syncing = true;

        /**
         * Parameters to use for all CMIS requests.
         */
        private Dictionary<string, string> cmisParameters;

        /**
         * Database to cache remote information from the CMIS server.
         */
        private CmisDatabase database;

        /**
         * Listener we inform about activity (used by spinner)
         */
        private ActivityListener activityListener;

        /**
         * Config 
         * */
        private SparkleConfig sconfig;

        // Why use a special constructor, add folder in config before syncing and use standard constructor instead
        /**
         * Constructor for SparkleFetcher (when a new CMIS folder is first added)
         * 
         */
        //public CmisDirectory(string canonical_name, string localPath, string remoteFolderPath,
        //    string url, string user, string password, string repositoryId,
        //    ActivityListener activityListener)
        //{
        //    this.activityListener = activityListener;
        //    this.remoteFolderPath = remoteFolderPath;

        //    // Set local root folder.
        //    this.localRootFolder = Path.Combine(SparkleFolder.ROOT_FOLDER, canonical_name);

        //    // Database is place in appdata of the users instead of sync folder (more secure)
        //    // database = new CmisDatabase(localRootFolder);
        //    string cmis_path = Path.Combine(config.ConfigPath, canonical_name + ".cmissync");
        //    database = new CmisDatabase(cmis_path);

        //    cmisParameters = new Dictionary<string, string>();
        //    cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
        //    cmisParameters[SessionParameter.AtomPubUrl] = url;
        //    cmisParameters[SessionParameter.User] = user;
        //    cmisParameters[SessionParameter.Password] = password;
        //    cmisParameters[SessionParameter.RepositoryId] = repositoryId;

        //    syncing = false;
        //}


        /**
         * Constructor for SparkleRepo (at every launch of CmisSync)
         */
        public CmisDirectory(string localPath, SparkleConfig config,
            ActivityListener activityListener)
        {
            this.activityListener = activityListener;
            this.sconfig = config;
            // Set local root folder
            String FolderName = Path.GetFileName(localPath);
            this.localRootFolder = Path.Combine(SparkleFolder.ROOT_FOLDER, FolderName);

            // Database is place in appdata of the users instead of sync folder (more secure)
            // database = new CmisDatabase(localRootFolder);
            string cmis_path = Path.Combine(config.ConfigPath, FolderName + ".cmissync");
            database = new CmisDatabase(cmis_path);

            // Get path on remote repository.
            remoteFolderPath = config.GetFolderOptionalAttribute(FolderName, "remoteFolder");

            cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = config.GetUrlForFolder(Path.GetFileName(localRootFolder));
            cmisParameters[SessionParameter.User] = config.GetFolderOptionalAttribute(Path.GetFileName(localRootFolder), "user");
            cmisParameters[SessionParameter.Password] = config.GetFolderOptionalAttribute(Path.GetFileName(localRootFolder), "password");
            cmisParameters[SessionParameter.RepositoryId] = config.GetFolderOptionalAttribute(Path.GetFileName(localRootFolder), "repository");

            cmisParameters[SessionParameter.ConnectTimeout] = "-1";

            syncing = false;

        }


        /**
         * Connect to the CMIS repository.
         */
        public void Connect()
        {
            do
            {
                try
                {
                    // Create session factory.
                    SessionFactory factory = SessionFactory.NewInstance();
                    session = factory.CreateSession(cmisParameters);

                    // Detect whether the repository has the ChangeLog capability.
                    ChangeLogCapability = session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All
                            || session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.ObjectIdsOnly;
                    SparkleLogger.LogInfo("Sync", "ChangeLog capability: " + ChangeLogCapability);
                    SparkleLogger.LogInfo("Sync", "Created CMIS session: " + session.ToString());
                }
                catch (CmisRuntimeException e)
                {
                    SparkleLogger.LogInfo("Sync", "Exception: " + e.Message + ", error content: " + e.ErrorContent);
                }
                if (session == null)
                {
                    SparkleLogger.LogInfo("Sync", "Connection failed, waiting for 10 seconds: " + this.localRootFolder + "(" + cmisParameters[SessionParameter.AtomPubUrl] + ")");
                    System.Threading.Thread.Sleep(10 * 1000);
                }
            }
            while (session == null);
        }


        /**
         * Sync in the background.
         */
        public void SyncInBackground()
        {
            if (syncing)
            {
                SparkleLogger.LogInfo("Sync", String.Format("[{0}] - sync is already running in background.", this.localRootFolder));
                return;
            }
            syncing = true;

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += new DoWorkEventHandler(
                delegate(Object o, DoWorkEventArgs args)
                {
                    SparkleLogger.LogInfo("Sync", String.Format("[{0}] - Launching sync in background, so that the UI stays available.", this.localRootFolder));
#if !DEBUG
                    try
                    {
#endif
                        Sync();
#if !DEBUG
                    }
                    catch (CmisBaseException e)
                    {
                        SparkleLogger.LogInfo("Sync", "CMIS exception while syncing:" + e.Message);
                        SparkleLogger.LogInfo("Sync", e.StackTrace);
                        SparkleLogger.LogInfo("Sync", e.ErrorContent);
                    }
#endif
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

            //            if (ChangeLogCapability)              Disabled ChangeLog algorithm until this issue is solved: https://jira.nuxeo.com/browse/NXP-10844
            //            {
            //                ChangeLogSync(remoteFolder);
            //            }
            //            else
            //            {
            // No ChangeLog capability, so we have to crawl remote and local folders.
            CrawlSync(remoteFolder, localRootFolder);
            //            }
        }


        private void ChangeLogSync(IFolder remoteFolder)
        {
            // Get last change log token on server side.
            string lastTokenOnServer = session.Binding.GetRepositoryService().GetRepositoryInfo(session.RepositoryInfo.Id, null).LatestChangeLogToken;

            // Get last change token that had been saved on client side.
            string lastTokenOnClient = database.GetChangeLogToken();

            if (lastTokenOnClient == null)
            {
                // Token is null, which means no sync has ever happened yet, so just copy everything.
                RecursiveFolderCopy(remoteFolder, localRootFolder);
            }
            else
            {
                // If there are remote changes, apply them.
                if (lastTokenOnServer.Equals(lastTokenOnClient))
                {
                    SparkleLogger.LogInfo("Sync", "No changes on server, ChangeLog token: " + lastTokenOnServer);
                }
                else
                {
                    // Check which files/folders have changed.
                    int maxNumItems = 1000;
                    IChangeEvents changes = session.GetContentChanges(lastTokenOnClient, true, maxNumItems);

                    // Replicate each change to the local side.
                    foreach (IChangeEvent change in changes.ChangeEventList)
                    {
                        ApplyRemoteChange(change);
                    }

                    // Save change log token locally.
                    // TODO only if successful
                    SparkleLogger.LogInfo("Sync", "Updating ChangeLog token: " + lastTokenOnServer);
                    database.SetChangeLogToken(lastTokenOnServer);
                }

                // Upload local changes by comparing with database.
                // TODO
            }
        }


        /**
         * Apply a remote change.
         */
        private void ApplyRemoteChange(IChangeEvent change)
        {
            SparkleLogger.LogInfo("Sync", "Change type:" + change.ChangeType + " id:" + change.ObjectId + " properties:" + change.Properties);
            switch (change.ChangeType)
            {
                case ChangeType.Created:
                case ChangeType.Updated:
                    ICmisObject cmisObject = session.GetObject(change.ObjectId);
                    if (cmisObject is DotCMIS.Client.Impl.Folder)
                    {
                        IFolder remoteFolder = (IFolder)cmisObject;
                        string localFolder = Path.Combine(localRootFolder, remoteFolder.Path);
                        RecursiveFolderCopy(remoteFolder, localFolder);
                    }
                    else if (cmisObject is DotCMIS.Client.Impl.Document)
                    {
                        IDocument remoteDocument = (IDocument)cmisObject;
                        string remoteDocumentPath = remoteDocument.Paths.First();
                        if (!remoteDocumentPath.StartsWith(remoteFolderPath))
                        {
                            SparkleLogger.LogInfo("Sync", "Change in unrelated document: " + remoteDocumentPath);
                            break; // The change is not under the folder we care about.
                        }
                        string relativePath = remoteDocumentPath.Substring(remoteFolderPath.Length + 1);
                        string relativeFolderPath = Path.GetDirectoryName(relativePath);
                        relativeFolderPath = relativeFolderPath.Replace("/", "\\"); // TODO OS-specific separator
                        string localFolderPath = Path.Combine(localRootFolder, relativeFolderPath);
                        DownloadFile(remoteDocument, localFolderPath);
                    }
                    break;
                case ChangeType.Deleted:
                    cmisObject = session.GetObject(change.ObjectId);
                    if (cmisObject is DotCMIS.Client.Impl.Folder)
                    {
                        IFolder remoteFolder = (IFolder)cmisObject;
                        string localFolder = Path.Combine(localRootFolder, remoteFolder.Path);
                        RemoveFolderLocally(localFolder); // Remove from filesystem and database.
                    }
                    else if (cmisObject is DotCMIS.Client.Impl.Document)
                    {
                        IDocument remoteDocument = (IDocument)cmisObject;
                        string remoteDocumentPath = remoteDocument.Paths.First();
                        if (!remoteDocumentPath.StartsWith(remoteFolderPath))
                        {
                            SparkleLogger.LogInfo("Sync", "Change in unrelated document: " + remoteDocumentPath);
                            break; // The change is not under the folder we care about.
                        }
                        string relativePath = remoteDocumentPath.Substring(remoteFolderPath.Length + 1);
                        string relativeFolderPath = Path.GetDirectoryName(relativePath);
                        relativeFolderPath = relativeFolderPath.Replace("/", "\\"); // TODO OS-specific separator
                        string localFolderPath = Path.Combine(localRootFolder, relativeFolderPath);
                        // TODO DeleteFile(localFolderPath); // Delete on filesystem and in database
                    }
                    break;
                case ChangeType.Security:
                    break;
            }
        }


        /**
         * Download all content from a CMIS folder.
         */
        private void RecursiveFolderCopy(IFolder remoteFolder, string localFolder)
        {
            activityListener.ActivityStarted();
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
            activityListener.ActivityStopped();
        }


        /**
         * Synchronize by checking all folders/files one-by-one.
         * This strategy is used if the CMIS server does not support the ChangeLog feature.
         * 
         * for all remote folders:
         *     if exists locally:
         *       recurse
         *     else
         *       if in database:
         *         delete recursively from server // if BIDIRECTIONAL
         *       else
         *         download recursively
         * for all remote files:
         *     if exists locally:
         *       if remote is more recent than local:
         *         download
         *       else
         *         upload                         // if BIDIRECTIONAL
         *     else:
         *       if in database:
         *         delete from server             // if BIDIRECTIONAL
         *       else
         *         download
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
            SparkleLogger.LogInfo("Sync", String.Format("Parse remote folder {0}", this.remoteFolderPath));
            crawlRemote(remoteFolder, localFolder, remoteFiles, remoteSubfolders);

            // Crawl local files.
            SparkleLogger.LogInfo("Sync", String.Format("Parse local files in the local folder {0}", localFolder));
            crawlLocalFiles(localFolder, remoteFolder, remoteFiles);

            // Crawl local folders.
            SparkleLogger.LogInfo("Sync", String.Format("Parse local folder {0}", localFolder));
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
                    if (CheckRules(remoteSubFolder.Name, RulesType.Folder))
                    {
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
                                database.RemoveFolder(localSubFolder);
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
                }
                else
                {
                    // It is a CMIS document.
                    IDocument remoteDocument = (IDocument)cmisObject;

                    if (CheckRules(remoteDocument.Name, RulesType.File))
                    {
                        // We use the filename of the document's content stream.
                        // This can be different from the name of the document.
                        // For instance in FileNet it is not usual to have a document where
                        // document.Name is "foo" and document.ContentStreamFileName is "foo.jpg".
                        string remoteDocumentFileName = remoteDocument.ContentStreamFileName;

                        // Check if file extension is allowed

                        remoteFiles.Add(remoteDocumentFileName);
                        // If this file does not have a filename, ignore it.
                        // It sometimes happen on IBM P8 CMIS server, not sure why.
                        if (remoteDocumentFileName == null)
                        {
                            SparkleLogger.LogInfo("Sync", "Skipping download of '" + remoteDocument.Name + "' with null content stream in " + localFolder);
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
                                    if (database.LocalFileHasChanged(filePath))
                                    {
                                        SparkleLogger.LogInfo("Sync", "Conflict with file: " + remoteDocumentFileName + ", backing up locally modified version and downloading server version");
                                        // Rename locally modified file.
                                        File.Move(filePath, SuffixIfExists(filePath + "_your-version"));

                                        // Download server version
                                        DownloadFile(remoteDocument, localFolder);

                                        // TODO move to OS-dependant layer
                                        //System.Windows.Forms.MessageBox.Show("Someone modified a file at the same time as you: " + filePath
                                        //    + "\n\nYour version has been saved with a '_your-version' suffix, please merge your important changes from it and then delete it.");
                                        // TODO show CMIS property lastModifiedBy
                                    }
                                    else
                                    {
                                        SparkleLogger.LogInfo("Sync", "Downloading modified file: " + remoteDocumentFileName);
                                        DownloadFile(remoteDocument, localFolder);
                                    }
                                }

                                // Change modification date in database
                                database.SetFileServerSideModificationDate(filePath, serverSideModificationDate);
                            }
                        }
                        else
                        {
                            if (database.ContainsFile(filePath))
                            {
                                // File has been recently removed locally, so remove it from server too.
                                remoteDocument.DeleteAllVersions();

                                // Remove it from database.
                                database.RemoveFile(filePath);
                            }
                            else
                            {
                                // New remote file, download it.
                                SparkleLogger.LogInfo("Sync", "Downloading new file: " + remoteDocumentFileName);
                                DownloadFile(remoteDocument, localFolder);
                            }
                        }
                    }
                }
            }
        }


        /**
         * Crawl local files in a given directory (not recursive).
         */
        private void crawlLocalFiles(string localFolder, IFolder remoteFolder, IList remoteFiles)
        {
            foreach (string filePath in Directory.GetFiles(localFolder))
            {
                string fileName = Path.GetFileName(filePath);

                if (!CheckRules(filePath, RulesType.File)) return;

                if (!remoteFiles.Contains(fileName))
                {
                    // This local file is not on the CMIS server now, so
                    // check whether it used to exist on server or not.
                    if (database.ContainsFile(filePath))
                    {
                        // If file has changed locally, move to 'your_version' and warn about conflict
                        // TODO

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
                            SparkleLogger.LogInfo("Sync", "Uploading file absent on repository: " + filePath);
                            UploadFile(filePath, remoteFolder);
                        }
                    }
                }
                else
                {
                    // The file exists both on server and locally.
                    if (database.LocalFileHasChanged(filePath))
                    {
                        if (BIDIRECTIONAL)
                        {
                            // Upload new version of file content.
                            SparkleLogger.LogInfo("Sync", "Uploading file update on repository: " + filePath);
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
            foreach (string localSubFolder in Directory.GetDirectories(localFolder))
            {
                if (CheckRules(localSubFolder, RulesType.Folder))
                {
                    string folderName = Path.GetFileName(localSubFolder);
                    if (!remoteFolders.Contains(folderName))
                    {
                        // This local folder is not on the CMIS server now, so
                        // check whether it used to exist on server or not.
                        if (database.ContainsFolder(localSubFolder))
                        {
                            RemoveFolderLocally(localSubFolder);
                        }
                        else
                        {
                            if (BIDIRECTIONAL)
                            {
                                // New local folder, upload recursively.
                                UploadFolderRecursively(remoteFolder, localSubFolder);
                            }
                        }
                    }
                }
            }
        }

        /**
         * Download a single file from the CMIS server.
         * Full rewrite by Yannick
         */
        private void DownloadFile(IDocument remoteDocument, string localFolder)
        {
            activityListener.ActivityStarted();

            string filepath = Path.Combine(localFolder, remoteDocument.ContentStreamFileName);

            // If a file exist, file is deleted.
            if (File.Exists(filepath))
                File.Delete(filepath);

            string tmpfilepath = filepath + ".sync";

            // Create Stream with the local file in append mode, if file is empty it's like a full download.
            StreamWriter localfile = new StreamWriter(tmpfilepath, true);
            localfile.AutoFlush = true;
            DotCMIS.Data.IContentStream contentStream = null;

            // Download file, starting at the last download point

            // Get the last position in the localfile.
            Boolean success = false;
            try
            {
                Int64 Offset = localfile.BaseStream.Position;

                contentStream = remoteDocument.GetContentStream(remoteDocument.Id, Offset, remoteDocument.ContentStreamLength);
                if (contentStream == null)
                {
                    SparkleLogger.LogInfo("Sync", "Skipping download of file with null content stream: " + remoteDocument.ContentStreamFileName);
                    throw new IOException();
                }

                SparkleLogger.LogInfo("Sync", String.Format("Start download of file with offset {0}", Offset));

                CopyStream(contentStream.Stream, localfile.BaseStream);
                localfile.Flush();
                localfile.Close();
                contentStream.Stream.Close();
                success = true;
            }
            catch (Exception ex)
            {
                SparkleLogger.LogInfo("Sync", String.Format("Download of file {0} abort: {1}", remoteDocument.ContentStreamFileName, ex));
                success = false;
                localfile.Flush();
                localfile.Close();
                if (contentStream != null) contentStream.Stream.Close();
            }
            // Rename file
            // TODO - Yannick - Control file integrity by using hash compare - Is it necessary ?
            if (success)
            {
                File.Move(tmpfilepath, filepath);

                // Get metadata.
                Dictionary<string, string> metadata = new Dictionary<string, string>();
                metadata.Add("Id", remoteDocument.Id);
                metadata.Add("VersionSeriesId", remoteDocument.VersionSeriesId);
                metadata.Add("VersionLabel", remoteDocument.VersionLabel);
                metadata.Add("CreationDate", remoteDocument.CreationDate.ToString());
                metadata.Add("CreatedBy", remoteDocument.CreatedBy);
                metadata.Add("lastModifiedBy", remoteDocument.LastModifiedBy);
                metadata.Add("CheckinComment", remoteDocument.CheckinComment);
                metadata.Add("IsImmutable", (bool)(remoteDocument.IsImmutable) ? "true" : "false");
                metadata.Add("ContentStreamMimeType", remoteDocument.ContentStreamMimeType);

                // Create database entry for this file.
                database.AddFile(filepath, remoteDocument.LastModificationDate, metadata);
            }
            activityListener.ActivityStopped();
        }

        private void CopyStream(Stream src, Stream dst)
        {
            byte[] buffer = new byte[8 * 1024];
            while (true)
            {
                int read = src.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    return;
                dst.Write(buffer, 0, read);
            }
        }

        ///**
        // * Download a single file from the CMIS server.
        // */
        //private void DownloadFile(IDocument remoteDocument, string localFolder)
        //{
        //    activityListener.ActivityStarted();

        //    //TODO - Yannick - CanSeek is not supported on contentStream.Stream but we can do the trick with HttpWebRequest.AddRange that is implemented behind GetContentStream(String id, Int64? offset, Int64? length)
        //    //The download code must be rewrite from scratch.
        //    DotCMIS.Data.IContentStream contentStream = remoteDocument.GetContentStream();

        //    // If this file does not have a content stream, ignore it.
        //    // Even 0 bytes files have a contentStream.
        //    // null contentStream sometimes happen on IBM P8 CMIS server, not sure why.
        //    if (contentStream == null)
        //    {
        //        SparkleLogger.LogInfo("Sync", "Skipping download of file with null content stream: " + remoteDocument.ContentStreamFileName);
        //        return;
        //    }

        //    // Download.
        //    string filePath = localFolder + Path.DirectorySeparatorChar + contentStream.FileName;

        //    // If there was previously a directory with this name, delete it.
        //    // TODO warn if local changes inside the folder.
        //    if (Directory.Exists(filePath))
        //    {
        //        Directory.Delete(filePath);
        //    }

        //    bool success = false;
        //    do
        //    {
        //        try
        //        {
        //            DownloadFile(contentStream, filePath);
        //            success = true;
        //        }
        //        catch (WebException e)
        //        {
        //            SparkleLogger.LogInfo("Sync", e.Message);
        //            SparkleLogger.LogInfo("Sync", "Problem during download, waiting for 10 seconds...");
        //            System.Threading.Thread.Sleep(10 * 1000);
        //        }
        //    }
        //    while (!success);

        //    // Get metadata.
        //    Dictionary<string, string> metadata = new Dictionary<string, string>();
        //    metadata.Add("Id", remoteDocument.Id);
        //    metadata.Add("VersionSeriesId", remoteDocument.VersionSeriesId);
        //    metadata.Add("VersionLabel", remoteDocument.VersionLabel);
        //    metadata.Add("CreationDate", remoteDocument.CreationDate.ToString());
        //    metadata.Add("CreatedBy", remoteDocument.CreatedBy);
        //    metadata.Add("lastModifiedBy", remoteDocument.LastModifiedBy);
        //    metadata.Add("CheckinComment", remoteDocument.CheckinComment);
        //    metadata.Add("IsImmutable", (bool)(remoteDocument.IsImmutable) ? "true" : "false");
        //    metadata.Add("ContentStreamMimeType", remoteDocument.ContentStreamMimeType);

        //    // Create database entry for this file.
        //    database.AddFile(filePath, remoteDocument.LastModificationDate, metadata);
        //    activityListener.ActivityStopped();
        //}

        ///**
        // * Download a file, without retrying
        // */
        //private void DownloadFile(DotCMIS.Data.IContentStream contentStream, string filePath)
        //{
        //    SparkleLogger.LogInfo("Sync", "Downloading " + filePath);
        //    // Append .sync at the end of the filename
        //    String tmpfile = filePath + ".sync";
        //    Stream file = File.OpenWrite(tmpfile);
        //    CopyStream(contentStream.Stream, file);
        //    //byte[] buffer = new byte[8 * 1024];
        //    //int len;
        //    //while ((len = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0) // TODO catch WebException here and retry
        //    //{
        //    //    file.Write(buffer, 0, len);
        //    //}
        //    file.Close();
        //    contentStream.Stream.Close();
        //    File.Move(tmpfile, filePath);
        //    SparkleLogger.LogInfo("Sync", "Downloaded");
        //}

        /**
         * Upload a single file to the CMIS server.
         */
        private void UploadFile(string filePath, IFolder remoteFolder)
        {
            activityListener.ActivityStarted();
            IDocument remoteDocument = null;
            try
            {
                // Prepare properties
                string fileName = Path.GetFileName(filePath);
                string tmpfileName = fileName + ".sync";
                Dictionary<string, object> properties = new Dictionary<string, object>();
                properties.Add(PropertyIds.Name, tmpfileName);
                properties.Add(PropertyIds.ObjectTypeId, "cmis:document");

                Boolean success = false;

                // Prepare content stream
                Stream file = File.OpenRead(filePath);
                ContentStream contentStream = new ContentStream();
                contentStream.FileName = fileName;
                contentStream.Stream = new MemoryStream();
                contentStream.MimeType = MimeType.GetMIMEType(fileName); // Should CmisSync try to guess?
                contentStream.Length = file.Length;
                contentStream.Stream = file;

                // Upload
                try
                {
                    // Until we can not append the remote file, if exist must delete it to avoid conflict
                    try
                    {
                        string remotepath = remoteFolder.Path + '/' + tmpfileName;
                        ICmisObject obj = session.GetObjectByPath(remotepath);
                        if (obj != null)
                        {
                            SparkleLogger.LogInfo("Sync", "Temp file exist on remote server, but CMIS 1.0 don't support Append Mode");
                            SparkleLogger.LogInfo("Sync", String.Format("We delete temp file and create a new empty file on the CMIS Server for {0} and launch a simple update", filePath));
                            obj.Delete(true);
                        }
                    }
                    catch (DotCMIS.Exceptions.CmisObjectNotFoundException)
                    {
                        // Create an empty file on remote server and get ContentStream
                        SparkleLogger.LogInfo("Sync", String.Format("File do not exist on remote server, so create an Empty file on the CMIS Server for {0} and launch a simple update", filePath));
                    }

                    // remoteDocument = remoteFolder.CreateDocument(properties, contentStream, null);
                    // UpdateFile(filePath, remoteDocument);
                    remoteDocument = remoteFolder.CreateDocument(properties, contentStream, null);
                    success = true;
                }
                catch (Exception ex)
                {
                    SparkleLogger.LogInfo("Sync", String.Format("Upload of file {0} abort: {1}", filePath, ex));
                    success = false;
                    if (contentStream != null) contentStream.Stream.Close();
                }

                if (success)
                {
                    SparkleLogger.LogInfo("Sync", String.Format("Upload of file {0} finish", filePath));
                    if (contentStream != null) contentStream.Stream.Close();
                    properties[PropertyIds.Name] = fileName;
                    remoteDocument.UpdateProperties(properties, true);

                    // Create database entry for this file.
                    database.AddFile(filePath, remoteDocument.LastModificationDate, null);
                }
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException ||
                    e is IOException)
                {
                    SparkleLogger.LogInfo("Sync", "File deleted while trying to upload it, reverting.");
                    // File has been deleted while we were trying to upload/checksum/add.
                    // This can typically happen in Windows when creating a new text file and giving it a name.
                    // Revert the upload.
                    if (remoteDocument != null)
                    {
                        remoteDocument.DeleteAllVersions();
                    }
                }
                else
                {
                    throw;
                }
            }
            activityListener.ActivityStopped();
        }

        /**
         * Upload folder recursively.
         * After execution, the hierarchy on server will be: .../remoteBaseFolder/localFolder/...
         */
        private void UploadFolderRecursively(IFolder remoteBaseFolder, string localFolder)
        {
            // Create remote folder.
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, Path.GetFileName(localFolder));
            properties.Add(PropertyIds.ObjectTypeId, "cmis:folder");
            IFolder folder = remoteBaseFolder.CreateFolder(properties);

            // Create database entry for this folder.
            database.AddFolder(localFolder, folder.LastModificationDate);

            // Upload each file in this folder.
            foreach (string file in Directory.GetFiles(localFolder))
            {
                UploadFile(file, folder);
            }

            // Recurse for each subfolder in this folder.
            foreach (string subfolder in Directory.GetDirectories(localFolder))
            {
                UploadFolderRecursively(folder, subfolder);
            }
        }



        private void UpdateFile(string filePath, IDocument remoteFile)
        {
            Stream localfile = File.OpenRead(filePath);
            if (localfile == null)
            {
                SparkleLogger.LogInfo("Sync", "Skipping upload/update of file with null content stream: " + filePath);
                throw new IOException();
            }

            // Prepare content stream
            string fileName = Path.GetFileName(filePath);

            ContentStream remoteStream = (ContentStream)remoteFile.GetContentStream();
            remoteStream.Stream = localfile;
            remoteStream.Length = localfile.Length;

            // CMIS do not have a Method to upload block by block. So upload file must be full.
            // We must waiting for support of CMIS 1.1 https://issues.apache.org/jira/browse/CMIS-628
            // http://docs.oasis-open.org/cmis/CMIS/v1.1/cs01/CMIS-v1.1-cs01.html#x1-29700019
            remoteFile.SetContentStream(remoteStream, true, true);

            SparkleLogger.LogInfo("Sync", "Update finished:" + filePath);
        }

        /**
         * Upload new version of file content.
         */
        private void UpdateFile(string filePath, IFolder remoteFolder)
        {
            SparkleLogger.LogInfo("Sync", "Updated " + filePath);
            activityListener.ActivityStarted();
            string fileName = Path.GetFileName(filePath);

            IDocument document = null;
            bool found = false;
            foreach (ICmisObject obj in remoteFolder.GetChildren())
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
                SparkleLogger.LogInfo("Sync", filePath + " not found on server, must be uploaded instead of updated");
                return;
            }

            UpdateFile(filePath, document);

            // Read new last modification date.
            // Update timestamp in database.
            database.SetFileServerSideModificationDate(filePath, document.LastModificationDate);
            activityListener.ActivityStopped();
        }

        /**
         * Remove folder from local filesystem and database.
         */
        private void RemoveFolderLocally(string folderPath)
        {
            // Folder has been deleted on server, delete it locally too.
            SparkleLogger.LogInfo("Sync", "Removing remotely deleted folder: " + folderPath);
            Directory.Delete(folderPath, true);

            // Delete folder from database.
            database.RemoveFolder(folderPath);
        }

        /**
         * Find an available name (potentially suffixed) for this file.
         * For instance:
         * - if /dir/file does not exist, return the same path
         * - if /dir/file exists, return /dir/file (1)
         * - if /dir/file (1) also exists, return /dir/file (2)
         * - etc
         */
        public static string SuffixIfExists(String path)
        {
            if (!File.Exists(path))
            {
                return path;
            }
            else
            {
                int index = 1;
                do
                {
                    string ret = path + " (" + index + ")";
                    if (!File.Exists(ret))
                    {
                        return ret;
                    }
                    index++;
                }
                while (true);
            }
        }

        /**
         * Check if the filename provide is compliance
         * Return true if path is ok, or false is path contains one or more rule
         * */
        public Boolean CheckRules(string path, RulesType ruletype)
        {
            string[] contents = new string[] {
                "~",             // gedit and emacs
                "Thumbs.db", "Desktop.ini","desktop.ini","thumbs.db", // Windows
                "$~"
            };

            string[] extensions = new string[] {
            ".autosave", // Various autosaving apps
            ".~lock", // LibreOffice
            ".part", ".crdownload", // Firefox and Chromium temporary download files
            ".sw[a-z]", ".un~", ".swp", ".swo", // vi(m)
            ".directory", // KDE
            ".DS_Store", ".Icon\r\r", "._", ".Spotlight-V100", ".Trashes", // Mac OS X
            ".(Autosaved).graffle", // Omnigraffle
            ".tmp", ".TMP", // MS Office
            ".~ppt", ".~PPT", ".~pptx", ".~PPTX",
            ".~xls", ".~XLS", ".~xlsx", ".~XLSX",
            ".~doc", ".~DOC", ".~docx", ".~DOCX",
            ".cvsignore", ".~cvsignore", // CVS
            ".sync", // CmisSync File Downloading/Uploading
            ".cmissync" // CmisSync Database 
            };

            string[] directories = new string[] {
                "CVS",".svn",".hg",".bzr",".DS_Store", ".Icon\r\r", "._", ".Spotlight-V100", ".Trashes" // Mac OS X
            };

            SparkleLogger.LogInfo("Sync", "Check rules for " + path);
            Boolean found = false;
            foreach (string content in contents)
            {
                if (path.Contains(content)) found = true;
            }

            if (ruletype == RulesType.Folder)
            {
                foreach (string dir in directories)
                {
                    if (path.Contains(dir)) found = true;
                }
            }
            else
            {
                foreach (string ext in extensions)
                {
                    string filext = Path.GetExtension(path);
                    if (filext == ext) found = true;
                }
            }

            return !found;

        }
    }

}