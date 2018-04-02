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
using CmisSync.Lib.Database;
using CmisSync.Lib.ActivityListener;
using CmisSync.Lib.Config;
using CmisSync.Lib.Sync.SyncRepo;
using CmisSync.Lib.Utilities.PathConverter;
using CmisSync.Lib.UserNotificationListener;
using CmisSync.Lib.Sync.SynchronizeItem;

namespace CmisSync.Lib.Sync.CmisRepoFolder
{
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
        private bool BIDIRECTIONAL = true; // TODO move to CmisProfile

        /// <summary>
        /// At which degree the repository supports Change Logs.
        /// See http://docs.oasis-open.org/cmis/CMIS/v1.0/os/cmis-spec-v1.0.html#_Toc243905424
        /// The possible values are actually none, objectidsonly, properties, all
        /// But for now we only distinguish between none (false) and the rest (true)
        /// </summary>
        private bool ChangeLogCapability; // TODO move to CmisProfile

        /// <summary>
        /// If the repository is able send a folder tree in one request, this is true,
        /// Otherwise the default behaviour is false
        /// </summary>
        private bool IsGetFolderTreeSupported = false; // TODO move to CmisProfile

        /// <summary>
        /// If the repository allows to request all Descendants of a folder or file,
        /// this is set to true, otherwise the default behaviour is false
        /// </summary>
        private bool IsGetDescendantsSupported = false; // TODO move to CmisProfile

        /// <summary>
        /// Is true, if the repository is able to return property changes.
        /// </summary>
        private bool IsPropertyChangesSupported = false; // TODO move to CmisProfile

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
        /// Whether sync is actually enabled right now.
        /// This is different from CmisRepo.Enabled, which if false means "disabled, or will be disable as soon as possible"
        /// </summary>
        private bool enabled = false;

        /// <summary>
        /// Listener we inform about activity (used by spinner).
        /// </summary>
        private IActivityListener activityListener;

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
        private RepoInfo repoInfo;

        /// <summary>
        /// Link to parent object.
        /// </summary>
        private RepoBase repo;
        

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
        private AutoResetEvent autoResetEvent = new AutoResetEvent(true); // TODO needed?

        /// <summary>
        ///  Constructor for Repo (at every launch of CmisSync)
        /// </summary>
        public SynchronizedFolder(RepoInfo repoInfo, RepoBase repo, IActivityListener activityListener)
        {
            this.activityListener = activityListener;

            if (null == repoInfo || null == repo)
            {
                throw new ArgumentNullException("repoInfo");
            }

            this.repo = repo;
            this.repoInfo = repoInfo;

            enabled = this.repoInfo.IsSuspended;

            // Database in the user's AppData/Roaming
            database = new Database.Database(repoInfo.CmisDatabase, repo.LocalPath, repoInfo.RemotePath);

            // Get path on remote repository.
            remoteFolderPath = repoInfo.RemotePath;

            if (Logger.IsInfoEnabled)
            {
                foreach (string ignoredFolder in repoInfo.getIgnoredPaths())
                {
                    Logger.Info("The folder \"" + ignoredFolder + "\" will be ignored");
                }
            }

            syncWorker = new BackgroundWorker();
            syncWorker.WorkerSupportsCancellation = true;
            syncWorker.DoWork += new DoWorkEventHandler(
                delegate(Object o, DoWorkEventArgs args)
                {
                    bool success = false;
                    try
                    {
                        success = Sync();
                    }
                    catch (OperationCanceledException e)
                    {
                        Logger.Info(e.Message);
                    }
                    catch (CmisPermissionDeniedException e)
                    {
                        Logger.Error(e.Message);
                        repo.OnSyncError(new PermissionDeniedException("Authentication failed.", e));
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e.Message);
                        repo.OnSyncError(new BaseException(e));
                    }
                    finally
                    {
                        OnSyncComplete(success);
                    }
                }
            );
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

