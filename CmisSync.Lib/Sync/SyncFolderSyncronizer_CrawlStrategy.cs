using DotCMIS.Client;
using DotCMIS.Exceptions;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using DotCMIS.Client.Impl;
using CmisSync.Lib.Cmis;


namespace CmisSync.Lib.Sync
{
    /// <summary>
    /// Part of CmisRepo.
    /// </summary>
    public partial class SyncFolderSyncronizer : SyncFolderSyncronizerBase
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
        private bool CrawlSync(IFolder remoteFolder, string localFolder)
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
            IList<string> remoteFiles = new List<string>();
            IList<string> remoteSubfolders = new List<string>();

            // Crawl remote children.
            // Logger.LogInfo("Sync", String.Format("Crawl remote folder {0}", this.remoteFolderPath));
            bool success = CrawlRemote(remoteFolder, localFolder, remoteFiles, remoteSubfolders);

            // Crawl local files.
            // Logger.LogInfo("Sync", String.Format("Crawl local files in the local folder {0}", localFolder));
            CrawlLocalFiles(localFolder, remoteFolder, remoteFiles);

            // Crawl local folders.
            // Logger.LogInfo("Sync", String.Format("Crawl local folder {0}", localFolder));
            CrawlLocalFolders(localFolder, remoteFolder, remoteSubfolders);

