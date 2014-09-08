using CmisSync.Lib.Cmis;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;
using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using CmisSync.Lib.Events;
using CmisSync.Lib.Database;

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
            /// <summary>
            /// Log.
            /// </summary>
            private static readonly ILog Logger = LogManager.GetLogger(typeof(SynchronizedFolder));


            /// <summary>
            /// Interval for which sync will wait while paused before retrying sync.
            /// </summary>
            private static readonly int SYNC_SUSPEND_SLEEP_INTERVAL = 1 * 1000; //five seconds


            /// <summary>
            /// An object for locking the sync method (one thread at a time can run sync).
            /// </summary>
            private Object syncLock = new Object();


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
            /// Example: "/User Homes/nicolas.raoul/demos"
            /// </summary>
            private string remoteFolderPath;


            /// <summary>
            /// Syncing lock.
            /// true if syncing (or pause) is being performed right now.
            /// TODO use is_syncing variable in parent
            /// </summary>
            private bool syncing;


            /// <summary>
            /// Whether sync is actually being in pause right now.
            /// This is different from CmisRepo.Status, which means "paused, or will be paused as soon as possible"
            /// </summary>
            private bool suspended = false;


            /// <summary>
            /// Listener we inform about activity (used by spinner).
            /// </summary>
            private IActivityListener activityListener;


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
            private Database.Database database;


            /// <summary>
            /// Configuration of the CmisSync synchronized folder, as defined in the XML configuration file.
            /// </summary>
            private RepoInfo repoinfo;


            /// <summary>
            /// Link to parent object.
            /// </summary>
            private RepoBase repo;

            
            /// <summary>
            /// EventQueue
            /// </summary>
            public SyncEventQueue Queue {get; private set;}
            

            /// <summary>
            /// Set for first sync.
            /// </summary>
            private bool firstSync = false;


            /// <summary>
            /// Background worker for sync.
            /// </summary>
            private BackgroundWorker syncWorker;


            /// <summary>
            /// Event to notify that the sync has completed.
            /// </summary>
            private AutoResetEvent autoResetEvent = new AutoResetEvent(true);


            /// <summary>
            ///  Constructor for Repo (at every launch of CmisSync)
            /// </summary>
            public SynchronizedFolder(RepoInfo repoInfo, RepoBase repoCmis, IActivityListener activityListener)
            {
                this.activityListener = activityListener;

                if (null == repoInfo || null == repoCmis)
                {
                    throw new ArgumentNullException("repoInfo");
                }

                this.repo = repoCmis;
                this.repoinfo = repoInfo;

                suspended = this.repoinfo.IsSuspended;

                Queue = repoCmis.Queue;

                // Database is the user's AppData/Roaming
                database = new Database.Database(repoinfo.CmisDatabase);

                // Get path on remote repository.
                remoteFolderPath = repoInfo.RemotePath;

                cmisParameters = new Dictionary<string, string>();
                UpdateCmisParameters();
                if (Logger.IsInfoEnabled)
                {
                    foreach (string ignoredFolder in repoInfo.getIgnoredPaths())
                    {
                        Logger.Info("The folder \"" + ignoredFolder + "\" will be ignored");
                    }
                }
                repoCmis.EventManager.AddEventHandler(new GenericSyncEventHandler<RepoConfigChangedEvent>(10, RepoInfoChanged));

                syncWorker = new BackgroundWorker();
                syncWorker.WorkerSupportsCancellation = true;
                syncWorker.DoWork += new DoWorkEventHandler(
                    delegate(Object o, DoWorkEventArgs args)
                    {
                        bool syncFull = (bool)args.Argument;
                        try
                        {
                            Sync(syncFull);
                        }
                        catch (OperationCanceledException e)
                        {
                            Logger.Info(e.Message);
                        }
                        catch (CmisPermissionDeniedException e)
                        {
                            repo.OnSyncError(new PermissionDeniedException("Authentication failed.", e));
                        }
                        catch (Exception e)
                        {
                            repo.OnSyncError(new BaseException(e));
                        }
                        finally
                        {
                            SyncComplete(syncFull);
                        }
                    }
                );
            }


            /// <summary>
            /// This method is called, every time the config changes
            /// </summary>
            private bool RepoInfoChanged(ISyncEvent e)
            {
                if (e is RepoConfigChangedEvent)
                {
                    repoinfo = (e as RepoConfigChangedEvent).RepoInfo;
                    UpdateCmisParameters();
                    ForceFullSyncAtNextSync();
                }
                return false;
            }

            /// <summary>
            /// Loads the CmisParameter from repoinfo. If repoinfo has been changed, this method sets the new informations for the next session
            /// </summary>
            private void UpdateCmisParameters()
            {
                cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
                cmisParameters[SessionParameter.AtomPubUrl] = repoinfo.Address.ToString();
                cmisParameters[SessionParameter.User] = repoinfo.User;
                cmisParameters[SessionParameter.Password] = repoinfo.Password.ToString();
                cmisParameters[SessionParameter.RepositoryId] = repoinfo.RepoID;
                // Sets the Connect Timeout to infinite
                cmisParameters[SessionParameter.ConnectTimeout] = "60000"; // One minute
                // Sets the Read Timeout to infinite
                cmisParameters[SessionParameter.ReadTimeout] = "60000"; // One Minute
            }


            /// <summary>
            ///  Update Settings.
            /// </summary>
            public void UpdateSettings(RepoInfo repoInfo)
            {
                //Cancel sync before settings update.
                CancelSync();

                //Set the cmis session to null
                session = null;

                this.repoinfo = repoInfo;
                cmisParameters[SessionParameter.Password] = repoInfo.Password.ToString();
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
                    filters.Add("cmis:lastModifiedBy");
                    filters.Add("cmis:path");
                    session.DefaultContext = session.CreateOperationContext(filters, false, true, false, IncludeRelationshipsFlag.None, null, true, null, true, 100);
            }


            /// <summary>
            /// Whether this folder's synchronization is running right now.
            /// </summary>
            public bool isSyncing()
            {
                return this.syncing;
            }


            /// <summary>
            /// Synchronize between CMIS folder and local folder.
            /// </summary>
            public bool IsSyncing()
            {
                return syncWorker.IsBusy;
            }


            /// <summary>
            /// Whether this folder's synchronization is suspended right now.
            /// </summary>
            public bool isSuspended()
            {
                return this.suspended;
            }


            /// <summary>
            /// Synchronize between CMIS folder and local folder.
            /// </summary>
            public void Sync()
            {
                Sync(true);
            }

            
            /// <summary>
            /// Track whether a full sync is done
            /// </summary>
            private bool syncFull = false;


            /// <summary>
            /// Forces the full sync independent of FS events or Remote events.
            /// </summary>
            public void ForceFullSync()
            {
                ForceFullSyncAtNextSync();
                Sync();
            }

            /// <summary>
            /// Forces the full sync at next sync.
            /// This can be used to ensure a full sync if fs or remote events where lost.
            /// </summary>
            public void ForceFullSyncAtNextSync()
            {
                syncFull = false;
            }


            /// <summary>
            /// Synchronize between CMIS folder and local folder.
            /// </summary>
            public void Sync(bool syncFull)
            {
                lock (syncLock)
                {
                    autoResetEvent.Reset();
                    repo.OnSyncStart(syncFull);

                    // If not connected, connect.
                    if (session == null)
                    {
                        Connect();
                        firstSync = true;
                    }
                    else {
                        //  Force to reset the cache for each Sync
                        session.Clear();
                    }

                    SleepWhileSuspended();


                    // Add ACL in the context, or ACL is null
                    // OperationContext context = new OperationContext();
                    // context.IncludeAcls = true;

                    // IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath, context);
                    IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath);

                    string localFolder = repoinfo.TargetDirectory;

                    if (firstSync)
                    {
                        Logger.Debug("Invoke a full crawl sync");
                        CrawlSync(remoteFolder, localFolder);
                        firstSync = false;
                    }

                    if ( false /* ChangeLog disabled for now TODO */ && ChangeLogCapability)
                    {
                        Logger.Debug("Invoke a remote change log sync");
                        ChangeLogSync(remoteFolder);
                        if(repo.Watcher.GetChangeList().Count > 0)
                        {
                            Logger.Debug("Changes on the local file system detected => starting crawl sync");
                            repo.Watcher.RemoveAll();
                            // TODO if(!CrawlSync(remoteFolder,localFolder))
                            // TODO    repo.Watcher.InsertChange("/", Watcher.ChangeTypes.Changed);
                        }
                    }
                    else
                    {
                        //  have to crawl remote
                        Logger.Debug("Invoke a remote crawl sync");
                        repo.Watcher.RemoveAll();
                        CrawlSync(remoteFolder, localFolder);
                    }

                }
            }

            /// <summary>
            /// Synchronize has completed.
            /// </summary>
            public void SyncComplete(bool syncFull)
            {
                lock (syncLock)
                {
                    try
                    {
                        repo.OnSyncComplete(syncFull);
                    }
                    finally
                    {
                        autoResetEvent.Set();
                    }
                }
            }

            /// <summary>
            /// Sync on the current thread.
            /// </summary>
            /// <param name="syncFull"></param>
            public void SyncInNotBackground(bool syncFull)
            {
                if (IsSyncing())
                {
                    Logger.Debug("Sync already running in background: " + repoinfo.TargetDirectory);
                    return;
                }

                try
                {
                    Sync(syncFull);
                }
                catch (CmisPermissionDeniedException e)
                {
                    repo.OnSyncError(new PermissionDeniedException("Authentication failed.", e));
                }
                catch (Exception e)
                {
                    repo.OnSyncError(new BaseException(e));
                }
                finally
                {
                    SyncComplete(syncFull);
                }
            }

            /// <summary>
            /// Sync in the background.
            /// </summary>
            public void SyncInBackground(bool syncFull)
            {
                if (IsSyncing())
                {
                    Logger.Debug("Sync already running in background: " + repoinfo.TargetDirectory);
                    return;
                }

                syncWorker.RunWorkerAsync(syncFull);
            }

            /// <summary>
            /// Cancel a running sync (does nothing if sync thread is stopped).
            /// </summary>
            public void CancelSync()
            {
                if (IsSyncing())
                {
                    Logger.Info("Cancel Sync Requested...");
                    syncWorker.CancelAsync();
                    Logger.Debug("Wait for thread to complete...");
                    autoResetEvent.WaitOne();
                    Logger.Debug("...cancel completed.");
                }
            }

            /// <summary>
            /// Handle CMIS Exception.
            /// </summary>
            private void ProcessRecoverableException(string logMessage, Exception exception)
            {
                bool recoverable;
                // Exceptions: http://docs.oasis-open.org/cmis/CMIS/v1.0/cs01/cmis-spec-v1.0.html#_Toc243905433
                if (exception is CmisInvalidArgumentException)
                {
                    // One or more of the input parameters to the service method is missing or invalid (any method)
                    recoverable = true;
                }
                else if (exception is CmisObjectNotFoundException)
                {
                    // The service call has specified an object that does not exist in the Repository (any method)
                    recoverable = true;
                }
                else if (exception is CmisNotSupportedException)
                {
                    // The service method invoked requires an optional capability not supported by the repository (any method)
                    recoverable = false;
                }
                else if (exception is CmisPermissionDeniedException)
                {
                    // The caller of the service method does not have sufficient permissions to perform the operation (any method)
                    recoverable = false;
                }
                else if (exception is CmisRuntimeException)
                {
                    // Any other cause not expressible by another CMIS exception (any method)
                    recoverable = false;
                }
                else if (exception is CmisConstraintException)
                {
                    // The operation violates a Repository- or Object-level constraint defined in the CMIS domain model (write methods)
                    recoverable = true;
                }
                else if (exception is CmisContentAlreadyExistsException)
                {
                    // The operation attempts to set the content stream for a Document that already has a content stream without explicitly specifying the �overwriteFlag� parameter (setContentStream method)
                    recoverable = true;
                }
                else if (exception is CmisFilterNotValidException)
                {
                    // The property filter or rendition filter input to the operation is not valid (read methods)
                    recoverable = true;
                }
                else if (exception is CmisNameConstraintViolationException)
                {
                    // The repository is not able to store the object that the user is creating/updating due to a name constraint violation (write methods)
                    recoverable = true;
                }
                else if (exception is CmisStorageException)
                {
                    // The repository is not able to store the object that the user is creating/updating due to an internal storage problem (write methods)
                    recoverable = true;
                }
                else if (exception is CmisStreamNotSupportedException)
                {
                    // The operation is attempting to get or set a contentStream for a Document whose Object-type specifies that a content stream is not allowed for Document�s of that type (write methods)
                    recoverable = true;
                }
                else if (exception is CmisUpdateConflictException)
                {
                    // The operation is attempting to update an object that is no longer current (as determined by the repository) (write methods)
                    recoverable = true;
                }
                else if (exception is CmisVersioningException)
                {
                    // The operation is attempting to perform an action on a non-current version of a Document that cannot be performed on a non-current version.
                    recoverable = true;
                }
                else if (exception is CmisConnectionException)
                {
                    // Client unable to connect to server
                    recoverable = false;
                }
                else if (exception is IOException)
                {
                    // IO Exception
                    recoverable = true;
                }
                else if (exception is UnauthorizedAccessException)
                {
                    // Unable to access file/directory
                    recoverable = true;
                }
                else if (exception is ArgumentException)
                {
                    // File contains characters not valid for .NET, for instance carriage return.
                    recoverable = true;
                }
                else
                {
                    // All other errors...
                    recoverable = false;
                }

                Logger.Error(logMessage, exception);

                if (!recoverable)
                {
                    throw exception;
                }
            }

            /// <summary>
            /// Download all content from a CMIS folder.
            /// </summary>
            private void RecursiveFolderCopy(IFolder remoteFolder, string localFolder)
            {
                SleepWhileSuspended();

                IItemEnumerable<ICmisObject> children;
                try
                {
                    children = remoteFolder.GetChildren();
                }
                catch (CmisBaseException e)
                {
                    ProcessRecoverableException("Could not get children objects: " + remoteFolder.Path, e);
                    return;
                }

                // List all children.
                foreach (ICmisObject cmisObject in children)
                {
                    if (cmisObject is DotCMIS.Client.Impl.Folder)
                    {
                        IFolder remoteSubFolder = (IFolder)cmisObject;
                        // string localSubFolder = Path.Combine(localFolder, PathRepresentationConverter.RemoteToLocal(cmisObject.Name));

                        var localSubFolderItem = database.GetFolderSyncItemFromRemotePath(remoteSubFolder.Path);
                        if (null == localSubFolderItem)
                        {
                            localSubFolderItem = SyncItemFactory.CreateFromRemotePath(remoteSubFolder.Path, repoinfo);
                        }

                        if (Utils.WorthSyncing(localFolder, PathRepresentationConverter.RemoteToLocal(remoteSubFolder.Name), repoinfo))
                        {
                            DownloadDirectory(remoteSubFolder, localSubFolderItem.LocalPath);
                        }
                    }
                    else if (cmisObject is DotCMIS.Client.Impl.Document)
                    {
                        if (Utils.WorthSyncing(localFolder, cmisObject.Name, repoinfo))
                        {
                            // It is a file, just download it.
                            DownloadFile((IDocument)cmisObject, localFolder);
                        }
                    }
                    else
                    {
                        Logger.Warn("Unknown object type: " + cmisObject.ObjectType.DisplayName);
                    }
                }
            }

            /// <summary>
            /// Download a single folder from the CMIS server.
            /// </summary>
            private bool DownloadDirectory(IFolder remoteFolder, string localFolder)
            {
                SleepWhileSuspended();
                try
                {
                    // Create local folder.
                    Directory.CreateDirectory(localFolder);

                    if (remoteFolder.CreationDate != null)
                    {
                        Directory.SetCreationTime(localFolder, (DateTime)remoteFolder.CreationDate);
                    }
                    if (remoteFolder.LastModificationDate != null)
                    {
                        Directory.SetLastWriteTime(localFolder, (DateTime)remoteFolder.LastModificationDate);
                    }
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not create directory: " + localFolder, e);
                    return false;
                }

                // Create database entry for this folder
                var syncFolderItem = database.GetFolderSyncItemFromRemotePath(remoteFolder.Path);
                if (null == syncFolderItem)
                {
                    syncFolderItem = SyncItemFactory.CreateFromRemotePath(remoteFolder.Path, repoinfo);
                }
                database.AddFolder(syncFolderItem, remoteFolder.Id, remoteFolder.LastModificationDate);
                Logger.Info("Added folder to database: " + localFolder);

                // Recurse into folder.
                RecursiveFolderCopy(remoteFolder, localFolder);

                return true;
            }


            /// <summary>
            /// Download a single folder from the CMIS server for sync.
            /// </summary>
            private bool SyncDownloadFolder(IFolder remoteSubFolder, string localFolder)
            {
                var syncItem = database.GetFolderSyncItemFromRemotePath(remoteSubFolder.Path);
                string localName = PathRepresentationConverter.RemoteToLocal(remoteSubFolder.Name);
                // string remotePathname = remoteSubFolder.Path;
                // string localSubFolder = Path.Combine(localFolder, localName);
                if (!Directory.Exists(localFolder))
                {
                    // The target folder has been removed/renamed => relaunch sync
                    Logger.Warn("The target folder has been removed/renamed: " + localFolder);
                    return false;
                }

                if (Directory.Exists(syncItem.LocalPath))
                {
                    return true;
                }

                if (database.ContainsFolder(syncItem))
                {
                    // If there was previously a folder with this name, it means that
                    // the user has deleted it voluntarily, so delete it from server too.

                    // Delete the folder from the remote server.
                    Logger.Debug(String.Format("CMIS::DeleteTree({0})", remoteSubFolder.Path));
                    try
                    {
                        remoteSubFolder.DeleteTree(true, null, true);
                        // Delete the folder from database.
                        database.RemoveFolder(syncItem);
                    }
                    catch (Exception)
                    {
                        Logger.Info("Remote Folder could not be deleted: " + remoteSubFolder.Path);
                        // Just go on and try it the next time
                    }
                }
                else
                {
                    // The folder has been recently created on server, so download it.

                    // If there was previously a file with this name, delete it.
                    // TODO warn if local changes in the file.
                    if (File.Exists(syncItem.LocalPath))
                    {
                        string conflictFilename = Utils.CreateConflictFilename(syncItem.LocalPath, repoinfo.User);
                        Logger.Warn("Local file \"" + syncItem.LocalPath + "\" has been renamed to \"" + conflictFilename + "\"");
                        File.Move(syncItem.LocalPath, conflictFilename);
                    }

                    // Skip if invalid folder name. See https://github.com/aegif/CmisSync/issues/196
                    if (Utils.IsInvalidFolderName(localName))
                    {
                        Logger.Info("Skipping download of folder with illegal name: " + localName);
                    }
                    else if (repoinfo.isPathIgnored(syncItem.RemotePath))
                    {
                        Logger.Info("Skipping dowload of ignored folder: " + syncItem.RemotePath);
                    }
                    else
                    {
                        // Create local folder.remoteDocument.Name
                        Logger.Info("Creating local directory: " + syncItem.LocalPath);
                        Directory.CreateDirectory(syncItem.LocalPath);

                        // Create database entry for this folder.
                        // TODO - Yannick - Add metadata
                        database.AddFolder(syncItem, remoteSubFolder.Id, remoteSubFolder.LastModificationDate);
                    }
                }

                return true;
            }


            /// <summary>
            /// Download a single file from the CMIS server for sync.
            /// </summary>
            private bool SyncDownloadFile(IDocument remoteDocument, string localFolder, IList<string> remoteFiles = null)
            {
                string remotePath = remoteDocument.Paths[0];
                var syncItem = database.GetSyncItemFromRemotePath(remotePath);
                if (null == syncItem)
                {
                    syncItem = SyncItemFactory.CreateFromRemotePath(remotePath, repoinfo);
                    // syncItem = SyncItemFactory.CreateFromLocalFolderAndRemoteName(localFolder, remoteDocument.Name, repoinfo);
                }
                //var syncItem = SyncItemFactory.CreateFromRemotePath(localFolder, remoteDocument.Name);
                string fileName = remoteDocument.Name;
                string filePath = Path.Combine(localFolder, fileName);

                // If this file does not have a filename, ignore it.
                // It sometimes happen on IBM P8 CMIS server, not sure why.
                if (remoteDocument.ContentStreamFileName == null)
                {
                    //TODO Possibly the file content has been changed to 0, this case should be handled
                    Logger.Warn("Skipping download of '" + syncItem.RemoteFileName + "' with null content stream in " + localFolder);
                    return true;
                }

                if (null != remoteFiles)
                {
                    remoteFiles.Add(syncItem.RemoteFileName);
                }

                // Check if file extension is allowed
                if (!Utils.WorthSyncing(syncItem.RemoteFileName))
                {
                    Logger.Info("Ignore the unworth syncing remote file: " + syncItem.RemoteFileName);
                    return true;
                }

                bool success = true;

                try
                {
                    if (syncItem.ExistsLocal())
                    {
                        // Check modification date stored in database and download if remote modification date if different.
                        DateTime? serverSideModificationDate = ((DateTime)remoteDocument.LastModificationDate).ToUniversalTime();
                        DateTime? lastDatabaseUpdate = database.GetServerSideModificationDate(syncItem);

                        if (lastDatabaseUpdate == null)
                        {
                            Logger.Info("Downloading file absent from database: " + syncItem.LocalPath);
                            success = DownloadFile(remoteDocument, localFolder);
                        }
                        else
                        {
                            // If the file has been modified since last time we downloaded it, then download again.
                            if (serverSideModificationDate > lastDatabaseUpdate)
                            {
                                Logger.Info("Downloading modified file: " + syncItem.RemoteFileName);
                                success = DownloadFile(remoteDocument, localFolder);
                            }
                            else if (serverSideModificationDate == lastDatabaseUpdate)
                            {
                                // check chunked upload
                                FileInfo fileInfo = new FileInfo(syncItem.LocalPath);
                                if (remoteDocument.ContentStreamLength < fileInfo.Length)
                                {
                                    success = ResumeUploadFile(syncItem.LocalPath, remoteDocument);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (database.ContainsFile(syncItem))
                        {
                            long retries = database.GetOperationRetryCounter(syncItem.LocalPath, Database.Database.OperationType.DELETE);
                            if (retries <= repoinfo.MaxDeletionRetries)
                            {
                                // File has been recently removed locally, so remove it from server too.
                                Logger.Info("Removing locally deleted file on server: " + syncItem.LocalPath);  //?
                                try
                                {
                                    remoteDocument.DeleteAllVersions();
                                    // Remove it from database.
                                    database.RemoveFile(syncItem);
                                    database.SetOperationRetryCounter(syncItem, 0, Database.Database.OperationType.DELETE);
                                }
                                catch (CmisBaseException ex)
                                {
                                    Logger.Warn("Could not delete remote file: ", ex);
                                    database.SetOperationRetryCounter(syncItem, retries + 1, Database.Database.OperationType.DELETE);
                                    throw;
                                }
                            }
                            else
                            {
                                Logger.Info(String.Format("Skipped deletion of remote file {0} because of too many failed retries ({1} max={2})", syncItem.RemotePath, retries, repoinfo.MaxDeletionRetries));  // ???
                            }
                        }
                        else
                        {
                            // New remote file, download it.
                            Logger.Info("New remote file: " + syncItem.RemotePath);
                            success = DownloadFile(remoteDocument, localFolder);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn(String.Format("Exception while download file to {0}", syncItem.LocalPath), e);
                    success = false;
                }

                return success;
            }
            
            
            /// <summary>
            /// Set the last modification date of a local file to whatever a remote document's last modfication date is.
            /// </summary>
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
                        if (metadata.TryGetValue("cmis:lastModificationDate", out cmisModDate) && cmisModDate.Length == 3) // TODO explain 3 and 2 in following line
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
                try
                {
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
                catch (Exception e)
                {
                    Logger.Debug(String.Format("Failed to set last modified date for the local folder: {0}", folderpath), e);
                }
            }


            /// <summary>
            /// Download a single file from the CMIS server.
            /// </summary>
            private bool DownloadFile(IDocument remoteDocument, string localFolder)
            {
                SleepWhileSuspended();

                var syncItem = database.GetSyncItemFromRemotePath(remoteDocument.Paths[0]);
                if (null == syncItem)
                {
                    syncItem = SyncItemFactory.CreateFromRemotePath(remoteDocument.Paths[0], repoinfo);
                }

                Logger.Info("Downloading: " + syncItem.RemoteFileName);

                // Skip if invalid file name. See https://github.com/aegif/CmisSync/issues/196
                if (Utils.IsInvalidFileName(syncItem.LocalFileName)) 
                {
                    Logger.Info("Skipping download of file with illegal filename: " + syncItem.LocalFileName);
                    return true;
                }

                try
                {
                    DotCMIS.Data.IContentStream contentStream = null;
                    string filepath = syncItem.LocalPath;
                    string tmpfilepath = filepath + ".sync";
                    if (database.GetOperationRetryCounter(filepath, Database.Database.OperationType.DOWNLOAD) > repoinfo.MaxDownloadRetries)
                    {
                        Logger.Info(String.Format("Skipping download of file {0} because of too many failed ({1}) downloads", database.GetOperationRetryCounter(filepath, Database.Database.OperationType.DOWNLOAD)));
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
                            DateTime? serverDate = database.GetDownloadServerSideModificationDate(syncItem);
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
                        contentStream = remoteDocument.GetContentStream();

                        // If this file does not have a content stream, ignore it.
                        // Even 0 bytes files have a contentStream.
                        // null contentStream sometimes happen on IBM P8 CMIS server, not sure why.
                        if (contentStream == null)
                        {
                            Logger.Warn("Skipping download of file with null content stream: " + syncItem.RemoteFileName);
                            return true;
                        }
                        // Skip downloading the content, just go on with an empty file
                        if (remoteDocument.ContentStreamLength == 0)
                        {
                            Logger.Info("Skipping download of file with content length zero: " + syncItem.RemoteFileName);
                            using (FileStream s = File.Create(tmpfilepath))
                            {
                                s.Close();
                            }
                        }
                        else
                        {
                            filehash = DownloadStream(contentStream, tmpfilepath);
                            contentStream.Stream.Close();
                        }
                        success = true;
                    }
                    catch (CmisBaseException e)
                    {
                        ProcessRecoverableException("Download failed: " + syncItem.RemoteFileName, e);
                        if (contentStream != null) contentStream.Stream.Close();
                        success = false;
                        File.Delete(tmpfilepath);
                    }

                    if (success)
                    {
                        Logger.Info(String.Format("Downloaded remote object({0}): {1}", remoteDocument.Id, syncItem.RemoteFileName));

                        // TODO Control file integrity by using hash compare?

                        // Get metadata.
                        Dictionary<string, string[]> metadata = null;
                        try
                        {
                            metadata = FetchMetadata(remoteDocument);
                        }
                        catch (CmisBaseException e)
                        {
                            ProcessRecoverableException("Could not fetch metadata: " + syncItem.RemoteFileName, e);
                            // Remove temporary local document to avoid it being considered a new document.
                            File.Delete(tmpfilepath);
                            return false;
                        }


                        // Conflict handling
                        if (File.Exists(filepath))
                        {
                            if (database.LocalFileHasChanged(filepath)) // Conflict. Server-side file and Local file both modified.
                            {
                                Logger.Info(String.Format("Conflict with file: {0}", syncItem.RemoteFileName));
                                // Rename local file with a conflict suffix.
                                string conflictFilename = Utils.CreateConflictFilename(filepath, repoinfo.User);
                                Logger.Debug(String.Format("Renaming conflicted local file {0} to {1}", filepath, conflictFilename));
                                File.Move(filepath, conflictFilename);

                                Logger.Debug(String.Format("Renaming temporary local download file {0} to {1}", tmpfilepath, filepath));
                                // Remove the ".sync" suffix.
                                // Remove the ".sync" suffix.
                                File.Move(tmpfilepath, filepath);
                                SetLastModifiedDate(remoteDocument, filepath, metadata);

                                // Warn user about conflict.
                                string lastModifiedBy = CmisUtils.GetProperty(remoteDocument, "cmis:lastModifiedBy");
                                string message =
                                    String.Format("User {0} added a file named {1} at the same time as you.", lastModifiedBy, filepath)
                                    + "\n\n"
                                    + "Your version has been renamed '" + conflictFilename + "', please merge your important changes from it and then delete it.";
                                Logger.Info(message);
                                Utils.NotifyUser(message);
                            }
                            else // Server side file was modified, but local file was not modified.
                            {
                                Logger.Debug(String.Format("Deleteing old local file {0}", filepath));
                                File.Delete(filepath);

                                Logger.Debug(String.Format("Renaming temporary local download file {0} to {1}", tmpfilepath, filepath));
                                // Remove the ".sync" suffix.
                                File.Move(tmpfilepath, filepath);
                                SetLastModifiedDate(remoteDocument, filepath, metadata);
                            }
                        }
                        else // No conflict
                        {
                            Logger.Debug(String.Format("Renaming temporary local download file {0} to {1}", tmpfilepath, filepath));
                            // Remove the ".sync" suffix.
                            File.Move(tmpfilepath, filepath);
                            SetLastModifiedDate(remoteDocument, filepath, metadata);
                        }


                        if (null != remoteDocument.CreationDate)
                        {
                            File.SetCreationTime(filepath, (DateTime)remoteDocument.CreationDate);
                        }
                        if (null != remoteDocument.LastModificationDate)
                        {
                            File.SetLastWriteTime(filepath, (DateTime)remoteDocument.LastModificationDate);
                        }

                        // Create database entry for this file.
                        database.AddFile(syncItem, remoteDocument.Id, remoteDocument.LastModificationDate, metadata, filehash);
                        Logger.Info("Added file to database: " + filepath);
                    }
                    return success;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not download file: " + syncItem.LocalPath, e);
                    return false;
                }
            }


            private bool ResumeUploadFile(string filePath, IDocument remoteDocument)
            {
                Logger.Debug("Resuming Upload: " + filePath + " to remote document: " + remoteDocument.Name);
                if (repoinfo.ChunkSize <= 0)
                {
                    return UpdateFile(filePath, remoteDocument);
                }

                // disable the chunk upload
                return UpdateFile(filePath, remoteDocument);
            }


            /// <summary>
            /// Download a file, without retrying.
            /// </summary>
            private byte[] DownloadStream(DotCMIS.Data.IContentStream contentStream, string filePath)
            {
                byte[] hash = { };
                using (Stream file = File.OpenWrite(filePath))
                using (SHA1 hashAlg = new SHA1Managed())
                using (CryptoStream hashstream = new CryptoStream(file, hashAlg, CryptoStreamMode.Write))
                {
                    byte[] buffer = new byte[8 * 1024];
                    int len;
                    while ((len = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        hashstream.Write(buffer, 0, len);
                    }
                    hashstream.FlushFinalBlock();
                    hash = hashAlg.Hash;
                }
                contentStream.Stream.Close();
                return hash;
            }


            /// <summary>
            /// Upload a single file to the CMIS server.
            /// </summary>
            private bool UploadFile(string filePath, IFolder remoteFolder) // TODO make SyncItem
            {
                SleepWhileSuspended();

                var syncItem = database.GetSyncItemFromLocalPath(filePath);
                if (null == syncItem)
                {
                    syncItem = SyncItemFactory.CreateFromLocalPath(filePath, repoinfo);
                }
                Logger.Info("Uploading: " + syncItem.LocalPath);

                try
                {
                    IDocument remoteDocument = null;
                    byte[] filehash = { };

                    // Prepare properties
                    string remoteFileName = syncItem.RemoteFileName;
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    properties.Add(PropertyIds.Name, remoteFileName);
                    properties.Add(PropertyIds.ObjectTypeId, "cmis:document");
                    properties.Add(PropertyIds.CreationDate, File.GetCreationTime(syncItem.LocalPath));
                    properties.Add(PropertyIds.LastModificationDate, File.GetLastWriteTime(syncItem.LocalPath));

                    // Prepare content stream
                    using (Stream file = File.Open(syncItem.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))      // 
                    using (SHA1 hashAlg = new SHA1Managed())
                    using (CryptoStream hashstream = new CryptoStream(file, hashAlg, CryptoStreamMode.Read))        // 
                    {
                        ContentStream contentStream = new ContentStream();
                        contentStream.FileName = remoteFileName;
                        contentStream.MimeType = MimeType.GetMIMEType(remoteFileName);
                        contentStream.Length = file.Length;
                        contentStream.Stream = hashstream;

                        Logger.Debug("Upload Start: " + filePath + " as " + remoteFolder.Path + Path.PathSeparator.ToString() + fileName);
                        remoteDocument = remoteFolder.CreateDocument(properties, contentStream, null);
                        Logger.Debug("remoteFolder.CreateDocument finished.");
                        filehash = hashAlg.Hash;
                    }

                    // Metadata.
                    Logger.Info("Uploaded: " + syncItem.LocalPath);

                    // Get metadata. Some metadata has probably been automatically added by the server.
                    Dictionary<string, string[]> metadata = FetchMetadata(remoteDocument);

                    // Create database entry for this file.
                    database.AddFile(syncItem, remoteDocument.Id, remoteDocument.LastModificationDate, metadata, filehash);
                    Logger.Info("Added file to database: " + syncItem.LocalPath);
                    return true;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not upload file: " + syncItem.LocalPath, e);
                    return false;
                }
            }

            /// <summary>
            /// Upload folder recursively.
            /// After execution, the hierarchy on server will be: .../remoteBaseFolder/localFolder/...
            /// </summary>
            private void UploadFolderRecursively(IFolder remoteBaseFolder, string localFolder)
            {
                SleepWhileSuspended();

                IFolder folder = null;
                try
                {
                    var syncItem = database.GetFolderSyncItemFromLocalPath(localFolder);
                    if (null == syncItem)
                    {
                        syncItem = SyncItemFactory.CreateFromLocalPath(localFolder, repoinfo);
                    }
                    // Create remote folder.
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    properties.Add(PropertyIds.Name, syncItem.RemoteFileName);
                    properties.Add(PropertyIds.ObjectTypeId, "cmis:folder");
                    properties.Add(PropertyIds.CreationDate, Directory.GetCreationTime(localFolder));
                    properties.Add(PropertyIds.LastModificationDate, Directory.GetLastWriteTime(localFolder));
                    try
                    {
                        Logger.Debug(String.Format("Creating remote folder {0} for local folder {1}", syncItem.RemoteFileName, localFolder));
                        folder = remoteBaseFolder.CreateFolder(properties);
                        Logger.Debug(String.Format("Created remote folder {0}({1}) for local folder {2}", syncItem.RemoteFileName, folder.Id ,localFolder));
                    }
                    catch (CmisNameConstraintViolationException)
                    {
                        foreach (ICmisObject cmisObject in remoteBaseFolder.GetChildren())
                        {
                            if (cmisObject.Name == syncItem.RemoteFileName)
                            {
                                folder = cmisObject as IFolder;
                            }
                        }
                        if (folder == null)
                        {
                            Logger.Warn("Remote file conflict with local folder " + syncItem.LocalFileName);
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        ProcessRecoverableException(String.Format("Exception when create remote folder for local folder {0}: {1}", localFolder), e);
                        return;
                    }


                    // Create database entry for this folder
                    // TODO Add metadata
                    database.AddFolder(syncItem, folder.Id, folder.LastModificationDate);
                    Logger.Info("Added folder to database: " + localFolder);
                }
                catch (CmisBaseException e)
                {
                    ProcessRecoverableException("Could not create remote directory: " + remoteBaseFolder.Path + "/" + Path.GetFileName(localFolder), e);
                    return;
                }

                try
                {
                    // Upload each file in this folder.
                    foreach (string file in Directory.GetFiles(localFolder))
                    {
                        if (Utils.WorthSyncing(localFolder, Path.GetFileName(file), repoinfo))
                        {
                            UploadFile(file, folder);
                        }
                    }

                    // Recurse for each subfolder in this folder.
                    foreach (string subfolder in Directory.GetDirectories(localFolder))
                    {
                        if (Utils.WorthSyncing(localFolder, Path.GetFileName(subfolder), repoinfo))
                        {
                            UploadFolderRecursively(folder, subfolder);
                        }
                    }
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not uploading folder: " + localFolder, e);
                }
            }


            /// <summary>
            /// Upload new version of file.
            /// </summary>
            private bool UpdateFile(string filePath, IDocument remoteFile)
            {
                SleepWhileSuspended();
                try
                {
                    var syncItem = database.GetSyncItemFromLocalPath(filePath);
                    if (null == syncItem)
                    {
                        syncItem = SyncItemFactory.CreateFromLocalPath(filePath, repoinfo);
                    }

                    Logger.Info("Updating: " + syncItem.LocalPath);
                    using (Stream localfile = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        // Ignore files with null or empty content stream.
                        if ((localfile == null) && (localfile.Length == 0))
                        {
                            Logger.Info("Skipping update of file with null or empty content stream: " + filePath);
                            return true;
                        }

                        // Check is write permission is allow

                        // Check if the file is Check out or not
                        //if (!(bool)remoteFile.IsVersionSeriesCheckedOut)
                        if ((remoteFile.IsVersionSeriesCheckedOut == null) || !(bool)remoteFile.IsVersionSeriesCheckedOut)
                        {

                            // Prepare content stream
                            ContentStream remoteStream = new ContentStream();
                            remoteStream.FileName = remoteFile.ContentStreamFileName;
                            remoteStream.Length = localfile.Length;
                            remoteStream.MimeType = remoteFile.GetContentStream().MimeType;
                            remoteStream.Stream = localfile;
                            remoteStream.Stream.Flush();
                            Logger.Debug("before SetContentStream");

                            // CMIS do not have a Method to upload block by block. So upload file must be full.
                            // We must waiting for support of CMIS 1.1 https://issues.apache.org/jira/browse/CMIS-628
                            // http://docs.oasis-open.org/cmis/CMIS/v1.1/cs01/CMIS-v1.1-cs01.html#x1-29700019
                            // DotCMIS.Client.IObjectId objID = remoteFile.SetContentStream(remoteStream, true, true);
                            remoteFile.SetContentStream(remoteStream, true, true);

                            Logger.Debug("after SetContentStream");

                            // Update timestamp in database.
                            database.SetFileServerSideModificationDate(syncItem, ((DateTime)remoteFile.LastModificationDate).ToUniversalTime());

                            // Update checksum
                            database.RecalculateChecksum(syncItem);

                            // TODO Update metadata?
                            Logger.Info("Updated: " + syncItem.LocalPath);
                            return true;
                        }
                        else
                        {
                            string message = String.Format("File {0} is CheckOut on the server by another user: {1}", syncItem.LocalPath, remoteFile.CheckinComment);

                            // throw new IOException("File is Check Out on the server");
                            Logger.Info(message);
                            Utils.NotifyUser(message);
                            return false;
                        }
                    }
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not update file: " + filePath, e);
                    return false;
                }
            }

            /// <summary>
            /// Upload new version of file content.
            /// </summary>
            private bool UpdateFile(string filePath, IFolder remoteFolder)
            {
                SleepWhileSuspended();
                try
                {
                    // Find the document within the folder.
                    var syncItem = database.GetSyncItemFromLocalPath(filePath);
                    if (null == syncItem)
                    {
                        syncItem = SyncItemFactory.CreateFromLocalPath(filePath, repoinfo);
                    }
                    string fileName = syncItem.RemoteFileName;
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
                        Logger.Info(syncItem.LocalPath + " not found on server, must be uploaded instead of updated");
                        return UploadFile(syncItem.LocalPath, remoteFolder);
                    }

                    // Update the document itself.
                    return UpdateFile(syncItem.LocalPath, document);
                }
                catch (CmisBaseException e)
                {
                    ProcessRecoverableException("Could not update file: " + filePath, e);
                    return false;
                }
            }


            /// <summary>
            /// Remove folder from local filesystem and database.
            /// </summary>
            private bool RemoveFolderLocally(string folderPath)
            {
                SleepWhileSuspended();
                // Folder has been deleted on server, delete it locally too.
                try
                {
                    Logger.Info("Removing remotely deleted folder: " + folderPath);
                    Directory.Delete(folderPath, true);
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not delete tree:" + folderPath, e);
                    return false;
                }

                // Delete folder from database.
                if (!Directory.Exists(folderPath))
                {
                    var syncFolderItem = database.GetFolderSyncItemFromLocalPath(folderPath);
                    if (null == syncFolderItem)
                    {
                        syncFolderItem = SyncItemFactory.CreateFromLocalPath(folderPath, repoinfo);
                    }
                    database.RemoveFolder(syncFolderItem);
                }

                return true;
            }

            /// <summary>
            /// Retrieve the CMIS metadata of a document.
            /// </summary>
            /// <returns>a dictionary in which each key is a type id and each value is a couple indicating the mode ("readonly" or "ReadWrite") and the value itself.</returns>
            private Dictionary<string, string[]> FetchMetadata(IDocument document)
            {
                Dictionary<string, string[]> metadata = new Dictionary<string, string[]>();

                IObjectType typeDef = session.GetTypeDefinition(document.ObjectType.Id/*"cmis:document" not Name FullName*/); // TODO cache
                IList<IPropertyDefinition> propertyDefs = typeDef.PropertyDefinitions;

                // Get metadata.
                foreach (IProperty property in document.Properties)
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

            /// <summary>
            /// Rename a file remotely.
            /// </summary>
/*            private bool RenameFile(string directory, string newFilename, IDocument remoteFile)
            {
                SleepWhileSuspended();

                string oldPathname = Path.Combine(directory, remoteFile.Name);
                string newPathname = Path.Combine(directory, newFilename);
                try
                {

                    Logger.InfoFormat("Renaming: {0} -> {1}", oldPathname, newPathname);

                    IDictionary<string, object> properties = new Dictionary<string, object>();
                    properties[PropertyIds.Name] = newFilename;

                    IDocument updatedDocument = (IDocument)remoteFile.UpdateProperties(properties);

                    // Update the path in the database...
                    database.MoveFile(oldPathname, newPathname);

                    // Update timestamp in database.
                    database.SetFileServerSideModificationDate(newPathname, ((DateTime)updatedDocument.LastModificationDate).ToUniversalTime());

                    Logger.InfoFormat("Renamed file: {0} -> {1}", oldPathname, newPathname);
                    return true;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException(String.Format("Could not rename file: {0} -> {1}", oldPathname, newPathname), e);
                    return false;
                }
            }

            /// <summary>
            /// Rename a folder remotely.
            /// </summary>
            private bool RenameFolder(string directory, string newFilename, IFolder remoteFolder)
            {
                SleepWhileSuspended();

                string oldPathname = Path.Combine(directory, remoteFolder.Name);
                string newPathname = Path.Combine(directory, newFilename);
                try
                {

                    Logger.InfoFormat("Renaming: {0} -> {1}", oldPathname, newPathname);

                    IDictionary<string, object> properties = new Dictionary<string, object>();
                    properties[PropertyIds.Name] = newFilename;

                    IFolder updatedFolder = (IFolder)remoteFolder.UpdateProperties(properties);

                    // Update the path in the database...
                    database.MoveFolder(oldPathname, newPathname);      // database query

                    Logger.InfoFormat("Renamed folder: {0} -> {1}", oldPathname, newPathname);
                    return true;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException(String.Format("Could not rename folder: {0} -> {1}", oldPathname, newPathname), e);
                    return false;
                }
            }

            /// <summary>
            /// Move a file remotely.
            /// </summary>
            private bool MoveFile(string oldDirectory, string newDirectory, IFolder oldRemoteFolder, IFolder newRemoteFolder, IDocument remoteFile)
            {
                SleepWhileSuspended();

                string oldPathname = Path.Combine(oldDirectory, remoteFile.Name);
                string newPathname = Path.Combine(newDirectory, remoteFile.Name);
                try
                {

                    Logger.InfoFormat("Moving: {0} -> {1}", oldPathname, newPathname);


                    IDocument updatedDocument = (IDocument)remoteFile.Move(oldRemoteFolder, newRemoteFolder);

                    // Update the path in the database...
                    database.MoveFile(oldPathname, newPathname);        // database query

                    // Update timestamp in database.
                    database.SetFileServerSideModificationDate(newPathname, ((DateTime)updatedDocument.LastModificationDate).ToUniversalTime());    // database query

                    Logger.InfoFormat("Moved file: {0} -> {1}", oldPathname, newPathname);
                    return true;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException(String.Format("Could not move file: {0} -> {1}", oldPathname, newPathname), e);
                    return false;
                }
            }

            /// <summary>
            /// Move a folder remotely.
            /// </summary>
            private bool MoveFolder(string oldDirectory, string newDirectory, IFolder oldRemoteFolder, IFolder newRemoteFolder, IFolder remoteFolder)
            {
                SleepWhileSuspended();

                string oldPathname = Path.Combine(oldDirectory, remoteFolder.Name);
                string newPathname = Path.Combine(newDirectory, remoteFolder.Name);
                try
                {

                    Logger.InfoFormat("Moving: {0} -> {1}", oldPathname, newPathname);


                    IFolder updatedFolder = (IFolder)remoteFolder.Move(oldRemoteFolder, newRemoteFolder);

                    // Update the path in the database...
                    database.MoveFolder(oldPathname, newPathname);      // database query

                    Logger.InfoFormat("Moved folder: {0} -> {1}", oldPathname, newPathname);
                    return true;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException(String.Format("Could not move folder: {0} -> {1}", oldPathname, newPathname), e);
                    return false;
                }
            }*/


            /// <summary>
            /// Sleep while suspended.
            /// </summary>
            private void SleepWhileSuspended()
            {
                if (syncWorker.CancellationPending)
                {
                    //Sync was cancelled...
                    throw new OperationCanceledException("Sync was cancelled by user.");
                }

                if (repo.Status == SyncStatus.Suspend)
                {
                    while (repo.Status == SyncStatus.Suspend)
                    {
                        suspended = true;
                        Logger.DebugFormat("Sync of {0} is suspend, next retry in {1}ms", repoinfo.Name, SYNC_SUSPEND_SLEEP_INTERVAL);
                        System.Threading.Thread.Sleep(SYNC_SUSPEND_SLEEP_INTERVAL);

                        if (syncWorker.CancellationPending)
                        {
                            //Sync was cancelled...
                            repo.Resume();
                            throw new OperationCanceledException("Suspended sync was cancelled by user.");
                        }
                    }
                    suspended = false;
                }
            }
        }
    }
}
