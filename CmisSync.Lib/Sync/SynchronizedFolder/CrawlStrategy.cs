using DotCMIS.Client;
using DotCMIS.Exceptions;
using System;
using System.Linq;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using DotCMIS.Client.Impl;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Utilities.FileUtilities;
using DotCMIS.Enums;
using System.Text;
using CmisSync.Lib.Sync;

using CmisSync.Lib.Utilities.UserNotificationListener;
using CmisSync.Lib.Sync.SynchronizeItem;

namespace CmisSync.Lib.Sync.CmisRepoFolder
{
    /// <summary>
    /// Synchronization with a particular CMIS folder.
    /// </summary>
    public partial class SynchronizedFolder
    {
        /// <summary>
        /// Synchronize by checking all folders/files one-by-one.
        /// This strategy is used if the CMIS server does not support the ChangeLog feature.
        /// 
        /// for all remote folders:
        ///     if exists locally:
        ///       recurse
        ///     else
        ///       if in database:
        ///         delete recursively from server // if BIDIRECTIONAL
        ///       else
        ///         download recursively
        /// for all remote files:
        ///     if exists locally:
        ///       if remote is more recent than local:
        ///         download
        ///       else
        ///         upload                         // if BIDIRECTIONAL
        ///     else:
        ///       if in database:
        ///         delete from server             // if BIDIRECTIONAL
        ///       else
        ///         download
        /// for all local files:
        ///   if not present remotely:
        ///     if in database:
        ///       delete
        ///     else:
        ///       upload                           // if BIDIRECTIONAL
        ///   else:
        ///     if has changed locally:
        ///       upload                           // if BIDIRECTIONAL
        /// for all local folders:
        ///   if not present remotely:
        ///     if in database:
        ///       delete recursively from local
        ///     else:
        ///       upload recursively               // if BIDIRECTIONAL
        /// </summary>
        /// <returns>
        /// True if all content has been successfully synchronized.
        /// False if anything has failed or been skipped.
        /// </returns>
        private bool CrawlSync(IFolder remoteFolder, string remotePath, string localFolder)
        {
            SleepWhileSuspended();

            /*if (IsGetDescendantsSupported)  Disabled because it causes server-side problems for folders with a huge number of files.
            {
                IList<ITree<IFileableCmisObject>> desc;
                try
                {
                    desc = remoteFolder.GetDescendants(-1);
                }
                catch (DotCMIS.Exceptions.CmisConnectionException ex)
                {
                    if (ex.InnerException is System.Xml.XmlException)
                    {
                        Logger.Warn(String.Format("CMIS::getDescendants() response could not be parsed: {0}", ex.InnerException.Message));
                    }
                    throw;
                }
                CrawlDescendants(remoteFolder, desc, localFolder);
            }*/

            // Lists of files/folders, to delete those that have been removed on the server.
            IList<string> localFilenameTranslationOfExistingRemoteDocuments = new List<string>();
            IList<string> remoteSubfolders = new List<string>();

            // Crawl remote children.
            Logger.InfoFormat("Crawl remote folder {0}", remotePath);
            bool success = CrawlRemote(remoteFolder, remotePath, localFolder, localFilenameTranslationOfExistingRemoteDocuments, remoteSubfolders);

            // Crawl local files.
            Logger.InfoFormat("Crawl local files in the local folder {0}", localFolder);
            CheckLocalFiles(localFolder, remoteFolder, localFilenameTranslationOfExistingRemoteDocuments);

            // Crawl local folders.
            Logger.InfoFormat("Crawl local folder {0}", localFolder);
            CheckLocalFolders(localFolder, remoteFolder, remoteSubfolders);

            return success;
        }


        /// <summary>
        /// Perform a crawl sync (check all folders and file checksums).
        /// If successful, update the local ChangeLog token.
        /// </summary>
        private bool CrawlSyncAndUpdateChangeLogToken(IFolder remoteFolder, string remotePath, string localFolder)
        {
            var sw = new System.Diagnostics.Stopwatch();
            bool success = false;
            Logger.Info("Remote Full Crawl Started");
            try
            {
                // Get ChangeLog token.
                string token = CmisUtils.GetChangeLogToken(session);

                // Sync.
                success = CrawlSync(remoteFolder, remotePath, localFolder);

                // Update ChangeLog token if sync has been successful.
                if (success)
                {
                    database.SetChangeLogToken(token);
                }
                else
                {
                    Logger.Info("ChangeLog token not updated as an error occurred during sync.");
                }
            }
            finally
            {
                Logger.InfoFormat("Remote Full Crawl Finished : {0} min", sw.Elapsed);
            }
            return success;
        }