            this.repoInfo = repoInfo;
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
        /// Connect to the CMIS repository.
        /// </summary>
        public void Connect()
        {
            // Create session.
            session = Auth.Authentication.GetCmisSession(repoInfo.Address.ToString(), repoInfo.User, repoInfo.Password.ToString(), repoInfo.RepoID);
            Logger.Debug("Created CMIS session: " + session.ToString());
            
            // Detect repository capabilities.
            ChangeLogCapability = session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All
                    || session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.ObjectIdsOnly;
            IsGetDescendantsSupported = session.RepositoryInfo.Capabilities.IsGetDescendantsSupported == true;
            IsGetFolderTreeSupported = session.RepositoryInfo.Capabilities.IsGetFolderTreeSupported == true;
            
            //repoInfo.CmisProfile.contentStreamFileNameOrderable = session.RepositoryInfo.Capabilities. TODO

            Config.CmisSyncConfig.SyncConfig.Folder folder = ConfigManager.CurrentConfig.GetFolder(this.repoInfo.Name);
            if (folder != null)
            {
                Config.CmisSyncConfig.Feature features = folder.SupportedFeatures;
                if (features != null)
                {
                    if (IsGetDescendantsSupported && features.GetDescendantsSupport == false)
                        IsGetDescendantsSupported = false;
                    if (IsGetFolderTreeSupported && features.GetFolderTreeSupport == false)
                        IsGetFolderTreeSupported = false;
                    if (ChangeLogCapability && features.GetContentChangesSupport == false)
                        ChangeLogCapability = false;
                    if (ChangeLogCapability && session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All 
                        || session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.Properties)
                        IsPropertyChangesSupported = true;
                }
            }
            Logger.Debug("ChangeLog capability: " + ChangeLogCapability.ToString());
            Logger.Debug("Get folder tree support: " + IsGetFolderTreeSupported.ToString());
            Logger.Debug("Get descendants support: " + IsGetDescendantsSupported.ToString());
            if (repoInfo.ChunkSize > 0)
            {
                Logger.Debug("Chunked Up/Download enabled: chunk size = " + repoInfo.ChunkSize.ToString() + " byte");
            }
            else
            {
                Logger.Debug("Chunked Up/Download disabled");
            }
            HashSet<string> filters = new HashSet<string>();
            filters.Add("cmis:objectId");
            filters.Add("cmis:name");
            if (!CmisUtils.IsDocumentum(session))
            {
                filters.Add("cmis:contentStreamFileName");
                filters.Add("cmis:contentStreamLength");
            }
            filters.Add("cmis:lastModificationDate");
            filters.Add("cmis:lastModifiedBy");
            filters.Add("cmis:path");
            filters.Add("cmis:changeToken"); // Needed to send update commands, see https://github.com/aegif/CmisSync/issues/516
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
            return this.enabled;
        }


        /// <summary>
        /// Synchronize between CMIS folder and local folder.
        /// </summary>
        /// <returns>success or not</returns>
        public bool Sync()
        {
            lock (syncLock)
            {
                autoResetEvent.Reset();
                repo.OnSyncStart();

                SleepWhileSuspended();

                // If not connected, connect.
                if (session == null)
                {
                    Connect();

                    firstSync = repoInfo.SyncAtStartup;
                }
                else
                {
                    //  Force to reset the cache for each Sync
                    session.Clear();
                }

                // Add ACL in the context, or ACL is null
                // OperationContext context = new OperationContext();
                // context.IncludeAcls = true;
                // IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath, context);

                IFolder remoteFolder = null;
                try
                {
                    remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath, true);
                }
                catch (PermissionDeniedException e)
                {
                    // The session might have been cut by the remote server, so try to reconnect.
                    Connect();

                    // Retry the same operation.
                    remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath, true);
                }

                string localFolder = repoInfo.TargetDirectory;
                var success = true;

