using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using DotCMIS.Client;
using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Exceptions;
using DotCMIS.Enums;
using System.ComponentModel;
using System.Collections;
using DotCMIS.Data.Impl;

using System.Net;
using CmisSync.Lib.Cmis;
using DotCMIS.Data;
using log4net;
using System.Security.Cryptography;

namespace CmisSync.Lib.Sync
{
    public partial class CmisRepo : RepoBase
    {
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CmisRepo));


        /// <summary>
        /// Synchronization with a particular CMIS folder.
        /// </summary>
        public partial class SynchronizedFolder : IDisposable
        {
            // Log
            private static readonly ILog Logger = LogManager.GetLogger(typeof(SynchronizedFolder));
            
            /// <summary>
            /// Whether sync is bidirectional or only from server to client.
            /// TODO make it a CMIS folder - specific setting
            /// </summary>
            private bool BIDIRECTIONAL = true;

            /// <summary>
            /// At which degree the repository supports Change Logs.
            /// See http://docs.oasis-open.org/cmis/CMIS/v1.0/os/cmis-spec-v1.0.html#_Toc243905424
            /// The possible values are actually none, objectidsonly, properties, all
            /// But for now we only distinguish between none (false) and the rest (true)
            /// </summary>
            private bool ChangeLogCapability;

            /// <summary>
            /// If the repository is able send a folder tree in one request, this is true,
            /// Otherwise the default behaviour is false
            /// </summary>
            private bool IsGetFolderTreeSupported = false;

            /// <summary>
            /// If the repository allows to request all Descendants of a folder or file,
            /// this is set to true, otherwise the default behaviour is false
            /// </summary>
            private bool IsGetDescendantsSupported = false;

            /// <summary>
            /// Is true, if the repository is able to return property changes.
            /// </summary>
            private bool IsPropertyChangesSupported = false;


            /// <summary>
            /// Session to the CMIS repository.
            /// </summary>
            private ISession session;


            /// <summary>
            /// Path of the root in the remote repository.
            // Example: "/User Homes/nicolas.raoul/demos"
            /// </summary>
            private string remoteFolderPath;


            /// <summary>
            /// Syncing lock.
            /// true if syncing is being performed right now.
            /// TODO use is_syncing variable in parent
            /// </summary>
            private bool syncing;


            /// <summary>
            /// Parameters to use for all CMIS requests.
            /// </summary>
            private Dictionary<string, string> cmisParameters;


            /// <summary>
            /// Track whether <c>Dispose</c> has been called.
            /// </summary>
            private bool disposed = false;


            /// <summary>
            /// Track whether <c>Dispose</c> has been called.
            /// </summary>
            private Object disposeLock = new Object();


            /// <summary>
            /// Database to cache remote information from the CMIS server.
            /// </summary>
            private Database database;


            /// <summary>
            /// Listener we inform about activity (used by spinner).
            /// </summary>
            private IActivityListener activityListener;


            /// <summary>
            /// Configuration of the CmisSync synchronized folder, as defined in the XML configuration file.
            /// </summary>
            private RepoInfo repoinfo;


            /// <summary>
            /// Link to parent object.
            /// </summary>
            private RepoBase repo;


            /// <summary>
            ///  Constructor for Repo (at every launch of CmisSync)
            /// </summary>
            public SynchronizedFolder(RepoInfo repoInfo,
                IActivityListener listener, RepoBase repoCmis)
            {
                if (null == repoInfo || null == repoCmis)
                {
                    throw new ArgumentNullException("repoInfo");
                }

                this.repo = repoCmis;
                this.activityListener = listener;
                this.repoinfo = repoInfo;

                // Database is the user's AppData/Roaming
                database = new Database(repoinfo.CmisDatabase);

                // Get path on remote repository.
                remoteFolderPath = repoInfo.RemotePath;

                cmisParameters = new Dictionary<string, string>();
                cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
                cmisParameters[SessionParameter.AtomPubUrl] = repoInfo.Address.ToString();
                cmisParameters[SessionParameter.User] = repoInfo.User;
                cmisParameters[SessionParameter.Password] = repoInfo.Password.ToString();
                cmisParameters[SessionParameter.RepositoryId] = repoInfo.RepoID;
                cmisParameters[SessionParameter.ConnectTimeout] = "-1";

                foreach (string ignoredFolder in repoInfo.getIgnoredPaths())
                {
                    Logger.Info("The folder \"" + ignoredFolder + "\" will be ignored");
                }
            }


            /// <summary>
            /// Destructor.
            /// </summary>
            ~SynchronizedFolder()
            {
                Dispose(false);
            }


            /// <summary>
            /// Implement IDisposable interface. 
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }


            /// <summary>
            /// Dispose pattern implementation.
            /// </summary>
            protected virtual void Dispose(bool disposing)
            {
                lock (disposeLock)
                {
                    if (!this.disposed)
                    {
                        if (disposing)
                        {
                            this.database.Dispose();
                        }
                        this.disposed = true;
                    }
                }
            }

            /// <summary>
            /// Resets all the failed upload to zero.
            /// </summary>
            public void resetFailedOperationsCounter()
            {
                database.DeleteAllFailedOperations();
            }


            /// <summary>
            /// Connect to the CMIS repository.
            /// </summary>
            public void Connect()
            {
                try
                {
                    // Create session factory.
                    SessionFactory factory = SessionFactory.NewInstance();
                    session = factory.CreateSession(cmisParameters);
                    // Detect whether the repository has the ChangeLog capability.
                    Logger.Debug("Created CMIS session: " + session.ToString());
                    ChangeLogCapability = session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All
                            || session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.ObjectIdsOnly;
                    IsGetDescendantsSupported = session.RepositoryInfo.Capabilities.IsGetDescendantsSupported == true;
                    IsGetFolderTreeSupported = session.RepositoryInfo.Capabilities.IsGetFolderTreeSupported == true;
                    Config.SyncConfig.Folder folder = ConfigManager.CurrentConfig.getFolder(this.repoinfo.Name);
                    if (folder != null)
                    {
                        Config.Feature features = folder.SupportedFeatures;
                        if (features != null)
                        {
                            if (IsGetDescendantsSupported && features.GetDescendantsSupport == false)
                                IsGetDescendantsSupported = false;
                            if (IsGetFolderTreeSupported && features.GetFolderTreeSupport == false)
                                IsGetFolderTreeSupported = false;
                            if (ChangeLogCapability && features.GetContentChangesSupport == false)
                                ChangeLogCapability = false;
                            if(ChangeLogCapability && session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All 
                                || session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.Properties)
                                IsPropertyChangesSupported = true;
                        }
                    }
                    Logger.Debug("ChangeLog capability: " + ChangeLogCapability.ToString());
                    Logger.Debug("Get folder tree support: " + IsGetFolderTreeSupported.ToString());
                    Logger.Debug("Get descendants support: " + IsGetDescendantsSupported.ToString());
                    if(repoinfo.ChunkSize>0) {
                        Logger.Debug("Chunked Up/Download enabled: chunk size = "+ repoinfo.ChunkSize.ToString() + " byte");
                    }else {
                        Logger.Debug("Chunked Up/Download disabled");
                    }
                    HashSet<string> filters = new HashSet<string>();
                    filters.Add("cmis:objectId");
                    filters.Add("cmis:name");
                    filters.Add("cmis:contentStreamFileName");
                    filters.Add("cmis:contentStreamLength");
                    filters.Add("cmis:lastModificationDate");
                    filters.Add("cmis:path");
                    session.DefaultContext = session.CreateOperationContext(filters, false, true, false, IncludeRelationshipsFlag.None, null, true, null, true, 100);
                }
                //TODO Implement error handling -> informing user about connection problems by showing status
                catch (CmisRuntimeException e)
                {
                    Logger.Error("Connection to repository failed: ", e);
                }
                catch (CmisObjectNotFoundException e)
                {
                    Logger.Error("Failed to find cmis object: ", e);
                }
                catch (CmisBaseException e)
                {
                    Logger.Error("Failed to create session to remote " + this.repoinfo.Address.ToString() + ": ", e);
                }
            }


            /// <summary>
            /// Track whether a full sync is done
            /// </summary>
            private bool syncFull = false;

            /// <summary>
            /// Synchronize between CMIS folder and local folder.
            /// </summary>
            public void Sync()
            {
                //// If not connected, connect.
                //if (session == null)
                //{
                //    Connect();
                //}
                sleepWhileSuspended();
                //  Force to create the session to reset the cache for each Sync, since DotCMIS uses cache
                Connect();

                if (session == null)
                {
                    Logger.Error("Could not connect to: " + cmisParameters[SessionParameter.AtomPubUrl]);
                    return; // Will try again at next sync. 
                }

                IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath);
                string localFolder = repoinfo.TargetDirectory;

                if (!repo.Watcher.EnableRaisingEvents)
                {
                    repo.Watcher.RemoveAll();
                    repo.Watcher.EnableRaisingEvents = true;
                    syncFull = false;
                }
                if (!syncFull)
                {
                    Logger.Debug("Invoke a full crawl sync");
                    syncFull = CrawlSync(remoteFolder, localFolder);
                    return;
                }

                if (ChangeLogCapability)
                {
                    Logger.Debug("Invoke a remote change log sync");
                    ChangeLogSync(remoteFolder);
                    if(repo.Watcher.GetChangeList().Count > 0)
                    {
                        Logger.Debug("Changes on the local file system detected => starting crawl sync");
                        repo.Watcher.RemoveAll();
                        if(!CrawlSync(remoteFolder,localFolder))
                            repo.Watcher.InsertChange("/", Watcher.ChangeTypes.Changed);
                    }
                }
                else
                {
                    //  have to crawl remote
                    Logger.Debug("Invoke a remote crawl sync");
                    repo.Watcher.RemoveAll();
                    CrawlSync(remoteFolder, localFolder);
                }
                /*
                Logger.Debug("Invoke a file system watcher sync");
                WatcherSync(remoteFolderPath, localFolder);
                foreach (string name in repo.Watcher.GetChangeList())
                {
                    Logger.Debug(String.Format("Change name {0} type {1}", name, repo.Watcher.GetChangeType(name)));
                }*/
            }


            /// <summary>
            /// Sync in the background.
            /// </summary>
            public void SyncInBackground()
            {
                if (this.syncing)
                {
                    //Logger.Debug("Sync already running in background: " + repoinfo.TargetDirectory);
                    return;
                }
                this.syncing = true;

                using (BackgroundWorker bw = new BackgroundWorker())
                {
                    bw.DoWork += new DoWorkEventHandler(
                        delegate(Object o, DoWorkEventArgs args)
                        {
                            Logger.Debug("Launching sync: " + repoinfo.TargetDirectory);
                            try
                            {
                                Sync();
                            }
                            catch (CmisBaseException e)
                            {
                                Logger.Error("CMIS exception while syncing:", e);
                            }
                            catch(ObjectDisposedException e)
                            {
                                Logger.Warn("Object disposed while syncing:", e);
                            }
                            catch(Exception e)
                            {
                                Logger.Warn("Execption thrown while syncing:", e);
                            }
                        }
                    );
                    bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                        delegate(object o, RunWorkerCompletedEventArgs args)
                        {
                            this.syncing = false;
                        }
                    );
                    bw.RunWorkerAsync();
                }
            }


            /// <summary>
            /// Download all content from a CMIS folder.
            /// </summary>
            private bool RecursiveFolderCopy(IFolder remoteFolder, string localFolder)
            {
                using (new ActivityListenerResource(activityListener))
                {
                    bool success = true;

                    try
                    {
                        // List all children.
                        foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
                        {
                            if (cmisObject is DotCMIS.Client.Impl.Folder)
                            {
                                IFolder remoteSubFolder = (IFolder)cmisObject;
                                string localSubFolder = localFolder + Path.DirectorySeparatorChar.ToString() + cmisObject.Name;
                                if (!Utils.IsInvalidFolderName(remoteFolder.Name) && !repoinfo.isPathIgnored(remoteSubFolder.Path))
                                {
                                    // Create local folder.
                                    Logger.Info("Creating local directory: "+ localSubFolder);
                                    Directory.CreateDirectory(localSubFolder);

                                    // Create database entry for this folder
                                    // TODO Add metadata
                                    database.AddFolder(localSubFolder, remoteSubFolder.Id, remoteSubFolder.LastModificationDate);

                                    // Recurse into folder.
                                    success = RecursiveFolderCopy(remoteSubFolder, localSubFolder) && success;
                                }
                            }
                            else
                            {
                                if (Utils.WorthSyncing(cmisObject.Name))
                                    // It is a file, just download it.
                                    success = DownloadFile((IDocument)cmisObject, localFolder) && success;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Warn(String.Format("Exception while download to local folder {0}: {1}", localFolder, Utils.ToLogString(e)));
                        success = false;
                    }

                    return success;
                }
            }


            private bool DownloadStreamInChunks(string filePath, Stream fileStream, IDocument remoteDocument)
            {
                if (repoinfo.ChunkSize <= 0)
                {
                    return false;
                }
                Logger.Debug(String.Format("Start downloading a chunk (size={0}): {1} from remote document: {2}", repoinfo.ChunkSize, filePath, remoteDocument.Name ));
                long? fileLength = remoteDocument.ContentStreamLength;
                FileInfo fileInfo = new FileInfo(filePath);

                for (long offset = fileInfo.Length; offset < fileLength; offset += repoinfo.ChunkSize)
                {
                    lock (disposeLock)
                    {
                        if (disposed)
                        {
                            throw new ObjectDisposedException("Downloading");
                        }
                        IContentStream contentStream = remoteDocument.GetContentStream(remoteDocument.ContentStreamId, offset, repoinfo.ChunkSize);
                        using (contentStream.Stream)
                        {
                            byte[] buffer = new byte[8 * 1024];
                            int len;
                            while ((len = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                fileStream.Write(buffer, 0, len);
                            }
                        }
                    }
                }

                return true;
            }


            /// <summary>
            /// Download a single folder from the CMIS server for sync.
            /// </summary>
            private bool SyncDownloadFolder(IFolder remoteSubFolder, string localFolder)
            {
                string name = remoteSubFolder.Name;
                string remotePathname = remoteSubFolder.Path;
                string localSubFolder = Path.Combine(localFolder, name);
                if(!Directory.Exists(localFolder))
                {
                    // The target folder has been removed/renamed => relaunch sync
                    Logger.Warn("The target folder has been removed/renamed: "+ localFolder);
                    return false;
                }

                if (Directory.Exists(localSubFolder))
                {
                    return true;
                }

                if (database.ContainsFolder(localSubFolder))
                {
                    // If there was previously a folder with this name, it means that
                    // the user has deleted it voluntarily, so delete it from server too.

                    // Delete the folder from the remote server.
                    Logger.Debug(String.Format("CMIS::DeleteTree({0})",remoteSubFolder.Path));
                    try{
                        remoteSubFolder.DeleteTree(true, null, true);
                        // Delete the folder from database.
                        database.RemoveFolder(localSubFolder);
                    }catch(Exception)
                    {
                        Logger.Info("Remote Folder could not be deleted: "+ remoteSubFolder.Path);
                        // Just go on and try it the next time
                    }
                }
                else
                {
                    // The folder has been recently created on server, so download it.

                    // If there was previously a file with this name, delete it.
                    // TODO warn if local changes in the file.
                    if (File.Exists(localSubFolder))
                    {
                        Logger.Warn("Local file \"" + localSubFolder + "\" has been renamed to \"" + localSubFolder + ".conflict\"");
                        File.Move(localSubFolder, localSubFolder + ".conflict");
                    }

                    // Skip if invalid folder name. See https://github.com/nicolas-raoul/CmisSync/issues/196
                    if (Utils.IsInvalidFolderName(name))
                    {
                        Logger.Info("Skipping download of folder with illegal name: " + name);
                    }
                    else if (repoinfo.isPathIgnored(remotePathname))
                    {
                        Logger.Info("Skipping dowload of ignored folder: " + remotePathname);
                    }
                    else
                    {
                        // Create local folder.remoteDocument.Name
                        Logger.Info("Creating local directory: " + localSubFolder);
                        Directory.CreateDirectory(localSubFolder);

                        // Create database entry for this folder.
                        // TODO - Yannick - Add metadata
                        database.AddFolder(localSubFolder, remoteSubFolder.Id, remoteSubFolder.LastModificationDate);
                    }
                }

                return true;
            }


            /// <summary>
            /// Download a single file from the CMIS server for sync.
            /// </summary>
            private bool SyncDownloadFile(IDocument remoteDocument, string localFolder, IList<string> remoteFiles = null)
            {
                string fileName = remoteDocument.Name;
                string filePath = Path.Combine(localFolder, fileName);

                // If this file does not have a filename, ignore it.
                // It sometimes happen on IBM P8 CMIS server, not sure why.
                if (remoteDocument.ContentStreamFileName == null)
                {
                    //TODO Possibly the file content has been changed to 0, this case should be handled
                    Logger.Warn("Skipping download of '" + fileName + "' with null content stream in " + localFolder);
                    return true;
                }

                if (null != remoteFiles)
                {
                    remoteFiles.Add(fileName);
                }

                // Check if file extension is allowed
                if (!Utils.WorthSyncing(fileName))
                {
                    Logger.Info("Ignore the unworth syncing remote file: " + fileName);
                    return true;
                }

                bool success = true;

                try
                {
                    if (File.Exists(filePath))
                    {
                        // Check modification date stored in database and download if remote modification date if different.
                        DateTime? serverSideModificationDate = ((DateTime)remoteDocument.LastModificationDate).ToUniversalTime();
                        DateTime? lastDatabaseUpdate = database.GetServerSideModificationDate(filePath);

                        if (lastDatabaseUpdate == null)
                        {
                            Logger.Info("Downloading file absent from database: " + filePath);
                            success = DownloadFile(remoteDocument, localFolder);
                        }
                        else
                        {
                            // If the file has been modified since last time we downloaded it, then download again.
                            if (serverSideModificationDate > lastDatabaseUpdate)
                            {
                                Logger.Info("Downloading modified file: " + fileName);
                                success = DownloadFile(remoteDocument, localFolder);
                            }
                            else if(serverSideModificationDate == lastDatabaseUpdate)
                            {
                                //  check chunked upload
                                FileInfo fileInfo = new FileInfo(filePath);
                                if (remoteDocument.ContentStreamLength < fileInfo.Length)
                                {
                                    success = ResumeUploadFile(filePath, remoteDocument);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (database.ContainsFile(filePath))
                        {
                            long retries = database.GetOperationRetryCounter(filePath, Database.OperationType.DELETE);
                            if(retries <= repoinfo.MaxDeletionRetries) {
                                // File has been recently removed locally, so remove it from server too.
                                Logger.Info("Removing locally deleted file on server: " + filePath);
                                try{
                                    remoteDocument.DeleteAllVersions();
                                    // Remove it from database.
                                    database.RemoveFile(filePath);
                                    database.SetOperationRetryCounter(filePath, 0, Database.OperationType.DELETE);
                                } catch(CmisBaseException ex)
                                {
                                    Logger.Warn("Could not delete remote file: ", ex);
                                    database.SetOperationRetryCounter(filePath, retries+1, Database.OperationType.DELETE);
                                    throw;
                                }
                            } else {
                                Logger.Info(String.Format("Skipped deletion of remote file {0} because of too many failed retries ({1} max={2})", filePath, retries, repoinfo.MaxDeletionRetries));
                            }
                        }
                        else
                        {
                            // New remote file, download it.
                            Logger.Info("New remote file: " + filePath);
                            success = DownloadFile(remoteDocument, localFolder);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn(String.Format("Exception while download file to {0}: {1}", filePath, Utils.ToLogString(e)));
                    success = false;
                }

                return success;
            }

            private void SetLastModifiedDate(IDocument remoteDocument, string filepath, Dictionary<string, string[]> metadata)
            {
                try
                {
                    if (remoteDocument.LastModificationDate != null)
                    {
                        File.SetLastWriteTimeUtc(filepath, (DateTime)remoteDocument.LastModificationDate);
                    }
                    else
                    {
                        string[] cmisModDate;
                        if (metadata.TryGetValue("cmis:lastModificationDate", out cmisModDate) && cmisModDate.Length == 3)
                        {
                            DateTime modDate = DateTime.Parse(cmisModDate[2]);
                            File.SetLastWriteTimeUtc(filepath, modDate);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Debug(String.Format("Failed to set last modified date for the local file: {0}", filepath), e);
                }
            }

            private void SetLastModifiedDate(IFolder remoteFolder, string folderpath, Dictionary<string, string[]> metadata)
            {
                try{
                    if (remoteFolder.LastModificationDate != null)
                    {
                        File.SetLastWriteTimeUtc(folderpath, (DateTime)remoteFolder.LastModificationDate);
                    }
                    else
                    {
                        string[] cmisModDate;
                        if (metadata.TryGetValue("cmis:lastModificationDate", out cmisModDate) && cmisModDate.Length == 3)
                        {
                            DateTime modDate = DateTime.Parse(cmisModDate[2]);
                            File.SetLastWriteTimeUtc(folderpath, modDate);
                        }
                    }
                }
                catch(Exception e)
                {
                    Logger.Debug(String.Format("Failed to set last modified date for the local folder: {0}", folderpath), e);
                }
            }

            /// <summary>
            /// Download a single file from the CMIS server.
            /// </summary>
            private bool DownloadFile(IDocument remoteDocument, string localFolder)
            {
                using (new ActivityListenerResource(activityListener))
                {
                    string fileName = remoteDocument.Name;

                    // Skip if invalid file name. See https://github.com/nicolas-raoul/CmisSync/issues/196
                    if (Utils.IsInvalidFileName(fileName))
                    {
                        Logger.Info("Skipping download of file with illegal filename: " + fileName);
                        return true;
                    }

                    try
                    {
                        DotCMIS.Data.IContentStream contentStream = null;
                        string filepath = Path.Combine(localFolder, fileName);
                        string tmpfilepath = filepath + ".sync";
                        if(database.GetOperationRetryCounter(filepath,Database.OperationType.DOWNLOAD) > repoinfo.MaxDownloadRetries)
                        {
                            Logger.Info(String.Format("Skipping download of file {0} because of too many failed ({1}) downloads",database.GetOperationRetryCounter(filepath,Database.OperationType.DOWNLOAD)));
                            return true;
                        }
                        // If there was previously a directory with this name, delete it.
                        // TODO warn if local changes inside the folder.
                        if (Directory.Exists(filepath))
                        {
                            Directory.Delete(filepath);
                        }

                        if (File.Exists(tmpfilepath))
                        {
                            DateTime? remoteDate = remoteDocument.LastModificationDate;
                            if (null == remoteDate)
                            {
                                File.Delete(tmpfilepath);
                            }
                            else
                            {
                                remoteDate = ((DateTime)remoteDate).ToUniversalTime();
                                DateTime? serverDate = database.GetDownloadServerSideModificationDate(filepath);
                                if (remoteDate != serverDate)
                                {
                                    File.Delete(tmpfilepath);
                                }
                            }
                        }

                        // Download file.
                        Boolean success = false;
                        byte[] filehash = { };
                        try
                        {
                            long? fileLength = remoteDocument.ContentStreamLength;

                            if (null == fileLength)
                            {
                                Logger.Warn("Skipping download of file with null content stream: " + fileName);
                                return true;
                            }

                            // Skip downloading the content, just go on with an empty file
                            if (0 == fileLength)
                            {
                                Logger.Info("Skipping download of file with content length zero: " + fileName);
                                using (FileStream s = File.Open(tmpfilepath, FileMode.Create))
                                {
                                    s.Close();
                                }
                                using (SHA1 sha = new SHA1CryptoServiceProvider())
                                {
                                    filehash = sha.ComputeHash(new byte[0]);
                                }
                                success = true;
                            }
                            else
                            {
                                Logger.Debug("Creating local download file: " + tmpfilepath);
                                using (Stream file = new FileStream(tmpfilepath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                                using (SHA1 hashAlg = new SHA1Managed())
                                {
                                    if (repoinfo.ChunkSize <= 0 )
                                    {
                                        using (CryptoStream hashstream = new CryptoStream(file, hashAlg, CryptoStreamMode.Write))
                                        using (LoggingStream logstream = new LoggingStream(hashstream, "Download progress", fileName, (long) fileLength))
                                        {
                                            contentStream = remoteDocument.GetContentStream();
                                            // If this file does not have a content stream, ignore it.
                                            // Even 0 bytes files have a contentStream.
                                            // null contentStream sometimes happen on IBM P8 CMIS server, not sure why.
                                            if (contentStream == null)
                                            {
                                                Logger.Warn("Skipping download of file with null content stream: " + fileName);
                                                return true;
                                            }

                                            using (contentStream.Stream)
                                            {
                                                byte[] buffer = new byte[8 * 1024];
                                                int len;
                                                while ((len = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
                                                {
                                                    logstream.Write(buffer, 0, len);
                                                }
                                                success = true;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        database.SetDownloadServerSideModificationDate(filepath, remoteDocument.LastModificationDate);
                                        byte[] buffer = new byte[8 * 1024];
                                        int len;
                                        while ((len = file.Read(buffer, 0, buffer.Length)) > 0)
                                        {
                                            hashAlg.TransformBlock(buffer, 0, len, buffer, 0);
                                        }
                                        using (CryptoStream hashstream = new CryptoStream(file, hashAlg, CryptoStreamMode.Write))
                                        {
                                            success = DownloadStreamInChunks(tmpfilepath, hashstream, remoteDocument);
                                        }
                                    }
                                    filehash = hashAlg.Hash;
                                }
                            }
                        }
                        catch (ObjectDisposedException ex)
                        {
                            Logger.Error(String.Format("Download aborted: {0}", fileName), ex);
                            return false;
                        }
                        catch (System.IO.DirectoryNotFoundException ex)
                        {
                            Logger.Warn(String.Format("Download failed because of a missing folder in the file path: {0}" , ex.Message ));
                            success = false;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Download failed: " + fileName + " " + ex);
                            success = false;
                            Logger.Debug("Removing temp download file: "+ tmpfilepath);
                            File.Delete(tmpfilepath);
                            success = false;
                            if(ex is CmisBaseException)
                            {
                                database.SetOperationRetryCounter(filepath,database.GetOperationRetryCounter(filepath,Database.OperationType.DOWNLOAD)+1,Database.OperationType.DOWNLOAD);
                            }
                        }

                        if (success)
                        {
                            Logger.Info(String.Format("Downloaded remote object({0}): {1}", remoteDocument.Id, fileName));
                            // TODO Control file integrity by using hash compare?

                            // Get metadata.
                            Dictionary<string, string[]> metadata = null;
                            try
                            {
                                metadata = FetchMetadata(remoteDocument);
                            }
                            catch (Exception e)
                            {
                                Logger.Info("Exception while fetching metadata: " + fileName + " " + Utils.ToLogString(e));
                                // Remove temporary local document to avoid it being considered a new document.
                                Logger.Debug("Removing local temp file: " + tmpfilepath);
                                File.Delete(tmpfilepath);
                                return false;
                            }

                            // If file exists, check it.
                            if (File.Exists(filepath))
                            {
                                if (database.LocalFileHasChanged(filepath))
                                {
                                    Logger.Info("Conflict with file: " + fileName + ", backing up locally modified version and downloading server version");
                                    // Rename locally modified file.
                                    //String ext = Path.GetExtension(filePath);
                                    //String filename = Path.GetFileNameWithoutExtension(filePath);
                                    String dir = Path.GetDirectoryName(filepath);

                                    String newFileName = Utils.SuffixIfExists(Path.GetFileNameWithoutExtension(filepath) + "_" + repoinfo.User + "-version");
                                    String newFilePath = Path.Combine(dir, newFileName);
                                    Logger.Debug(String.Format("Moving local file {0} file to new file {1}", filepath, newFilePath));
                                    File.Move(filepath, newFilePath);
                                    Logger.Debug(String.Format("Moving temporary local download file {0} to target file {1}", tmpfilepath, filepath));
                                    File.Move(tmpfilepath, filepath);
                                    SetLastModifiedDate(remoteDocument, filepath, metadata);
                                    repo.OnConflictResolved();
                                    // TODO move to OS-dependant layer
                                    //System.Windows.Forms.MessageBox.Show("Someone modified a file at the same time as you: " + filePath
                                    //    + "\n\nYour version has been saved with a '_your-version' suffix, please merge your important changes from it and then delete it.");
                                    // TODO show CMIS property lastModifiedBy
                                }
                                else
                                {
                                    Logger.Debug("Removing local file: " + filepath);
                                    File.Delete(filepath);
                                    Logger.Debug(String.Format("Moving temporary local download file {0} to target file {1}", tmpfilepath, filepath));
                                    File.Move(tmpfilepath, filepath);
                                    SetLastModifiedDate(remoteDocument, filepath, metadata);
                                    
                                }
                            }
                            else
                            {
                                Logger.Debug(String.Format("Moving temporary local download file {0} to target file {1}", tmpfilepath, filepath));
                                File.Move(tmpfilepath, filepath);
                                SetLastModifiedDate(remoteDocument, filepath, metadata);
                            }

                            // Create database entry for this file.
                            database.AddFile(filepath, remoteDocument.Id, remoteDocument.LastModificationDate, metadata, filehash);

                            Logger.Debug("Added to database: " + fileName);
                        }

                        return success;
                    }
                    catch (IOException e)
                    {
                        Logger.Warn("Exception while file operation: " + Utils.ToLogString(e));
                        return false;
                    }
                }
            }


            private bool ResumeUploadFile(string filePath, IDocument remoteDocument)
            {
                Logger.Debug("Resuming Upload: "+ filePath + " to remote document: " + remoteDocument.Name);
                if (repoinfo.ChunkSize <= 0)
                {
                    return UpdateFile(filePath, remoteDocument);
                }

                if (database.LocalFileHasChanged(filePath))
                {
                    return UpdateFile(filePath, remoteDocument);
                }

                using (Stream file = File.OpenRead(filePath))
                {
                    file.Position = (long)remoteDocument.ContentStreamLength;
                    return UploadStreamInTrunk(filePath, file, remoteDocument);
                }

                //return false;
            }


            private bool UploadStreamInTrunk(string filePath, Stream fileStream, IDocument remoteDocument)
            {
                if (repoinfo.ChunkSize <= 0)
                {
                    return false;
                }

                string fileName = remoteDocument.Name;
                for (long offset = fileStream.Position; offset < fileStream.Length; offset += repoinfo.ChunkSize)
                {
                    bool isLastTrunk = false;
                    if (offset + repoinfo.ChunkSize >= fileStream.Length)
                    {
                        isLastTrunk = true;
                    }
                    Logger.Debug(String.Format("Uploading next chunk (size={1}) of {0}: {2} of {3} finished({4}%)", fileName, repoinfo.ChunkSize, offset, fileStream.Length, 100*offset / fileStream.Length));
                    using (ChunkedStream chunkstream = new ChunkedStream(fileStream, repoinfo.ChunkSize))
                    {
                        chunkstream.ChunkPosition = offset;

                        ContentStream contentStream = new ContentStream();
                        contentStream.FileName = fileName;
                        contentStream.MimeType = MimeType.GetMIMEType(fileName);
                        contentStream.Length = repoinfo.ChunkSize;
                        if (isLastTrunk)
                        {
                            contentStream.Length = fileStream.Length - offset;
                        }
                        contentStream.Stream = chunkstream;
                        lock (disposeLock)
                        {
                            if (disposed)
                            {
                                throw new ObjectDisposedException("Uploading");
                            }
                            try
                            {
                                remoteDocument.AppendContentStream(contentStream, isLastTrunk);
                                Logger.Debug("Response of the server: " + offset.ToString());
                                database.SetFileServerSideModificationDate(filePath, remoteDocument.LastModificationDate);
                            }
                            catch (Exception ex)
                            {
                                Logger.Fatal("Upload failed: " + ex);
                                return false;
                            }
                        }
                    }
                }
                return true;
            }


            /// <summary>
            /// Upload a single file to the CMIS server.
            /// </summary>
            private bool UploadFile(string filePath, IFolder remoteFolder)
            {
                using (new ActivityListenerResource(activityListener))
                {
                    long retries = database.GetOperationRetryCounter(filePath, Database.OperationType.UPLOAD);
                    if(retries > this.repoinfo.MaxUploadRetries) {
                        Logger.Info(String.Format("Skipping uploading file absent on repository, because of too many failed retries({0}): {1}", retries, filePath));
                        return true;
                    }
                    try{
                        IDocument remoteDocument = null;
                        Boolean success = false;
                        byte[] filehash = { };
                        try
                        {
                            Logger.Info("Uploading: " + filePath);

                            // Prepare properties
                            string fileName = Path.GetFileName(filePath);
                            Dictionary<string, object> properties = new Dictionary<string, object>();
                            properties.Add(PropertyIds.Name, fileName);
                            properties.Add(PropertyIds.ObjectTypeId, "cmis:document");
                            properties.Add(PropertyIds.CreationDate, ((long)(File.GetCreationTimeUtc(filePath) - new DateTime(1970, 1, 1)).TotalMilliseconds).ToString());

                            // Prepare content stream
                            using (Stream file = File.OpenRead(filePath))
                            {
                                if (repoinfo.ChunkSize <= 0 || file.Length <= repoinfo.ChunkSize)
                                {
                                    using (SHA1 hashAlg = new SHA1Managed())
                                    using (CryptoStream hashstream = new CryptoStream(file, hashAlg, CryptoStreamMode.Read))
                                    using(LoggingStream logstream = new LoggingStream(hashstream, "Upload progress", fileName, file.Length))
                                    {
                                        ContentStream contentStream = new ContentStream();
                                        contentStream.FileName = fileName;
                                        contentStream.MimeType = MimeType.GetMIMEType(fileName);
                                        contentStream.Length = file.Length;
                                        contentStream.Stream = logstream;

                                        // Upload
                                        try
                                        {
                                            Logger.Debug(String.Format("CMIS::CreateDocument(Properties(Name={0}, ObjectType={1})," +
                                                                       "ContentStream(FileName={0}, MimeType={2}, Length={3})",
                                                                   fileName,"cmis:document", contentStream.MimeType,contentStream.Length));
                                            try
                                            {
                                                remoteDocument = remoteFolder.CreateDocument(properties, null, null);
                                                Logger.Debug(String.Format("CMIS::Document Id={0} Name={1}",
                                                                           remoteDocument.Id, fileName));
                                            } catch(Exception e) {
                                                string reason = Utils.IsValidISO(fileName)?String.Empty:" Reason: Upload perhaps failed because of an invalid UTF-8 character";
                                                Logger.Info(String.Format("Could not create the remote document {0} as target for local document {1}{2}", fileName, filePath, reason));
                                                throw;
                                            }
                                            remoteDocument.SetContentStream(contentStream, false);
                                            filehash = hashAlg.Hash;
                                            success = true;
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Fatal("Upload failed: " + filePath + " " + ex);
                                            throw;
                                        }
                                    }
                                }
                                else
                                {
                                    filehash = new SHA1Managed().ComputeHash(file);
                                    file.Position = 0;

                                    ContentStream contentStream = new ContentStream();
                                    contentStream.FileName = fileName;
                                    contentStream.MimeType = MimeType.GetMIMEType(fileName);
                                    contentStream.Length = 0;
                                    contentStream.Stream = new MemoryStream(0);
                                    Logger.Debug("CMIS::CreateDocument()");
                                    lock (disposeLock)
                                    {
                                        if (disposed)
                                        {
                                            throw new ObjectDisposedException("Uploading");
                                        }
                                        remoteDocument = remoteFolder.CreateDocument(properties, contentStream, null);
                                        Logger.Debug(String.Format("CMIS::Document Id={0} Name={1}",
                                                                       remoteDocument.Id, fileName));
                                        Dictionary<string, string[]> metadata = FetchMetadata(remoteDocument);
                                        database.AddFile(filePath, remoteDocument.Id, remoteDocument.LastModificationDate, metadata, filehash);
                                    }
                                    success = UploadStreamInTrunk(filePath, file, remoteDocument);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            if (e is FileNotFoundException ||
                                e is IOException)
                            {
                                Logger.Warn("File deleted while trying to upload it, reverting.");
                                // File has been deleted while we were trying to upload/checksum/add.
                            // This can typically happen in Windows Explore when creating a new text file and giving it a name.
                            // In this case, revert the upload.
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

                        // Metadata.
                        if (success)
                        {
                            Logger.Info("Uploaded: " + filePath);

                            // Get metadata. Some metadata has probably been automatically added by the server.
                            Dictionary<string, string[]> metadata = FetchMetadata(remoteDocument);

                            // Create database entry for this file.
                            database.AddFile(filePath, remoteDocument.Id, remoteDocument.LastModificationDate, metadata, filehash);
                            SetLastModifiedDate(remoteDocument, filePath, metadata);
                        }
                        return success;
                    }
                    catch(Exception e)
                    {
                        retries++;
                        database.SetOperationRetryCounter(filePath, retries, Database.OperationType.UPLOAD);
                        Logger.Warn(String.Format("Uploading of {0} failed {1} times: ", filePath, retries), e);
                        return false;
                    }
                }
            }


            /// <summary>
            /// Upload folder recursively.
            /// After execution, the hierarchy on server will be: .../remoteBaseFolder/localFolder/...
            /// </summary>
            private bool UploadFolderRecursively(IFolder remoteBaseFolder, string localFolder)
            {
                // Create remote folder.
                Dictionary<string, object> properties = new Dictionary<string, object>();
                properties.Add(PropertyIds.Name, Path.GetFileName(localFolder));
                properties.Add(PropertyIds.ObjectTypeId, "cmis:folder");
                properties.Add(PropertyIds.CreationDate, "");
                properties.Add(PropertyIds.LastModificationDate,"");
                IFolder folder = null;
                try
                {
                    Logger.Debug(String.Format("Creating remote folder {0} for local folder {1}", Path.GetFileName(localFolder), localFolder));
                    folder = remoteBaseFolder.CreateFolder(properties);
                    Logger.Debug(String.Format("Created remote folder {0}({1}) for local folder {2}", Path.GetFileName(localFolder), folder.Id ,localFolder));
                }
                catch (CmisNameConstraintViolationException)
                {
                    foreach (ICmisObject cmisObject in remoteBaseFolder.GetChildren())
                    {
                        if (cmisObject.Name == Path.GetFileName(localFolder))
                        {
                            folder = cmisObject as IFolder;
                        }
                    }
                    if (folder == null)
                    {
                        Logger.Warn("Remote file conflict with local folder " + Path.GetFileName(localFolder));
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(String.Format("Exception when create remote folder for local folder {0}: {1}", localFolder, Utils.ToLogString(ex)));
                    return false;
                }

                // Create database entry for this folder
                // TODO Add metadata
                database.AddFolder(localFolder, folder.Id, folder.LastModificationDate);
                SetLastModifiedDate(folder,localFolder, FetchMetadata(folder));
                bool success = true;
                try
                {
                    // Upload each file in this folder.
                    foreach (string file in Directory.GetFiles(localFolder))
                    {
                        if (Utils.WorthSyncing(file.Substring(file.LastIndexOf(Path.DirectorySeparatorChar)+1)))
                        {
                            Logger.Debug(String.Format("Invoke upload file {0} of folder {1}", file, localFolder));
                            success = UploadFile(file, folder) && success;
                        }
                    }

                    // Recurse for each subfolder in this folder.
                    foreach (string subfolder in Directory.GetDirectories(localFolder))
                    {
                        string path = subfolder.Substring(repoinfo.TargetDirectory.Length);
                        path = path.Replace("\\\\","/");
                        if (!Utils.IsInvalidFolderName(subfolder) && !repoinfo.isPathIgnored(path))
                        {
                            Logger.Debug("Start recursive upload of folder: " + subfolder);
                            success = UploadFolderRecursively(folder, subfolder) && success;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e is System.IO.DirectoryNotFoundException ||
                        e is IOException)
                    {
                        Logger.Warn("Folder deleted while trying to upload it, reverting.");
                        // Folder has been deleted while we were trying to upload/checksum/add.
                        // In this case, revert the upload.
                        folder.DeleteTree(true, null, true);
                    }
                    else
                    {
                        Logger.Warn("Exception on recursiv upload of folder: " + localFolder);
                        return false;
                    }
                }

                return success;
            }


            /// <summary>
            /// Upload new version of file.
            /// </summary>
            private bool UpdateFile(string filePath, IDocument remoteFile)
            {
                long retries = database.GetOperationRetryCounter(filePath, Database.OperationType.UPLOAD);
                if(retries >= repoinfo.MaxUploadRetries)
                {
                    Logger.Info(String.Format("Skipping updating file content on repository, because of too many failed retries({0}): {1}", retries, filePath));
                    return true;
                }

                try
                {
                    bool success = false;
                    Logger.Info("## Updating " + filePath);
                    using (Stream localfile = File.OpenRead(filePath))
                    {
                        // Ignore files with null or empty content stream.
                        if ((localfile == null) && (localfile.Length == 0))
                        {
                            Logger.Info("Skipping update of file with null or empty content stream: " + filePath);
                            return true;
                        }

                        if (repoinfo.ChunkSize <= 0)
                        {
                            using(LoggingStream logstream = new LoggingStream(localfile, "Updating ContentStream", filePath, localfile.Length)) {
                                ContentStream contentStream = new ContentStream();
                                contentStream.FileName = remoteFile.Name;
                                contentStream.Length = localfile.Length;
                                contentStream.MimeType = MimeType.GetMIMEType(contentStream.FileName);
                                contentStream.Stream = logstream;
                                Logger.Debug(String.Format("before SetContentStream to remote object ({0})", remoteFile.Id));

                                remoteFile.SetContentStream(contentStream, true, true);

                                Logger.Debug(String.Format("after SetContentStream to remote object ({0})", remoteFile.Id));
                                Logger.Info(String.Format("## Updated {0} ({1})", filePath, remoteFile.Id));
                                success = true;
                            }
                        }
                        else
                        {
                            Logger.Debug(String.Format("before SetContentStream to remote object ({0})", remoteFile.Id));

                            for (long offset = 0; offset < localfile.Length; offset += repoinfo.ChunkSize)
                            {
                                bool isLastChunk = false;
                                if (offset + repoinfo.ChunkSize >= localfile.Length)
                                {
                                    isLastChunk = true;
                                }
                                using (ChunkedStream chunkstream = new ChunkedStream(localfile, repoinfo.ChunkSize))
                                {
                                    ContentStream contentStream = new ContentStream();
                                    contentStream.FileName = remoteFile.Name;
                                    contentStream.Length = repoinfo.ChunkSize;
                                    if (isLastChunk)
                                    {
                                        contentStream.Length = localfile.Length - offset;
                                    }
                                    contentStream.MimeType = MimeType.GetMIMEType(contentStream.FileName);
                                    contentStream.Stream = chunkstream;

                                    // Upload
                                    if (offset == 0)
                                    {
                                        remoteFile.SetContentStream(contentStream, true);
                                    }
                                    else
                                    {
                                        remoteFile.AppendContentStream(contentStream, isLastChunk);
                                    }
                                }
                            }

                            Logger.Debug(String.Format("after SetContentStream to remote object ({0})",remoteFile.Id));
                            Logger.Info(String.Format("## Updated {0} ({1})", filePath, remoteFile.Id));
                            success = true;
                        }
                    }

                    if (success)
                    {
                        // Update timestamp in database.
                        database.SetFileServerSideModificationDate(filePath, ((DateTime)remoteFile.LastModificationDate).ToUniversalTime());

                        // Update checksum
                        database.RecalculateChecksum(filePath);

                        // TODO Update metadata?
                    }

                    return success;
                }
                catch (Exception e)
                {
                    retries++;
                    database.SetOperationRetryCounter(filePath, retries, Database.OperationType.UPLOAD);
                    Logger.Warn(String.Format("Updating content of {0} failed {1} times: ", filePath, retries), e);
                    return false;
                }
            }

            /// <summary>
            /// Upload new version of file content.
            /// </summary>
            private bool UpdateFile(string filePath, IFolder remoteFolder)
            {
                using (new ActivityListenerResource(activityListener))
                {
                    Logger.Info("# Updating " + filePath);

                    // Find the document within the folder.
                    string fileName = Path.GetFileName(filePath);
                    IDocument document = null;
                    bool found = false;
                    foreach (ICmisObject obj in remoteFolder.GetChildren())
                    {
                        if (null != (document = obj as IDocument))
                        {
                            if (document.Name == fileName)
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    // If not found, it means the document has been deleted.
                    if (!found)
                    {
                        Logger.Info(filePath + " not found on server, must be uploaded instead of updated");
                        return UploadFile(filePath, remoteFolder);
                    }

                    // Update the document itself.
                    bool success = UpdateFile(filePath, document);

                    Logger.Info("# Updated " + filePath);

                    return success;
                }
            }


            /// <summary>
            /// Move folder from local filesystem and database.
            /// </summary>
            private void MoveFolderLocally(string oldFolderPath, string newFolderPath)
            {
                if (!Directory.Exists(oldFolderPath))
                {
                    return;
                }

                if (!Directory.Exists(newFolderPath))
                {
                    Directory.Move(oldFolderPath, newFolderPath);
                    database.MoveFolder(oldFolderPath, newFolderPath);
                    return;
                }

                foreach (FileInfo file in new DirectoryInfo(oldFolderPath).GetFiles())
                {
                    string oldFilePath = Path.Combine(oldFolderPath, file.Name);
                    string newFilePath = Path.Combine(newFolderPath, file.Name);
                    if (File.Exists(newFilePath))
                    {
                        File.Delete(oldFilePath);
                        database.RemoveFile(oldFilePath);
                    }
                    else
                    {
                        File.Move(oldFilePath, newFilePath);
                        database.MoveFile(oldFilePath, newFilePath);
                    }
                }

                foreach (DirectoryInfo folder in new DirectoryInfo(oldFolderPath).GetDirectories())
                {
                    MoveFolderLocally(Path.Combine(oldFolderPath, folder.Name), Path.Combine(newFolderPath, folder.Name));
                }

                Directory.Delete(oldFolderPath, true);
                database.RemoveFolder(oldFolderPath);

                return;
            }


            /// <summary>
            /// Remove folder from local filesystem and database.
            /// </summary>
            private bool RemoveFolderLocally(string folderPath)
            {
                // Folder has been deleted on server, delete it locally too.
                try
                {
                    Logger.Info("Removing remotely deleted folder: " + folderPath);
                    Directory.Delete(folderPath, true);
                }
                catch (IOException e)
                {
                    Logger.Warn(String.Format("Exception while delete tree {0}: {1}", folderPath, Utils.ToLogString(e)));
                    return false;
                }

                // Delete folder from database.
                if (!Directory.Exists(folderPath))
                {
                    database.RemoveFolder(folderPath);
                }

                return true;
            }

            /// <summary>
            /// Retrieve the CMIS metadata of a document.
            /// </summary>
            /// <returns>a dictionary in which each key is a type id and each value is a couple indicating the mode ("readonly" or "ReadWrite") and the value itself.</returns>
            private Dictionary<string, string[]> FetchMetadata(ICmisObject o)
            {
                Dictionary<string, string[]> metadata = new Dictionary<string, string[]>();

                IObjectType typeDef = session.GetTypeDefinition(o.ObjectType.Id/*"cmis:document" not Name FullName*/); // TODO cache
                IList<IPropertyDefinition> propertyDefs = typeDef.PropertyDefinitions;

                // Get metadata.
                foreach (IProperty property in o.Properties)
                {
                    // Mode
                    string mode = "readonly";
                    foreach (IPropertyDefinition propertyDef in propertyDefs)
                    {
                        if (propertyDef.Id.Equals("cmis:name"))
                        {
                            Updatability updatability = propertyDef.Updatability;
                            mode = updatability.ToString();
                        }
                    }

                    // Value
                    if (property.IsMultiValued)
                    {
                        metadata.Add(property.Id, new string[] { property.DisplayName, mode, property.ValuesAsString });
                    }
                    else
                    {
                        metadata.Add(property.Id, new string[] { property.DisplayName, mode, property.ValueAsString });
                    }
                }

                return metadata;
            }
        }
    }
}