        /// <summary>
        /// Crawl remote content, syncing down if needed.
        /// Meanwhile, cache remoteFiles and remoteFolders, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
        /// </summary>
        private bool CrawlRemote(IFolder remoteFolder, string remotePath, string localFolder, IList<string> localFilenameTranslationOfExistingRemoteDocuments, IList<string> remoteFolders)
        {
            bool success = true;
            SleepWhileSuspended();

            // Remember seen names, and depending on the profile ignore the ones that have appeared already.
            // For instance, if the CMIS server has a file called Hello and a folder called HELLO, then on Windows the paths would conflict.
            // Store lowercase file and folder names inside.
            HashSet<string> names = new HashSet<string>();

            // Get all remote children.
            // TODO: use paging
            IOperationContext operationContext = session.CreateOperationContext();
            operationContext.FilterString = "*";
            operationContext.IncludeAllowableActions = true;
            operationContext.IncludePolicies = true;
            operationContext.IncludeRelationships = IncludeRelationshipsFlag.Both;
            operationContext.IncludeAcls = true;
            // renditionFilter=* not available in DotCMIS
            this.repoInfo.CmisProfile.ConfigureOperationContext(operationContext);
            operationContext.MaxItemsPerPage = Int32.MaxValue;

            // Get children.
            IItemEnumerable<ICmisObject> childrenEnumeration = remoteFolder.GetChildren(operationContext);

            // TODO: debug
            System.Console.WriteLine ("Got children from {0} ", remotePath);
            foreach (ICmisObject cmisObject in childrenEnumeration) {
                System.Console.WriteLine ("cmis object in children enumeration: \n" +
                                          "  name: {0} \n" + 
                                          "  type: {1}", cmisObject.Name, cmisObject.GetType ().ToString ()); 
            }

            childrenEnumeration = remoteFolder.GetChildren (operationContext);
            // Log.
            /*IList<ICmisObject> children = childrenEnumeration.ToList(); // Because we need to enumerate twice (log+crawl).
            StringBuilder logBuilder = new StringBuilder();
            foreach (ICmisObject cmisObject in children)
            {
                logBuilder.Append(cmisObject.Name + " (" + cmisObject.GetType().Name + ") ");
            }
            Logger.Debug("Content of " + remotePath + ": " + logBuilder);*/

            // Crawl.
            foreach (ICmisObject cmisObject in childrenEnumeration /*children*/)
            {
                try
                {
                    if (cmisObject is DotCMIS.Client.Impl.Folder)
                    {
                        // It is a CMIS folder.
                        IFolder remoteSubFolder = (IFolder)cmisObject;

                        string remoteSubPath = CmisFileUtil.PathCombine(remotePath, remoteSubFolder.Name);
                        // TODO: debug
                        System.Console.WriteLine ("Found a remote sub folder: \n" +
                                                  "  sub folder path: {0}\n" +
                                                  "  remote path: {1}\n" +
                                                  "  remote subfolder name: {2}\n" +
                                                  "  remote subfolder path: {3}",
                                                  remoteSubPath, remotePath, remoteSubFolder.Name, remoteSubFolder.Path);

                        // Ignore edgy folders.
                        if (repoInfo.CmisProfile.IgnoreIfSameLowercaseNames && names.Contains(remoteSubFolder.Name.ToLowerInvariant()))
                        {
                            Logger.Warn("Ignoring " + remoteSubFolder.Name + "because other file or folder has the same name when ignoring lowercase/uppercase");
                            continue;
                        }
                        if (!repoInfo.CmisProfile.RemoteObjectWorthSyncing(remoteSubFolder))
                        {
                            continue;
                        }

                        CrawlRemoteFolder(remoteSubFolder, remoteSubPath, localFolder, remoteFolders);
                        names.Add(remoteSubFolder.Name.ToLowerInvariant());
                    }
                    else if (cmisObject is DotCMIS.Client.Impl.Document)
                    {
                        // It is a CMIS document.
                        IDocument remoteDocument = (IDocument)cmisObject;

                        string remoteDocumentPath = CmisFileUtil.PathCombine(remotePath, remoteDocument.Name);

                        // Ignore edgy documents.
                        if (repoInfo.CmisProfile.IgnoreIfSameLowercaseNames &&
                            names.Contains(repoInfo.CmisProfile.localFilename(remoteDocument).ToLowerInvariant()))
                        {
                            Logger.Warn("Ignoring " + repoInfo.CmisProfile.localFilename(remoteDocument) + "because other file or folder has the same name when ignoring lowercase/uppercase");
                            continue;
                        }
                        if (!repoInfo.CmisProfile.RemoteObjectWorthSyncing(remoteDocument))
                        {
                            continue;
                        }

                        // TODO: debug
                        System.Console.WriteLine ("Crawl Remote Document: \n" +
                                                  "  remotePath: {0}\n" +
                                                  "  remoteDocumentName: {1}\n" +
                                                  "  remoteDocumentPath: {2}\n" +
                                                  "  localFolder: {3}",
                                                  remotePath, remoteDocument.Name, remoteDocumentPath, localFolder);

                        CrawlRemoteDocument(remoteDocument, remoteDocumentPath, localFolder, localFilenameTranslationOfExistingRemoteDocuments);
                        names.Add(remoteDocument.Name.ToLowerInvariant());
                    }
                    else if (isLink(cmisObject))
                    {
                        Logger.Debug("Ignoring file '" + remoteFolder + "/" + cmisObject.Name + "' of type '" +
                            cmisObject.ObjectType.Description + "'. Links are not currently handled.");
                    }
                    else
                    {
                        Logger.Warn("Unknown object type: '" + cmisObject.ObjectType.Description + "' (" + cmisObject.ObjectType.DisplayName
                            + ") for object " + remoteFolder + "/" + cmisObject.Name);
                    }
                }
                catch (CmisBaseException e)
                {
                    ProcessRecoverableException("Could not access remote object: " + cmisObject.Name, e);
                    success = false;
                }
            }
            return success;
        }