            return success;
        }


        private void CrawlSyncAndUpdateChangeLogToken(IFolder remoteFolder, string localFolder)
        {
            // Get ChangeLog token.
            string token = CmisUtils.GetChangeLogToken(getSession());

            // Sync.
            bool success = CrawlSync(remoteFolder, localFolder);

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


        /// <summary>
        /// Crawl remote content, syncing down if needed.
        /// Meanwhile, cache remoteFiles and remoteFolders, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
        /// </summary>
        private bool CrawlRemote(IFolder remoteFolder, string localFolder, IList<string> remoteFiles, IList<string> remoteFolders)
        {
            bool success = true;
            SleepWhileSuspended();

            // Get all remote children.
            // TODO: use paging
            IOperationContext operationContext = getSession().CreateOperationContext();
            operationContext.MaxItemsPerPage = Int32.MaxValue;
            Logger.Debug("CrawlRemote(remoteFolder=\"" + remoteFolder.Path + "\")");
            foreach (ICmisObject cmisObject in remoteFolder.GetChildren(operationContext))
            {
                try
                {
                    if (cmisObject is DotCMIS.Client.Impl.Folder)
                    {
                        // It is a CMIS folder.
                        IFolder remoteSubFolder = (IFolder)cmisObject;
                        CrawlRemoteFolder(remoteSubFolder, localFolder, remoteFolders);
                    }
                    else if (cmisObject is DotCMIS.Client.Impl.Document)
                    {
                        // It is a CMIS document.
                        IDocument remoteDocument = (IDocument)cmisObject;
                        CrawlRemoteDocument(remoteDocument, localFolder, remoteFiles);
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
                    HandleException(new RemoteObjectException(cmisObject, e));
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
        private void CrawlRemoteFolder(IFolder remoteSubFolder, string localFolder, IList<string> remoteFolders)
        {
            SleepWhileSuspended();

            try
            {
                if (SyncUtils.IsWorthSyncing(localFolder, remoteSubFolder.Name, SyncFolderInfo))
                {
                    // Logger.Debug("CrawlRemote localFolder:\"" + localFolder + "\" remoteSubFolder.Path:\"" + remoteSubFolder.Path + "\" remoteSubFolder.Name:\"" + remoteSubFolder.Name + "\"");
                    remoteFolders.Add(remoteSubFolder.Name);

                    SyncItem subFolderItem = getSyncItemFromRemotePath(remoteSubFolder.Path);

                    // Check whether local folder exists.
                    if (Directory.Exists(subFolderItem.LocalPath))
                    {
                        // Recurse into folder.
                        CrawlSync(remoteSubFolder, subFolderItem.LocalPath);
                    }
                    else
                    {
                        // If there was previously a file with this name, delete it.
                        // TODO warn if local changes in the file.
                        if (File.Exists(subFolderItem.LocalPath))
                        {

                            File.Delete(subFolderItem.LocalPath);

                        }

                        if (database.ContainsFolder(subFolderItem))
                        {
                            // If there was previously a folder with this name, it means that
                            // the user has deleted it voluntarily, so delete it from server too.



                            // Delete the folder from the remote server.
                            try
                            {
                                Logger.Debug("Removing remote folder tree: " + remoteSubFolder.Path);
                                IList<string> failedIDs = remoteSubFolder.DeleteTree(true, null, true);
                                if (failedIDs == null || failedIDs.Count != 0)
                                {
                                    Logger.Error("Failed to completely delete remote folder " + remoteSubFolder.Path);
                                    // TODO Should we retry? Maybe at least once, as a manual recursion instead of a DeleteTree.
                                }
                            }
                            catch (CmisPermissionDeniedException e)
                            {
                                HandleException(new DeleteRemoteFolderException(remoteSubFolder.Path, e));
                                //restore the folder
                                //TODO: check permissions before deleting the folder 
                                DownloadFolder(remoteSubFolder, localFolder);
                                return;
                            }

                            // Delete the folder from database.
                            database.RemoveFolder(subFolderItem);


                        }
                        else
                        {
                            if (SyncUtils.IsInvalidFileName(remoteSubFolder.Name))
                            {
                                Logger.Warn("Skipping remote folder with name invalid on local filesystem: " + remoteSubFolder.Name);
                            }
                            else
                            {
                                // The folder has been recently created on server, so download it.

                                Directory.CreateDirectory(subFolderItem.LocalPath);

                                // Create database entry for this folder.
                                // TODO - Yannick - Add metadata
                                database.AddFolder(subFolderItem, remoteSubFolder.Id, remoteSubFolder.LastModificationDate);
                                Logger.Info("Added folder to database: " + subFolderItem.LocalPath);

                                // Recursive copy of the whole folder.
                                RecursiveFolderCopy(remoteSubFolder, subFolderItem.LocalPath);


                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {

                HandleException(new DownloadFolderException(remoteSubFolder.Path, e));
            }
        }

        /// <summary>
        /// Crawl remote document, syncing down if needed.
        /// Meanwhile, cache remoteFiles, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
        /// </summary>
        private void CrawlRemoteDocument(IDocument remoteDocument, string localFolder, IList<string> remoteFiles)
        {
            SleepWhileSuspended();

            if (SyncUtils.IsWorthSyncing(localFolder, remoteDocument.Name, SyncFolderInfo))
            {
                // We use the filename of the document's content stream.
                // This can be different from the name of the document.
                // For instance in FileNet it is not unusual to have a document where
                // document.Name is "foo" and document.ContentStreamFileName is "foo.jpg".
                string remoteDocumentFileName = remoteDocument.ContentStreamFileName;
                //Logger.Debug("CrawlRemote doc: " + localFolder + CmisPath.CMIS_FILE_SEPARATOR + remoteDocumentFileName);

                // If this file does not have a filename, ignore it.
                // It sometimes happen on IBM P8 CMIS server, not sure why.
                if (remoteDocumentFileName == null)
                {
                    Logger.Warn("Skipping download of '" + remoteDocument.Name + "' with null content stream in " + localFolder);
                    return;
                }

                remoteFiles.Add(remoteDocumentFileName);

                var paths = remoteDocument.Paths;
                var pathsCount = paths.Count;
                SyncItem syncItem = getSyncItemFromRemotePath(remoteDocument.Paths[0]);

                if (syncItem.ExistsLocal())
                {
                    // Check modification date stored in database and download if remote modification date if different.
                    DateTime? serverSideModificationDate = getUtcLastModificationDate(remoteDocument);
                    DateTime? lastDatabaseUpdate = database.GetServerSideModificationDate(syncItem);

                    if (lastDatabaseUpdate == null)
                    {
                        Logger.Info("Downloading file absent from database: " + syncItem.LocalPath);

                        DownloadFile(remoteDocument, localFolder);

                    }
                    else
                    {
                        // If the file has been modified since last time we downloaded it, then download again.
                        if (serverSideModificationDate > lastDatabaseUpdate)
                        {
                            if (database.LocalFileHasChanged(syncItem.LocalPath))
                            {
                                Logger.Info("Conflict with file: " + remoteDocumentFileName + ", backing up locally modified version and downloading server version");
                                Logger.Info("- serverSideModificationDate: " + serverSideModificationDate);
                                Logger.Info("- lastDatabaseUpdate: " + lastDatabaseUpdate);
                                Logger.Info("- Checksum in database: " + database.GetChecksum(syncItem.LocalPath));
                                Logger.Info("- Checksum of local file: " + Database.Database.Checksum(syncItem.LocalPath));

                                // Rename locally modified file.
                                String newFilePath = SyncUtils.CreateConflictFilename(syncItem.LocalPath, SyncFolderInfo.Account.Credentials.UserName);
                                File.Move(syncItem.LocalPath, newFilePath);

                                // Download server version
                                DownloadFile(remoteDocument, localFolder);
                                Logger.Info("- Checksum of remote file: " + Database.Database.Checksum(syncItem.LocalPath));

                                // Notify the user.
                                string lastModifiedBy = CmisUtils.GetProperty(remoteDocument, "cmis:lastModifiedBy");
                                HandleException(new FileConflictException(syncItem.LocalPath, lastModifiedBy, newFilePath));
                            }
                            else
                            {
                                Logger.Info("Downloading modified file: " + remoteDocumentFileName);
                                DownloadFile(remoteDocument, localFolder);
                            }


                        }
                    }
                }
                else
                {
                    if (database.ContainsFile(syncItem))
                    {
                        if (!(bool)remoteDocument.IsVersionSeriesCheckedOut)
                        {
                            // File has been recently removed locally, so remove it from server too.
                            Logger.Info("Removing locally deleted file on server: " + syncItem.RemotePath);
                            remoteDocument.DeleteAllVersions();
                            // Remove it from database.
                            database.RemoveFile(syncItem);
                        }
                        else
                        {
                            HandleException(new CheckOutFileException(syncItem.LocalPath, remoteDocument.CheckinComment));
                        }
                    }
                    else
                    {
                        // New remote file, download it.
                        Logger.Info("New remote file: " + syncItem.RemotePath);

                        DownloadFile(remoteDocument, localFolder);

                    }
                }
            }
        }

        /// <summary>
        /// Crawl local files in a given directory (not recursive).
        /// </summary>
        private void CrawlLocalFiles(string localFolder, IFolder remoteFolder, IList<string> remoteFiles)
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
                CrawlLocalFile(filePath, remoteFolder, remoteFiles);
            }
        }

        /// <summary>
        /// Crawl local file in a given directory (not recursive).
        /// </summary>
        private void CrawlLocalFile(string filePath, IFolder remoteFolder, IList<string> remoteFiles)
        {
            SleepWhileSuspended();

            try
            {
                if (SyncUtils.IsSymlink(new FileInfo(filePath)))
                {
                    Logger.Info("Skipping symbolic linked file: " + filePath);
                    return;
                }

                SyncItem item = getSyncItemFromLocalPath(filePath);
               
                // string fileName = Path.GetFileName(filePath);
                string fileName = item.RemoteFileName;

                if (SyncUtils.IsWorthSyncing(Path.GetDirectoryName(filePath), fileName, SyncFolderInfo))
                {
                    if (!remoteFiles.Contains(fileName))
                    {
                        // This local file is not on the CMIS server now, so
                        // check whether it used invalidFolderNameRegex to exist on server or not.
                        if (database.ContainsFile(SyncItemFactory.CreateFromLocalPath(filePath, SyncFolderInfo)))
                        {
                            if (database.LocalFileHasChanged(filePath))
                            {
                                // If file has changed locally, move to 'your_version' and warn about conflict
                                if (bidirectionalSync)
                                {
                                    // Local file was updated, sync up.
                                    Logger.Info("Uploading locally edited remotely removed file from the repository: " + filePath);

                                    UploadFile(filePath, remoteFolder);

                                }
                                else
                                {
                                    Logger.Info("Conflict with file: " + filePath + ", backing up locally modified version.");

                                    // Rename locally modified file.
                                    String newFilePath = SyncUtils.CreateConflictFilename(filePath, SyncFolderInfo.Account.Credentials.UserName);
                                    File.Move(filePath, newFilePath);

                                    // Delete file from database.
                                    database.RemoveFile(item);

                                }
                            }
                            else
                            {
                                // File has been deleted on server, so delete it locally.
                                Logger.Info("Removing remotely deleted file: " + filePath);

                                File.Delete(filePath);

                                // Delete file from database.
                                database.RemoveFile(item);


                            }
                        }
                        else
                        {
                            if (bidirectionalSync)
                            {
                                // New file, sync up.
                                Logger.Info("Uploading file absent on repository: " + filePath);

                                UploadFile(filePath, remoteFolder);

                            }
                        }
                    }
                    else
                    {
                        // The file exists both on server and locally.
                        if (database.LocalFileHasChanged(filePath))
                        {
                            if (bidirectionalSync)
                            {
                                // Upload new version of file content.
                                Logger.Info("Uploading file update on repository: " + filePath);

                                UpdateFile(filePath, remoteFolder);

                            }
                        }
                    }
                }
                else 
                {
                    if (SyncUtils.IsConflictFile(fileName))
                    {
                        //TODO: too heavy
                        bool conflicFileCreatedByThisSyncRun = false;
                        foreach (SyncronizerEvent e in Events) {
                            if (e.Exception != null && e.Exception is FileConflictException && Object.Equals(((FileConflictException)e.Exception).ConflictFilename, filePath))
                            {
                                conflicFileCreatedByThisSyncRun = true;
                            }
                        }
                        if (!conflicFileCreatedByThisSyncRun)
                        {
                            NotifySyncEvent(EventLevel.WARN, new ConflictFileStillPresentException(filePath));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new DownloadFileException(filePath, e));
            }
        }


        /// <summary>
        /// Crawl local folders in a given directory (not recursive).
        /// </summary>
        private void CrawlLocalFolders(string localFolder, IFolder remoteFolder, IList<string> remoteFolders)
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
                CrawlLocalFolder(localSubFolder, remoteFolder, remoteFolders);
            }
        }

        /// <summary>
        /// Crawl local folder in a given directory (not recursive).
        /// </summary>
        private void CrawlLocalFolder(string localSubFolder, IFolder remoteFolder, IList<string> remoteFolders)
        {
            SleepWhileSuspended();
            try
            {
                if (SyncUtils.IsSymlink(new DirectoryInfo(localSubFolder)))
                {
                    Logger.Info("Skipping symbolic link folder: " + localSubFolder);
                    return;
                }

                string folderName = Path.GetFileName(localSubFolder);
                var syncFolderItem = database.GetFolderSyncItemFromLocalPath(localSubFolder);
                if (null == syncFolderItem)
                {
                    syncFolderItem = SyncItemFactory.CreateFromLocalPath(localSubFolder, SyncFolderInfo);
                }

                if (SyncUtils.IsWorthSyncing(Path.GetDirectoryName(localSubFolder), folderName, SyncFolderInfo))
                {
                    if (!remoteFolders.Contains(syncFolderItem.RemoteFileName))
                    {
                        // This local folder is not on the CMIS server now, so
                        // check whether it used to exist on server or not.
                        if (database.ContainsFolder(syncFolderItem))
                        {

                            RemoveFolderLocally(localSubFolder);

                        }
                        else
                        {
                            if (bidirectionalSync)
                            {
                                // New local folder, upload recursively.

                                UploadFolderRecursively(remoteFolder, localSubFolder);

                            }
                        }
                    }
                }

            }
            catch (Exception e)
            {
                HandleException(new DownloadFolderException(localSubFolder, e));
            }
        }
    }
}
