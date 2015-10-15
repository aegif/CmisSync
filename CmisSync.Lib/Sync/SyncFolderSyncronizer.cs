//   CmisSync, a CMIS synchronization tool.
//   Copyright (C) 2012  Nicolas Raoul <nicolas.raoul@aegif.jp>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using CmisSync.Lib.Cmis;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;
using log4net;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Threading;
using CmisSync.Lib.Database;

namespace CmisSync.Lib.Sync
{
    /// <summary></summary>
    public partial class SyncFolderSyncronizer : SyncFolderSyncronizerBase
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SyncFolderSyncronizer));

        /// <summary>
        /// Whether sync is bidirectional or only from server to client.
        /// TODO make it a CMIS folder - specific setting
        /// </summary>
        private bool bidirectionalSync = true;

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
        private ISession _session;

        /// <summary>
        /// Path of the root in the remote repository.
        /// Example: "/User Homes/nicolas.raoul/demos"
        /// </summary>
        private string remoteFolderPath;

        /// <summary>
        /// Path of the local folder.
        /// Example: "C:\Users\mpreti\CmisSync\test"
        /// </summary>
        private string localFolderPath;

        /// <summary>
        /// Track whether <c>Dispose</c> has been called.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Database to cache remote information from the CMIS server.
        /// </summary>
        private Database.Database database;

        private bool _forceFullSyncAtNextSync;



        /// <summary>
        /// Constructor.
        /// </summary>
        public SyncFolderSyncronizer(Config.SyncConfig.SyncFolder SyncFolderInfo)
            : base(SyncFolderInfo)
        {
            // Database is the user's AppData/Roaming
            database = new Database.Database(SyncFolderInfo);

            foreach (Config.IgnoredFolder ignoredFolder in SyncFolderInfo.IgnoredFolders)
            {
                Logger.Info("The folder \"" + ignoredFolder + "\" will be ignored");
            }
        }

        protected override void configure()
        {
            base.configure();

            remoteFolderPath = SyncFolderInfo.RemotePath;
            localFolderPath = SyncFolderInfo.LocalPath;
        }

        public void deleteResources(bool keepLocalFiles)
        {
            if (Status != SyncStatus.Idle || Status != SyncStatus.Idle_Suspended)
            {
                throw new InvalidOperationException();
            }

            database.Delete();
            if (keepLocalFiles == false)
            {
                deleteLocalFiles();
            }
        }

        private void deleteLocalFiles()
        {
            Directory.Delete(SyncFolderInfo.LocalPath, true);
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~SyncFolderSyncronizer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        protected override void Dispose(bool disposing)
        {            
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.database.Dispose();
                }
                this.disposed = true;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// get the Session or create it if still not present
        /// </summary>
        /// <returns></returns>
        private ISession getSession()
        {
            if (_session == null)
            {
                Connect();
            }
            //TODO: check if it's still connected/valid
            return _session;
        }

        /// <summary>
        /// Connect to the CMIS repository.
        /// </summary>
        public void Connect()
        {
            if (_session == null)
            {
                // Create session.
                _session = Auth.Auth.GetCmisSession(SyncFolderInfo.Account.RemoteUrl, SyncFolderInfo.Account.Credentials, SyncFolderInfo.RepositoryId);
                Logger.Debug("Created CMIS session: " + _session.ToString());

                // Detect repository capabilities.
                ChangeLogCapability = _session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All
                        || _session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.ObjectIdsOnly;
                IsGetDescendantsSupported = _session.RepositoryInfo.Capabilities.IsGetDescendantsSupported == true;
                IsGetFolderTreeSupported = _session.RepositoryInfo.Capabilities.IsGetFolderTreeSupported == true;

                Config.Feature features = SyncFolderInfo.SupportedFeatures;
                if (features != null)
                {
                    if (IsGetDescendantsSupported && features.GetDescendantsSupport == false)
                        IsGetDescendantsSupported = false;
                    if (IsGetFolderTreeSupported && features.GetFolderTreeSupport == false)
                        IsGetFolderTreeSupported = false;
                    if (ChangeLogCapability && features.GetContentChangesSupport == false)
                        ChangeLogCapability = false;
                    if (ChangeLogCapability && _session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All
                        || _session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.Properties)
                        IsPropertyChangesSupported = true;
                }
                Logger.Debug("ChangeLog capability: " + ChangeLogCapability.ToString());
                Logger.Debug("Get folder tree support: " + IsGetFolderTreeSupported.ToString());
                Logger.Debug("Get descendants support: " + IsGetDescendantsSupported.ToString());

                if (SyncFolderInfo.ChunkSize > 0)
                {
                    Logger.Debug("Chunked Up/Download enabled: chunk size = " + SyncFolderInfo.ChunkSize.ToString() + " byte");
                }
                else
                {
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
                filters.Add("cmis:changeToken"); // Needed to send update commands, see https://github.com/aegif/CmisSync/issues/516
                _session.DefaultContext = _session.CreateOperationContext(filters, false, true, false, IncludeRelationshipsFlag.None, null, true, null, true, 100);
            }
            else
            {
                Logger.Warn("Connect() called, but the session is yet present");
            }
        }

        /// <summary>
        /// Synchronize between CMIS folder and local folder, without any check on concurrent executions or any notification.
        /// </summary>
        protected override void doSync(SyncMode syncMode)
        {
            Logger.Debug(SyncFolderInfo.DisplayName + ": doSync(syncMode=" + syncMode + ", _forceFullSyncAtNextSync=" + _forceFullSyncAtNextSync + ")");
            if (_forceFullSyncAtNextSync)
            {
                syncMode = SyncMode.FULL;
                _forceFullSyncAtNextSync = false;
            }

            SleepWhileSuspended();

            // Add ACL in the context, or ACL is null
            // OperationContext context = new OperationContext();
            // context.IncludeAcls = true;
            //IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath, context);

            //reset cache
            getSession().Clear();
            IFolder remoteFolder = tryGetObjectByPath(remoteFolderPath, 2);

            checkDirectories();

            if (syncMode == SyncMode.FULL)
            {
                Logger.Debug("invoke a full crawl sync");
                CrawlSyncAndUpdateChangeLogToken(remoteFolder, SyncFolderInfo.LocalPath);
            }
            else if (syncMode == SyncMode.PARTIAL)
            {
                // Apply local changes noticed by the filesystem watcher.
                WatcherSync(remoteFolderPath, SyncFolderInfo.LocalPath);

                if (ChangeLogCapability)
                {
                    Logger.Debug("Invoke a remote change log sync");
                    ChangeLogThenCrawlSync(remoteFolder, SyncFolderInfo.LocalPath);
                }
                else
                {
                    //  Have to crawl remote.
                    Logger.Warn("Invoke a full crawl sync (the remote does not support ChangeLog)");
                    //FIXME: why do we need to clear the watcher? it should already be
                    watcher.Clear();
                    CrawlSyncAndUpdateChangeLogToken(remoteFolder, SyncFolderInfo.LocalPath);
                }
            }
            else
            {
                throw new ArgumentException("Unknown syncMode: " + syncMode);
            }
        }

        private IFolder tryGetObjectByPath(string remoteFolderPath, int tries)
        {
            while (tries > 0)
            {
                try
                {
                    return (IFolder)getSession().GetObjectByPath(remoteFolderPath);
                }
                catch (CmisBaseException)
                {
                    //maybe a temporary error, retry
                    tries--;
                    if (tries <= 0)
                    {
                        throw;
                    }
                }
            }
            throw new InvalidOperationException("should not reach here");
        }

        private void checkDirectories()
        {
            if (!Directory.Exists(SyncFolderInfo.LocalPath))
            {
                // The user has deleted/moved/renamed the local root folder while CmisSync was running.
                throw new MissingRootSyncFolderException(SyncFolderInfo.LocalPath);
            }
        }

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
            _forceFullSyncAtNextSync = true;
        }

        /// <summary>
        /// Handle CMIS Exception.
        /// </summary>
        private void HandleException(SyncException exception)
        {
            Exception e = exception;

            if (isRecoverableException(e))
            {
                Logger.Info(exception.Message);
                NotifySyncException(EventLevel.WARN, exception);
                return;
            }
            else
            {
                Logger.Warn(exception.Message, exception);
                // TODO Any temporary file to clean?
                throw exception;
            }

            throw new UnhandledException(exception);
        }

        /// <summary>
        /// if the exception can be reported to the user and the process can continue or it must be aborted
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        private bool isRecoverableException(Exception exception)
        {
            Exception e = exception;

            while (e != null)
            {
                // Exceptions: http://docs.oasis-open.org/cmis/CMIS/v1.0/cs01/cmis-spec-v1.0.html#_Toc243905433
                if (e is CmisInvalidArgumentException)
                {
                    // One or more of the input parameters to the service method is missing or invalid (any method)
                    return true;
                }
                else if (e is CmisObjectNotFoundException)
                {
                    // The service call has specified an object that does not exist in the Repository (any method)
                    return true;
                }
                else if (e is CmisNotSupportedException)
                {
                    // The service method invoked requires an optional capability not supported by the repository (any method)
                    return false;
                }
                else if (e is CmisPermissionDeniedException)
                {
                    // The caller of the service method does not have sufficient permissions to perform the operation (any method)
                    // True because it might be only for a single document and not for the others.
                    return true;
                }
                else if (e is CmisRuntimeException)
                {
                    // Any other cause not expressible by another CMIS exception (any method)
                    // True because it might be only for a single document and not for the others.
                    return true;
                }
                else if (e is CmisConstraintException)
                {
                    // The operation violates a Repository- or Object-level constraint defined in the CMIS domain model (write methods)
                    return true;
                }
                else if (e is CmisContentAlreadyExistsException)
                {
                    // The operation attempts to set the content stream for a Document that already has a content stream without explicitly specifying the �overwriteFlag� parameter (setContentStream method)
                    return true;
                }
                else if (e is CmisFilterNotValidException)
                {
                    // The property filter or rendition filter input to the operation is not valid (read methods)
                    return true;
                }
                else if (e is CmisNameConstraintViolationException)
                {
                    // The repository is not able to store the object that the user is creating/updating due to a name constraint violation (write methods)
                    return true;
                }
                else if (e is CmisStorageException)
                {
                    // The repository is not able to store the object that the user is creating/updating due to an internal storage problem (write methods)
                    return true;
                }
                else if (e is CmisStreamNotSupportedException)
                {
                    // The operation is attempting to get or set a contentStream for a Document whose Object-type specifies that a content stream is not allowed for Document�s of that type (write methods)
                    return true;
                }
                else if (e is CmisUpdateConflictException || e is FileConflictException || e is ConflictFileStillPresentException)
                {
                    // The operation is attempting to update an object that is no longer current (as determined by the repository) (write methods)
                    return true;
                }
                else if (e is CmisVersioningException)
                {
                    // The operation is attempting to perform an action on a non-current version of a Document that cannot be performed on a non-current version.
                    return true;
                }
                else if (e is CmisConnectionException)
                {
                    // Client unable to connect to server
                    return false;
                }
                else if (e is IOException)
                {
                    // IO Exception
                    return true;
                }
                else if (e is UnauthorizedAccessException)
                {
                    // Unable to access file/directory
                    return true;
                }
                else if (e is ArgumentException)
                {
                    // File contains characters not valid for .NET, for instance carriage return.
                    return true;
                }
                else
                {
                    e = e.InnerException;
                }
            }

            return false;
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
                HandleException(new RemoteFileException(remoteFolder.Path, "Could not get children objects", e));
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
                        localSubFolderItem = SyncItemFactory.CreateFromRemotePath(remoteSubFolder.Path, SyncFolderInfo);
                    }

                    if (SyncUtils.IsWorthSyncing(localFolder, PathRepresentationConverter.RemoteToLocal(remoteSubFolder.Name), SyncFolderInfo))
                    {
                        DownloadDirectory(remoteSubFolder, localSubFolderItem.LocalPath);
                    }
                }
                else if (cmisObject is DotCMIS.Client.Impl.Document)
                {
                    if (SyncUtils.IsWorthSyncing(localFolder, cmisObject.Name, SyncFolderInfo))
                    {
                        // It is a file, just download it.
                        DownloadFile((IDocument)cmisObject, localFolder);
                    }
                }
                else
                {
                    Logger.Debug("Unknown object type: " + cmisObject.ObjectType.DisplayName
                        + " for object " + remoteFolder + "/" + cmisObject.Name);
                }
            }
        }


        /// <summary>
        /// Download a single folder from the CMIS server, by simple recursive copy.
        /// </summary>
        /// <returns>true if successful</returns>
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
                HandleException(new LocalFileException(localFolder, "Could not create directory", e));
                return false;
            }

            // Create database entry for this folder
            var syncFolderItem = database.GetFolderSyncItemFromRemotePath(remoteFolder.Path);
            if (null == syncFolderItem)
            {
                syncFolderItem = SyncItemFactory.CreateFromRemotePath(remoteFolder.Path, SyncFolderInfo);
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
        /// <returns>true if successful</returns>
        private bool SyncDownloadedFolder(IFolder remoteSubFolder, string localFolder)
        {
            var syncItem = SyncItemFactory.CreateFromRemotePath(remoteSubFolder.Path, SyncFolderInfo);
            string localName = PathRepresentationConverter.RemoteToLocal(remoteSubFolder.Name);

            // If the target folder has been removed/renamed, then relaunch sync.
            if (!Directory.Exists(localFolder))
            {
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
                    string conflictFilename = SyncUtils.CreateConflictFilename(syncItem.LocalPath, SyncFolderInfo.Account.Credentials.UserName);
                    Logger.Warn("Local file \"" + syncItem.LocalPath + "\" has been renamed to \"" + conflictFilename + "\"");
                    File.Move(syncItem.LocalPath, conflictFilename);
                }

                // Skip if invalid folder name. See https://github.com/aegif/CmisSync/issues/196
                if (SyncUtils.IsInvalidFolderName(localName))
                {
                    Logger.Info("Skipping download of folder with illegal name: " + localName);
                }
                else if (SyncFolderInfo.isPathIgnored(syncItem.RemoteRelativePath))
                {
                    Logger.Info("Skipping dowload of ignored folder: " + syncItem.RemoteRelativePath);
                }
                else
                {
                    // Create local folder.remoteDocument.Name
                    Logger.Info("Creating local directory: " + syncItem.LocalPath);
                    Directory.CreateDirectory(syncItem.LocalPath);

                    // Should the local folder be made read-only?
                    // Check ther permissions of the current user to the remote folder.
                    bool readOnly = !remoteSubFolder.AllowableActions.Actions.Contains(PermissionMappingKeys.CanAddToFolderObject);
                    if (readOnly)
                    {
                        new DirectoryInfo(syncItem.LocalPath).Attributes = FileAttributes.ReadOnly;
                    }

                    // Create database entry for this folder.
                    // TODO - Yannick - Add metadata
                    database.AddFolder(syncItem, remoteSubFolder.Id, remoteSubFolder.LastModificationDate);
                }
            }

            return true;
        }


        //FIXME: what is the difference from DownloadDirectory?
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool DownloadFolder(IFolder remoteFolder, string localFolder)
        {
            if (Directory.Exists(localFolder))
            {
                return true;
            }
            if (remoteFolder == null)
            {
                return false;
            }
            if (!Directory.Exists(Path.GetDirectoryName(localFolder)))
            {
                if (!DownloadFolder(remoteFolder.FolderParent, Path.GetDirectoryName(localFolder)))
                {
                    return false;
                }
            }
            return SyncDownloadedFolder(remoteFolder, Path.GetDirectoryName(localFolder));
        }


        /// <summary>
        /// Download a single file from the CMIS server for sync.
        /// </summary>
        /// <returns>true if successful</returns>
        private bool SyncDownloadFile(IDocument remoteDocument, string localFolder, IList<string> remoteFiles = null)
        {
            string remotePath = remoteDocument.Paths[0];
            //TODO: should create a link for each secondary paths
            SyncItem syncItem = getSyncItemFromRemotePath(remotePath);

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
            if (!SyncUtils.WorthSyncing(syncItem.RemoteFileName))
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
                        if (retries <= SyncFolderInfo.DeletionRetries)
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
                            Logger.Info(String.Format("Skipped deletion of remote file {0} because of too many failed retries ({1} max={2})", syncItem.RemotePath, retries, SyncFolderInfo.DeletionRetries));  // ???
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

        private SyncItem getSyncItemFromRemotePath(string remotePath)
        {
            SyncItem syncItem = database.GetSyncItemFromRemotePath(remotePath);
            if (null == syncItem)
            {
                syncItem = SyncItemFactory.CreateFromRemotePath(remotePath, SyncFolderInfo);
            }
            return syncItem;
        }

        private SyncItem getSyncItemFromLocalPath(string localPath)
        {
            SyncItem syncItem = database.GetSyncItemFromLocalPath(localPath);
            if (null == syncItem)
            {
                syncItem = SyncItemFactory.CreateFromLocalPath(localPath, SyncFolderInfo);
            }
            return syncItem;
        }

        private DateTime? getUtcCreationDate(ICmisObject remoteObject, Dictionary<string, string[]> metadata = null)
        {
            DateTime? date = remoteObject.CreationDate;
            if (date == null && metadata != null)
            {
                string[] cmisModDate;
                if (metadata.TryGetValue("cmis:creationDate", out cmisModDate) && cmisModDate.Length == 3) // TODO explain 3 and 2 in following line
                {
                    date = DateTime.Parse(cmisModDate[2]);
                }
            }
            if (date != null)
            {
                return ((DateTime)date).ToUniversalTime();
            }
            else
            {
                return null;
            }
        }

        private DateTime? getUtcLastModificationDate(ICmisObject remoteObject, Dictionary<string, string[]> metadata = null)
        {
            DateTime? date = remoteObject.LastModificationDate;
            if (date == null && metadata != null)
            {
                string[] cmisModDate;
                if (metadata.TryGetValue("cmis:lastModificationDate", out cmisModDate) && cmisModDate.Length == 3) // TODO explain 3 and 2 in following line
                {
                    date = DateTime.Parse(cmisModDate[2]);
                }
            }
            if (date != null)
            {
                return ((DateTime)date).ToUniversalTime();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Set the creation date of a local file or folder to whatever a remote cmisObject's creation date is.
        /// </summary>
        private void SetCreationDate(ICmisObject remoteObject, string filepath, Dictionary<string, string[]> metadata)
        {
            try
            {
                DateTime? creationDate = getUtcCreationDate(remoteObject, metadata);
                if (creationDate != null)
                {
                    File.SetCreationTimeUtc(filepath, creationDate.Value);
                    if (Logger.IsDebugEnabled)
                    {
                        if (!File.GetCreationTimeUtc(filepath).Equals(creationDate))
                        {
                            throw new InvalidOperationException("SetCreationTimeUtc failed");
                        }
                    }
                }
                else
                {
                    throw new ArgumentException("The remote object Creation Date could not be determined");
                }
            }
            catch (Exception e)
            {
                Logger.Debug(String.Format("Failed to set creation date for the local file: {0}", filepath), e);
            }
        }

        /// <summary>
        /// Set the last modification date of a local file or folder to whatever a remote cmisObject's last modfication date is.
        /// </summary>
        private void SetLastModifiedDate(ICmisObject remoteObject, string filepath, Dictionary<string, string[]> metadata)
        {
            try
            {
                DateTime? lastModificationDate = getUtcLastModificationDate(remoteObject, metadata);
                if (lastModificationDate != null)
                {
                    File.SetLastWriteTimeUtc(filepath, lastModificationDate.Value);
                    if (Logger.IsDebugEnabled)
                    {
                        if (!File.GetLastWriteTimeUtc(filepath).Equals(lastModificationDate))
                        {
                            throw new InvalidOperationException("SetLastWriteTimeUtc failed");
                        }
                    }
                }
                else
                {
                    throw new ArgumentException("The remote object Last Modification Date could not be determined");
                }
            }
            catch (Exception e)
            {
                Logger.Debug(String.Format("Failed to set last modified date for the local file: {0}", filepath), e);
            }
        }

        private void SetPermissions(ICmisObject remoteObject, string filepath, Dictionary<string, string[]> metadata)
        {
            // Should the local file be made read-only?
            // Check ther permissions of the current user to the remote document.
            bool readOnly = !remoteObject.AllowableActions.Actions.Contains(Actions.CanSetContentStream);

            new FileInfo(filepath).IsReadOnly = readOnly;
        }

        /// <summary>
        /// Download a single file from the CMIS server.
        /// 
        /// Algorithm:
        /// 
        /// Skip if invalid filename
        /// If directory exists with same name, delete it
        /// If temporary file already exists but database has a different modification date than server, delete it
        /// Download data and metadata, return if that fails
        /// If a file with this name already exists locally
        ///   If conflict
        ///     Rename the existing file and put the server fils instead
        ///     Notify the user
        ///   If file update
        ///     Replace the file
        /// Else (new file)
        ///   Save
        /// Set creation date and last modification date if available
        /// Make read-only if remote can not be modified
        /// Create CmisSync database entry for this file
        /// 
        /// </summary>
        private bool DownloadFile(IDocument remoteDocument, string localFolder)
        {
            SleepWhileSuspended();

            SyncItem syncItem = getSyncItemFromRemotePath(remoteDocument.Paths[0]);
            Logger.Info("Downloading: " + syncItem.RemoteFileName);

            // Skip if invalid file name. See https://github.com/aegif/CmisSync/issues/196
            if (SyncUtils.IsInvalidFileName(syncItem.LocalFileName))
            {
                Logger.Info("Skipping download of file with illegal filename: " + syncItem.LocalFileName);
                return true;
            }

            string filepath = syncItem.LocalPath;
            string tmpfilepath = filepath + ".sync";
            if (database.GetOperationRetryCounter(filepath, Database.Database.OperationType.DOWNLOAD) > SyncFolderInfo.MaxDownloadRetries)
            {
                Logger.Info(String.Format("Skipping download of file {0} because of too many failed ({1}) downloads", database.GetOperationRetryCounter(filepath, Database.Database.OperationType.DOWNLOAD)));
                return true;
            }

            try
            {
                // If there was previously a directory with this name
                if (Directory.Exists(filepath))
                {
                    HandleException(new DirectoryCollisionFileException(filepath));
                    return false;
                }

                if (File.Exists(tmpfilepath))
                {
                    Logger.Warn("found an existing .sync file wile downloading a new file. Probabli it's a previously failed syncronization leftover: deleting it");
                    //TODO: make sure it is not a user file
                    File.Delete(tmpfilepath);
                }


                // Download file.
                DotCMIS.Data.IContentStream contentStream = null;
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
                    }
                }
                catch (CmisBaseException e)
                {
                    try
                    {
                        HandleException(new DownloadFileException(syncItem.RemoteFileName, e));
                    }
                    finally
                    {
                        File.Delete(tmpfilepath);
                        if (contentStream != null) contentStream.Stream.Close();
                    }
                    return false;
                }
                finally
                {
                    if (contentStream != null)
                    {
                        contentStream.Stream.Close();
                    }
                }

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
                    HandleException(new FetchMetadataFileException(syncItem.RemoteFileName, e));
                    // Remove temporary local document to avoid it being considered a new document.
                    File.Delete(tmpfilepath);
                    return false;
                }


                // Update or conflict
                if (File.Exists(filepath))
                {
                    if (database.LocalFileHasChanged(filepath)) // Conflict. Server-side file and Local file both modified.
                    {
                        Logger.Info(String.Format("Conflict with file: {0}", syncItem.RemoteFileName));
                        // Rename local file with a conflict suffix.
                        string conflictFilename = SyncUtils.CreateConflictFilename(filepath, SyncFolderInfo.Account.Credentials.UserName);
                        Logger.Debug(String.Format("Renaming conflicted local file {0} to {1}", filepath, conflictFilename));
                        File.Move(filepath, conflictFilename);

                        Logger.Debug(String.Format("Renaming temporary local download file {0} to {1}", tmpfilepath, filepath));
                        // Remove the ".sync" suffix.
                        File.Move(tmpfilepath, filepath);

                        // Warn user about conflict.
                        string lastModifiedBy = CmisUtils.GetProperty(remoteDocument, "cmis:lastModifiedBy");
                        Logger.Info("FileConflict: user=" + lastModifiedBy + ", file=" + filepath + ", conflictFileName=" + conflictFilename);
                        HandleException(new FileConflictException(filepath, lastModifiedBy, conflictFilename));
                    }
                    else // Server side file was modified, but local file was not modified.
                    {
                        Logger.Debug(String.Format("Deleting old local file {0}", filepath));
                        File.Delete(filepath);

                        Logger.Debug(String.Format("Renaming temporary local download file {0} to {1}", tmpfilepath, filepath));
                        // Remove the ".sync" suffix.
                        File.Move(tmpfilepath, filepath);
                    }
                }
                else // No conflict
                {
                    Logger.Debug(String.Format("Renaming temporary local download file {0} to {1}", tmpfilepath, filepath));
                    // Remove the ".sync" suffix.
                    File.Move(tmpfilepath, filepath);
                }

                SetCreationDate(remoteDocument, filepath, metadata);
                SetLastModifiedDate(remoteDocument, filepath, metadata);
                SetPermissions(remoteDocument, filepath, metadata);

                // Create database entry for this file.
                database.AddFile(syncItem, remoteDocument.Id, remoteDocument.LastModificationDate, metadata, filehash);
                Logger.Info("Added file to database: " + filepath);

                return true;
            }
            catch (Exception e)
            {
                HandleException(new DownloadFileException(syncItem.LocalPath, e));
                return false;
            }
        }

        private bool ResumeUploadFile(string filePath, IDocument remoteDocument)
        {
            Logger.Debug("Resuming Upload: " + filePath + " to remote document: " + remoteDocument.Name);
            return UpdateFile(filePath, ref remoteDocument);
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
                syncItem = SyncItemFactory.CreateFromLocalPath(filePath, SyncFolderInfo);
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
                using (Stream file = File.Open(syncItem.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (SHA1 hashAlg = new SHA1Managed())
                using (CryptoStream hashstream = new CryptoStream(file, hashAlg, CryptoStreamMode.Read))
                {
                    ContentStream contentStream = new ContentStream();
                    contentStream.FileName = remoteFileName;
                    contentStream.MimeType = MimeType.GetMIMEType(remoteFileName);
                    contentStream.Length = file.Length;
                    contentStream.Stream = hashstream;

                    Logger.Debug("Uploading: " + syncItem.LocalPath + " as "
                        + remoteFolder.Path + "/" + remoteFileName);
                    remoteDocument = remoteFolder.CreateDocument(properties, contentStream, null);
                    Logger.Debug("Uploaded: " + syncItem.LocalPath);
                    filehash = hashAlg.Hash;
                }

                // Get metadata. Some metadata has probably been automatically added by the server.
                Dictionary<string, string[]> metadata = FetchMetadata(remoteDocument);

                // Create database entry for this file.
                database.AddFile(syncItem, remoteDocument.Id, remoteDocument.LastModificationDate, metadata, filehash);
                Logger.Info("Added file to database: " + syncItem.LocalPath);
                return true;
            }
            catch (Exception e)
            {
                HandleException(new UploadFileException(syncItem.LocalPath, e));
                return false;
            }
        }

        /// <summary>
        /// Upload folder recursively.
        /// After execution, the hierarchy on server will be: .../remoteBaseFolder/localFolder/...
        /// </summary>
        private bool UploadFolderRecursively(IFolder remoteBaseFolder, string localFolder)
        {
            bool success = true;
            SleepWhileSuspended();

            IFolder folder = null;
            try
            {
                SyncItem syncItem = getSyncItemFromLocalPath(localFolder);

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
                    Logger.Debug(String.Format("Created remote folder {0}({1}) for local folder {2}", syncItem.RemoteFileName, folder.Id, localFolder));
                }
                catch (CmisNameConstraintViolationException e)
                {
                    HandleException(new DirectoryCreationRemoteFileCollisionException(syncItem.LocalFileName, e));
                    return false;
                }

                // Create database entry for this folder
                // TODO Add metadata
                database.AddFolder(syncItem, folder.Id, folder.LastModificationDate);
                Logger.Info("Added folder to database: " + localFolder);
            }
            catch (CmisBaseException e)
            {
                HandleException(new CreateRemoteDirectory(remoteBaseFolder.Path + "/" + Path.GetFileName(localFolder), e));
                return false;
            }

            try
            {
                // Upload each file in this folder.
                foreach (string file in Directory.GetFiles(localFolder))
                {
                    if (SyncUtils.IsWorthSyncing(localFolder, Path.GetFileName(file), SyncFolderInfo))
                    {
                        success &= UploadFile(file, folder);
                    }
                }

                // Recurse for each subfolder in this folder.
                foreach (string subfolder in Directory.GetDirectories(localFolder))
                {
                    if (SyncUtils.IsWorthSyncing(localFolder, Path.GetFileName(subfolder), SyncFolderInfo))
                    {
                        success &= UploadFolderRecursively(folder, subfolder);
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new UploadFolderException(localFolder, e));
                return false;
            }
            return success;
        }


        /// <summary>
        /// Upload new version of file.
        /// </summary>
        private bool UpdateFile(string filePath, ref IDocument remoteFile)
        {
            SleepWhileSuspended();
            try
            {
                SyncItem syncItem = getSyncItemFromLocalPath(filePath);

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
                        //remoteFile still refer to the old version of the file, and the LastModificationDate is not the right one:
                        //load the new version data
                        remoteFile = getSession().GetLatestDocumentVersion(remoteFile.Id);
                        database.SetFileServerSideModificationDate(syncItem, ((DateTime)remoteFile.LastModificationDate).ToUniversalTime());

                        // Update checksum
                        database.RecalculateChecksum(syncItem);

                        // TODO Update metadata?
                        Logger.Info("Updated: " + syncItem.LocalPath);
                        return true;
                    }
                    else
                    {
                        HandleException(new CheckOutFileException(syncItem.LocalPath, remoteFile.CheckinComment));
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new UploadFileException(filePath, e));
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
                    syncItem = SyncItemFactory.CreateFromLocalPath(filePath, SyncFolderInfo);
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
                return UpdateFile(syncItem.LocalPath, ref document);
            }
            catch (CmisBaseException e)
            {
                HandleException(new UploadFileException(filePath, e));
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
                HandleException(new DeleteLocalFolderException(folderPath, e));
                return false;
            }

            // Delete folder from database.
            if (!Directory.Exists(folderPath))
            {
                var syncFolderItem = database.GetFolderSyncItemFromLocalPath(folderPath);
                if (null == syncFolderItem)
                {
                    syncFolderItem = SyncItemFactory.CreateFromLocalPath(folderPath, SyncFolderInfo);
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

            IObjectType typeDef = getSession().GetTypeDefinition(document.ObjectType.Id/*"cmis:document" not Name FullName*/); // TODO cache
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
        private bool RenameRemoteFile(string directory, string newFilename, IDocument remoteFile)
        {
            SleepWhileSuspended();

            string oldPathname = Path.Combine(directory, remoteFile.Name);
            string newPathname = Path.Combine(directory, newFilename);

            Logger.InfoFormat("Renaming: {0} -> {1}", oldPathname, newPathname);

            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = newFilename;

            IDocument updatedDocument;
            try
            {
                updatedDocument = (IDocument)remoteFile.UpdateProperties(properties);
            }
            catch (Exception e)
            {
                HandleException(new RenameRemoteFileException(oldPathname, newPathname, e));
                return false;
            }

            // Update the path in the database...
            database.MoveFile(SyncItemFactory.CreateFromLocalPath(oldPathname, SyncFolderInfo), SyncItemFactory.CreateFromLocalPath(newPathname, SyncFolderInfo));

            // Update timestamp in database.
            database.SetFileServerSideModificationDate(
                SyncItemFactory.CreateFromLocalPath(newPathname, SyncFolderInfo),
                ((DateTime)updatedDocument.LastModificationDate).ToUniversalTime());

            Logger.InfoFormat("Renamed file: {0} -> {1}", oldPathname, newPathname);
            return true;
        }

        /// <summary>
        /// Rename a folder remotely.
        /// </summary>
        private bool RenameRemoteFolder(string directory, string newFilename, IFolder remoteFolder)
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
                database.MoveFolder(SyncItemFactory.CreateFromLocalPath(oldPathname, SyncFolderInfo), SyncItemFactory.CreateFromLocalPath(newPathname, SyncFolderInfo));      // database query

                Logger.InfoFormat("Renamed folder: {0} -> {1}", oldPathname, newPathname);
                return true;
            }
            catch (Exception e)
            {
                HandleException(new RenameRemoteFolderException(oldPathname, newPathname, e));
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
                database.MoveFile(SyncItemFactory.CreateFromLocalPath(oldPathname, SyncFolderInfo), SyncItemFactory.CreateFromLocalPath(newPathname, SyncFolderInfo));        // database query

                // Update timestamp in database.
                database.SetFileServerSideModificationDate(
                    SyncItemFactory.CreateFromLocalPath(newPathname, SyncFolderInfo),
                    ((DateTime)updatedDocument.LastModificationDate).ToUniversalTime());    // database query

                Logger.InfoFormat("Moved file: {0} -> {1}", oldPathname, newPathname);
                return true;
            }
            catch (Exception e)
            {
                HandleException(new MoveRemoteFileException(oldPathname, newPathname, e));
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
                database.MoveFolder(SyncItemFactory.CreateFromLocalPath(oldPathname, SyncFolderInfo), SyncItemFactory.CreateFromLocalPath(newPathname, SyncFolderInfo));      // database query

                Logger.InfoFormat("Moved folder: {0} -> {1}", oldPathname, newPathname);
                return true;
            }
            catch (Exception e)
            {
                HandleException(new MoveRemoteFolderException(oldPathname, newPathname, e));
                return false;
            }
        }

        /// <summary></summary>
        public override void Resume()
        {
            resetFailedOperationsCounter();
            ForceFullSyncAtNextSync();
            base.Resume();
        }

        /// <summary>
        /// Resets all the failed upload to zero.
        /// </summary>
        public void resetFailedOperationsCounter()
        {
            Logger.Debug("Reset all failed upload counter");
            database.DeleteAllFailedOperations();
        }
    }
}