        private bool isLink(ICmisObject cmisObject)
        {
            IObjectType parent = cmisObject.ObjectType.GetParentType();
            while (parent != null)
            {
                if (parent.Id.Equals("I:cm:link"))
                {
                    return true;
                }
                parent = parent.GetParentType();
            }
            return false;
        }

        /// <summary>
        /// Crawl remote subfolder, syncing down if needed.
        /// Meanwhile, cache all contained remote folders, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
        /// </summary>
        private void CrawlRemoteFolder(IFolder remoteSubFolder, string remotePath, string localFolder, IList<string> remoteFolders)
        {
            SleepWhileSuspended();

            try
            {
                // Get the sub-folder's would-be local name.
                var subFolderItem = database.GetFolderSyncItemFromRemotePath(remoteSubFolder.Path);
                if (null == subFolderItem)
                {
                    subFolderItem = SyncItemFactory.CreateFromRemoteFolder(remoteSubFolder.Path, repoInfo, database);
                }

                if (Utils.WorthSyncing(localFolder, subFolderItem.LocalLeafname, repoInfo))
                {
                    Logger.Info("CrawlRemote localFolder:\"" + localFolder + "\" remoteSubFolder.Path:\"" + remoteSubFolder.Path + "\" remoteSubFolder.Name:\"" + remoteSubFolder.Name + "\"");
                    remoteFolders.Add(remoteSubFolder.Name);

                    // Check whether local folder exists.
                    if (Directory.Exists(subFolderItem.LocalPath))
                    {
                        try
                        {
                            activityListener.ActivityStarted();
                            // Recurse into folder.
                            CrawlSync(remoteSubFolder, remotePath, subFolderItem.LocalPath);
                        }
                        finally
                        {
                            activityListener.ActivityStopped();
                        }
                    }
                    else
                    {
                        // Maybe the whole synchronized folder has disappeared?
                        // While rare for normal filesystems, that happens rather often with mounted folders (for instance encrypted folders)
                        // In such a case, we should abort this synchronization rather than delete the remote subfolder.
                        if (!Directory.Exists(repoInfo.TargetDirectory))
                        {
                            throw new Exception("Local folder has disappeared: " + repoInfo.TargetDirectory + " , aborting synchronization");
                        }

                        // If there was previously a file with this name, delete it.
                        // TODO warn if local changes in the file.
                        if (File.Exists(subFolderItem.LocalPath))
                        {
                            try
                            {
                                activityListener.ActivityStarted();
                                Utils.DeleteEvenIfReadOnly(subFolderItem.LocalPath);
                            }
                            finally
                            {
                                activityListener.ActivityStopped();
                            }
                        }

                        if (database.ContainsFolder(subFolderItem))
                        {
                            try
                            {
                                activityListener.ActivityStarted();

                                // If there was previously a folder with this name, it means that
                                // the user has deleted it voluntarily, so delete it from server too.

                                DeleteRemoteFolder(remoteSubFolder, subFolderItem, remotePath);
                            }
                            finally
                            {
                                activityListener.ActivityStopped();
                            }
                        }
                        else
                        {
                            try
                            {
                                // The folder has been recently created on server, so download it.
                                activityListener.ActivityStarted();
                                Directory.CreateDirectory(subFolderItem.LocalPath);

                                // Create database entry for this folder.
                                // TODO - Yannick - Add metadata
                                database.AddFolder(subFolderItem, remoteSubFolder.Id, remoteSubFolder.LastModificationDate);
                                Logger.Info("Added folder to database: " + subFolderItem.LocalPath);

                                // Recursive copy of the whole folder.
                                RecursiveFolderCopy(remoteSubFolder, remotePath, subFolderItem.LocalPath);

                            }
                            finally
                            {
                                activityListener.ActivityStopped();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                activityListener.ActivityStopped();
                ProcessRecoverableException("Could not crawl sync remote folder: " + remoteSubFolder.Path, e);
            }
        }

        /// <summary>
        /// Check remote document, syncing down if needed.
        /// Meanwhile, cache remoteFiles, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
        /// </summary>
        /// <param name="localFilenameTranslationOfExistingRemoteDocuments">output collection where the method will store any remote file. Specify null if not needed.</param>
        private bool CrawlRemoteDocument(IDocument remoteDocument, string remotePath, string localFolder, IList<string> localFilenameTranslationOfExistingRemoteDocuments)
        {
            SleepWhileSuspended();
            bool success = true;

            if (Utils.WorthSyncing(localFolder, repoInfo.CmisProfile.localFilename(remoteDocument), repoInfo))
            {
                // We use the filename of the document's content stream.
                // This can be different from the name of the document.
                // For instance in FileNet it is not unusual to have a document where
                // document.Name is "foo" and document.ContentStreamFileName is "foo.jpg".
                string localFilenameOfRemoteDocument = repoInfo.CmisProfile.localFilename(remoteDocument);
                //Logger.Debug("CrawlRemote doc: " + localFolder + CmisUtils.CMIS_FILE_SEPARATOR + remoteDocumentFileName);

                // If this file does not have a filename, ignore it.
                // It sometimes happen on IBM P8 CMIS server, not sure why.
                if (localFilenameOfRemoteDocument == null)
                {
                    Logger.Warn("Skipping download of '" + repoInfo.CmisProfile.localFilename(remoteDocument) + "' with null content stream in " + localFolder);
                    return true;
                }

                if (localFilenameTranslationOfExistingRemoteDocuments != null)
                {
                    localFilenameTranslationOfExistingRemoteDocuments.Add(localFilenameOfRemoteDocument);
                }

                var paths = remoteDocument.Paths;
                var pathsCount = paths.Count;
                var syncItem = database.GetSyncItemFromRemotePath(remotePath);
                if (null == syncItem)
                {
                    syncItem = SyncItemFactory.CreateFromRemoteDocument(remotePath, repoInfo.CmisProfile.localFilename(remoteDocument), repoInfo, database);
                }

                if (syncItem.FileExistsLocal())
                {
                    // Check modification date stored in database and download if remote modification date if different.
                    DateTime? serverSideModificationDate = ((DateTime)remoteDocument.LastModificationDate).ToUniversalTime();
                    DateTime? lastDatabaseUpdate = database.GetServerSideModificationDate(syncItem);
                    //Logger.Debug("serverSideModificationDate:" + serverSideModificationDate
                    //    + " lastDatabaseUpdate:" + lastDatabaseUpdate);

                    if (lastDatabaseUpdate == null)
                    {
                        Logger.Info("Downloading file absent from database: " + syncItem.LocalPath);
                        activityListener.ActivityStarted();
                        success &= DownloadFile(remoteDocument, remotePath, localFolder);
                        activityListener.ActivityStopped();
                    }
                    else
                    {
                        // If the file has been modified since last time we downloaded it, then download again.
                        if (serverSideModificationDate > lastDatabaseUpdate)
                        {
                            activityListener.ActivityStarted();

                            if (database.LocalFileHasChanged(syncItem.LocalPath))
                            {
                                Logger.Info("Conflict with file: " + localFilenameOfRemoteDocument + ", backing up locally modified version and downloading server version");
                                Logger.Info("- serverSideModificationDate: " + serverSideModificationDate);
                                Logger.Info("- lastDatabaseUpdate: " + lastDatabaseUpdate);
                                Logger.Info("- Checksum in database: " + database.GetChecksum(syncItem.LocalPath));
                                Logger.Info("- Checksum of local file: " + Database.Database.Checksum(syncItem.LocalPath));

                                // Rename locally modified file.
                                String newFilePath = Utils.CreateConflictFilename(syncItem.LocalPath, repoInfo.User);
                                File.Move(syncItem.LocalPath, newFilePath);

                                // Download server version
                                success &= DownloadFile(remoteDocument, remotePath, localFolder);
                                Logger.Info("- Checksum of remote file: " + Database.Database.Checksum(syncItem.LocalPath));
                                repo.OnConflictResolved();

                                // Notify the user.
                                string lastModifiedBy = CmisUtils.GetProperty(remoteDocument, "cmis:lastModifiedBy");
                                string message = String.Format(
                                    // Properties_Resources.ResourceManager.GetString("ModifiedSame", CultureInfo.CurrentCulture),
                                    "User {0} modified file \"{1}\" at the same time as you.",
                                    lastModifiedBy, syncItem.LocalPath)
                                    + "\n\n"
                                    // + Properties_Resources.ResourceManager.GetString("YourVersion", CultureInfo.CurrentCulture);
                                    + "Your version has been saved as \"" + newFilePath + "\", please merge your important changes from it and then delete it.";
                                Logger.Info(message);
                                UserNotificationListenerUtil.NotifyUser(message);
                            }
                            else
                            {
                                Logger.Info("Downloading modified file: " + localFilenameOfRemoteDocument);
                                success &= DownloadFile(remoteDocument, remotePath, localFolder);
                            }

                            activityListener.ActivityStopped();
                        }
                    }
                }
                else if (Directory.Exists(syncItem.LocalPath))
                {
                    // With the same name have been created: a document on the server, and a folder locally.
                    // This is a conflict.
                    // Rename the local and download the remote.

                    Logger.Info("Conflict between remote document \"" + localFilenameOfRemoteDocument
                        + "\" and local folder \"" + syncItem.LocalPath
                        + "\", renaming local folder and downloading remote document");

                    // Rename local folder.
                    String newFolderPath = Utils.CreateConflictFoldername(syncItem.LocalPath, repoInfo.User);
                    Directory.Move(syncItem.LocalPath, newFolderPath);

                    // Download server document
                    success &= DownloadFile(remoteDocument, remotePath, localFolder);
                    repo.OnConflictResolved();

                    // Notify the user.
                    string lastModifiedBy = CmisUtils.GetProperty(remoteDocument, "cmis:lastModifiedBy");
                    string message = String.Format(
                        // Properties_Resources.ResourceManager.GetString("ModifiedSame", CultureInfo.CurrentCulture),
                        "User {0} has created document \"{1}\" at the same time as you created a folder with the same name.",
                        lastModifiedBy, syncItem.LocalPath)
                        + "\n\n"
                        // + Properties_Resources.ResourceManager.GetString("YourVersion", CultureInfo.CurrentCulture);
                        + "Your folder has been saved as \"" + newFolderPath + "\", please rename it.";
                    Logger.Info(message);
                    UserNotificationListenerUtil.NotifyUser(message);
                }
                else
                {
                    // The remote file does not exist on the local filesystem.

                    // Maybe the whole synchronized folder has disappeared?
                    // While rare for normal filesystems, that happens rather often with mounted folders (for instance encrypted folders)
                    // In such a case, we should abort this synchronization rather than delete the remote file.
                    if (!Directory.Exists(repoInfo.TargetDirectory))
                    {
                        throw new Exception("Local target directory has disappeared: " + repoInfo.TargetDirectory + " , aborting synchronization");
                    }

                    if (database.ContainsLocalFile(syncItem.LocalRelativePath))
                    {
                        // The file used to be present locally (as revealed by the database), but does not exist anymore locally.
                        // So, it must have been deleted locally by the user.
                        // Thus, CmisSync must remove the file from the server too.

                        success &= DeleteRemoteDocument(remoteDocument, syncItem);

                    }
                    else
                    {
                        // New remote file, download it.

                        Logger.Info("New remote file: " + remoteDocument.Name);
                        activityListener.ActivityStarted();
                        success &= DownloadFile(remoteDocument, remotePath, localFolder);
                        activityListener.ActivityStopped();
                    }
                }
            }
            return success;
        }


        /// <summary>
        /// Crawl local files in a given directory (not recursive).
        /// </summary>
        private void CheckLocalFiles(string localFolder, IFolder remoteFolder, IList<string> localFilenameTranslationOfExistingRemoteDocuments)
        {
            SleepWhileSuspended();

            string[] files;
            try
            {
                files = Directory.GetFiles(localFolder);
            }
            catch (Exception e)
            {
                Logger.Warn("Could not get the file list from folder: " + localFolder, e);
                return;
            }

            foreach (string filePath in files)
            {
                CheckLocalFile(filePath, remoteFolder, localFilenameTranslationOfExistingRemoteDocuments);
            }
        }

        /// <summary>
        /// Check a local file in a given directory (not recursive).
        /// </summary>
        /// <param name="localFilenameTranslationOfExistingRemoteDocuments">Remove the file if it is not in this list of remote files (translated to local filenames). Ignored if null</param>
        private bool CheckLocalFile(string localFilePath, IFolder remoteFolder, IList<string> localFilenameTranslationOfExistingRemoteDocuments)
        {
            SleepWhileSuspended();
            bool success = true;

            try
            {
                if (Utils.IsSymlink(new FileInfo(localFilePath)))
                {
                    Logger.Info("Skipping symbolic linked file: " + localFilePath);
                    return true;
                }

                var item = database.GetSyncItemFromLocalPath(localFilePath);
                if (null == item)
                {
                    // The file has been recently created locally (not synced from server).
                    item = SyncItemFactory.CreateFromLocalPath(localFilePath, false, repoInfo, database);
                }

                string fileName = item.LocalLeafname;

                if (Utils.WorthSyncing(Path.GetDirectoryName(localFilePath), fileName, repoInfo))
                {
                    if (localFilenameTranslationOfExistingRemoteDocuments != null &&
                            ! localFilenameTranslationOfExistingRemoteDocuments.Contains(fileName))
                    {
                        // This local file is not on the CMIS server now, so
                        // check whether it used to exist on server or not.
                        if (database.ContainsLocalFile(localFilePath))
                        {
                            if (database.LocalFileHasChanged(localFilePath))
                            {
                                // If file has changed locally, move to 'your_version' and warn about conflict
                                if (BIDIRECTIONAL)
                                {
                                    // Local file was updated, sync up.
                                    Logger.Info("Uploading locally edited remotely removed file from the repository: " + localFilePath);
                                    activityListener.ActivityStarted();
                                    success &= UploadFile(localFilePath, remoteFolder);
                                    activityListener.ActivityStopped();
                                }
                                else
                                {
                                    Logger.Info("Conflict with file: " + localFilePath + ", backing up locally modified version.");
                                    activityListener.ActivityStarted();
                                    // Rename locally modified file.
                                    String newFilePath = Utils.CreateConflictFilename(localFilePath, repoInfo.User);

                                    // The file might be ReadOnly, so make it writable first, otherwise the move will fail.
                                    File.SetAttributes(localFilePath, FileAttributes.Normal); // TODO use Utils.DeleteEvenIfReadOnly

                                    File.Move(localFilePath, newFilePath);

                                    // Delete file from database.
                                    database.RemoveFile(item);

                                    repo.OnConflictResolved();
                                    activityListener.ActivityStopped();
                                }
                            }
                            else
                            {
                                // File has been deleted on server, so delete it locally.
                                Logger.Info("Removing remotely deleted file: " + localFilePath);
                                activityListener.ActivityStarted();

                                // The file might be ReadOnly, so make it writable first, otherwise removal will fail.
                                File.SetAttributes(localFilePath, FileAttributes.Normal); // TODO use Utils.DeleteEvenIfReadOnly

                                // Delete from the local filesystem.
                                File.Delete(localFilePath);

                                // Delete file from database.
                                database.RemoveFile(item);

                                activityListener.ActivityStopped();
                            }
                        }
                        else
                        {
                            if (BIDIRECTIONAL)
                            {
                                // New file, sync up.
                                Logger.Info("Uploading file absent on repository: " + localFilePath);
                                activityListener.ActivityStarted();
                                success &= UploadFile(localFilePath, remoteFolder);
                                activityListener.ActivityStopped();
                            }
                        }
                    }
                    else
                    {
                        // The file exists both on server and locally.
                        if (database.LocalFileHasChanged(localFilePath))
                        {
                            if (BIDIRECTIONAL)
                            {
                                // Upload new version of file content.
                                Logger.Info("Uploading file update on repository: " + localFilePath);
                                activityListener.ActivityStarted();
                                success &= UpdateFile(localFilePath, remoteFolder);
                                activityListener.ActivityStopped();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ProcessRecoverableException("Could not crawl sync local file: " + localFilePath, e);
                success = false;
            }
            return success;
        }


        /// <summary>
        /// Crawl local folders in a given directory (not recursive).
        /// </summary>
        private void CheckLocalFolders(string localFolder, IFolder remoteRoot, IList<string> remoteFolders)
        {
            SleepWhileSuspended();

            string[] folders;
            try
            {
                folders = Directory.GetDirectories(localFolder);
            }
            catch (Exception e)
            {
                Logger.Warn(String.Format("Exception while get the folder list from folder {0}", localFolder), e);
                return;
            }

            foreach (string localSubFolder in folders)
            {
                CheckLocalFolder(localSubFolder, remoteRoot, remoteFolders);
            }
        }

        /// <summary>
        /// Check a particular local folder (not recursive).
        /// See whether it has been deleted locally or not.
        /// </summary>
        private void CheckLocalFolder(string localSubFolder, IFolder remoteRoot, IList<string> remoteFolders)
        {
            SleepWhileSuspended();
            try
            {
                if (Utils.IsSymlink(new DirectoryInfo(localSubFolder)))
                {
                    Logger.Info("Skipping symbolic link folder: " + localSubFolder);
                    return;
                }

                string folderName = Path.GetFileName(localSubFolder);

                if (Utils.WorthSyncing(Path.GetDirectoryName(localSubFolder), folderName, repoInfo))
                {
                    var syncFolderItem = database.GetFolderSyncItemFromLocalPath(localSubFolder);
                    if (null == syncFolderItem)
                    {
                        // The local item is not in database. It has not been uploaded yet.
                        syncFolderItem = SyncItemFactory.CreateFromLocalPath(localSubFolder, true, repoInfo, database);
                    }

                    if (remoteFolders.Contains(syncFolderItem.RemoteLeafname))
                    {
                        // The same folder exists locally and remotely.
                        // Are they synchronized, or were they just created (or moved) at the same time?
                        if (database.ContainsFolder(syncFolderItem))
                        {
                            // Check modification dates of local folder and remote folder.
                            // TODO
                        }
                        else
                        {
                            // The folder just appeared both on the local and remote sides.
                            // Rename local folder then download remote folder.

                            IFolder remoteFolder = (IFolder)session.GetObjectByPath(syncFolderItem.RemotePath, true);

                            var path = syncFolderItem.LocalPath;
                            var newPath = Utils.CreateConflictFoldername(path, repoInfo.User);

                            Directory.Move(path, newPath);

                            // Delete file from database.
                            database.RemoveFile(syncFolderItem);

                            repo.OnConflictResolved();

                            // Download folder from server.
                            DownloadDirectory(remoteFolder, syncFolderItem.RemotePath, path);
                        }
                    }
                    else
                    {
                        // This local folder is not on the CMIS server now, so
                        // check whether it used to exist on server or not.
                        if (database.ContainsFolder(syncFolderItem))
                        {
                            activityListener.ActivityStarted();
                            RemoveFolderLocally(localSubFolder);
                            activityListener.ActivityStopped();
                        }
                        else
                        {
                            // New local folder, upload recursively.
                            activityListener.ActivityStarted();
                            UploadFolderRecursively(remoteRoot, localSubFolder);
                            activityListener.ActivityStopped();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ProcessRecoverableException("Could not crawl sync local folder: " + localSubFolder, e);
            }
        }
    }
}