                if (firstSync)
                {
                    Logger.Debug("First sync, apply local changes then invoke a full crawl sync");
                    
                    // Compare local files with local database and apply changes to the server.
                    ApplyLocalChanges(localFolder);

                    if (ChangeLogCapability)
                    {
                        //Before full sync, get latest changelog
                        var lastTokenOnServer = CmisUtils.GetChangeLogToken(session);
                        success = CrawlSync(remoteFolder, remoteFolderPath, localFolder);
                        if(success) database.SetChangeLogToken(lastTokenOnServer);
                    }
                    else
                    {
                        // TODO debug
                        System.Console.WriteLine ("Crawl Sync start: \n" +
                                                  "  remoteFolder's path property: {0}\n" +
                                                  "  remoteFolderPath: {1}\n" +
                                                  "  localFolder: {2}\n" +
                                                  "  repoInfoRemotePath: {3}\n" +
                                                  "  repoInfoTargetPath: {4}",
                                                  remoteFolder.Path, remoteFolderPath, localFolder, repoInfo.RemotePath, repoInfo.TargetDirectory);

                        // Full sync.
                        success = CrawlSync(remoteFolder, remoteFolderPath, localFolder);

                    }

                    //If crawl sync failed, retry.
                    firstSync = !success;
                }
                else
                {
                    // Apply local changes noticed by the filesystem watcher.
                    if (repo.Watcher != null)
                    {
                        success = WatcherSync(remoteFolderPath, localFolder);
                    }

                    // Compare locally, in case the watcher did not do its job correctly (that happens, Windows bug).
                    success &= ApplyLocalChanges(localFolder);

                    if (ChangeLogCapability)
                    {
                        Logger.Debug("Invoke a remote change log sync");
                        success &= ChangeLogThenCrawlSync(remoteFolder, remoteFolderPath, localFolder);
                    }
                    else
                    {
                        //  Have to crawl remote.
                        Logger.Warn("Invoke a full crawl sync (the remote does not support ChangeLog)");
                        if (repo.Watcher != null)
                        {
                            repo.Watcher.Clear();
                        }
                        success &= CrawlSyncAndUpdateChangeLogToken(remoteFolder, remoteFolderPath, localFolder);
                    }
                }
                return success;
            }
        }

        /// <summary>
        /// Synchronization has completed.
        /// </summary>
        public void OnSyncComplete(bool success)
        {
            lock (syncLock)
            {
                try
                {
                    repo.OnSyncComplete(success);
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
        /// <returns>success or not</returns>
        public bool SyncInForeground()
        {
            if (IsSyncing())
            {
                Logger.Debug("SyncInForeground: Sync already running in background: " + repoInfo.TargetDirectory);
                return false;
            }

            bool success = false;
            try
            {
                success = Sync();
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
                OnSyncComplete(success);
            }
            return success;
        }

        /// <summary>
        /// Sync in the background.
        /// </summary>
        public void SyncInBackground()
        {
            if (IsSyncing())
            {
                Logger.Debug("SyncInBackground: Sync already running in background: " + repoInfo.TargetDirectory);
                return;
            }

            syncWorker.RunWorkerAsync();

            Logger.Debug("SyncInBackground: IsSyncing(): " + IsSyncing());
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
                // True because it might be only for a single document and not for the others.
                recoverable = true;
            }
            else if (exception is CmisRuntimeException)
            {
                // Any other cause not expressible by another CMIS exception (any method)
                // True because it might be only for a single document and not for the others.
                recoverable = true;
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

            if ( ! recoverable)
            {
                // TODO Any temporary file to clean maybe?
                throw exception;
            }
        }

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

            if ( ! repo.Enabled)
            {
                while ( ! repo.Enabled)
                {
                    enabled = true;
                    Logger.DebugFormat("Sync of {0} is suspend, next retry in {1}ms", repoInfo.Name, SYNC_SUSPEND_SLEEP_INTERVAL);
                    System.Threading.Thread.Sleep(SYNC_SUSPEND_SLEEP_INTERVAL);

                    if (syncWorker.CancellationPending)
                    {
                        //Sync was cancelled...
                        repo.Enable();
                        throw new OperationCanceledException("Suspended sync was cancelled by user.");
                    }
                }
                enabled = false;
            }
        }
    }
}
