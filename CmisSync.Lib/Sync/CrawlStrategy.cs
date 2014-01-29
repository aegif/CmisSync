using DotCMIS.Client;
using DotCMIS.Exceptions;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Globalization;


namespace CmisSync.Lib.Sync
{
    /// <summary>
    /// Part of CmisRepo.
    /// </summary>
    public partial class CmisRepo : RepoBase
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
            private void CrawlSync(IFolder remoteFolder, string localFolder)
            {
                SleepWhileSuspended();

                // Lists of files/folders, to delete those that have been removed on the server.
                IList remoteFiles = new ArrayList();
                IList remoteSubfolders = new ArrayList();

                try
                {
                    // Crawl remote children.
                    // Logger.LogInfo("Sync", String.Format("Crawl remote folder {0}", this.remoteFolderPath));
                    CrawlRemote(remoteFolder, localFolder, remoteFiles, remoteSubfolders);

                    // Crawl local files.
                    // Logger.LogInfo("Sync", String.Format("Crawl local files in the local folder {0}", localFolder));
                    CrawlLocalFiles(localFolder, remoteFolder, remoteFiles);

                    // Crawl local folders.
                    // Logger.LogInfo("Sync", String.Format("Crawl local folder {0}", localFolder));
                    CrawlLocalFolders(localFolder, remoteFolder, remoteSubfolders);
                }
                catch (CmisBaseException e)
                {
                    ProcessRecoverableException("Could not crawl folder: " + remoteFolder.Path, e);
                }
            }


            /// <summary>
            /// Crawl remote content, syncing down if needed.
            /// Meanwhile, cache remoteFiles and remoteFolders, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
            /// </summary>
            private void CrawlRemote(IFolder remoteFolder, string localFolder, IList remoteFiles, IList remoteFolders)
            {
                SleepWhileSuspended();
                
                foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
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
                    else
                    {
                        Logger.Warn("Unknown object type: " + cmisObject.ObjectType.DisplayName);
                    }
                }
            }


            /// <summary>
            /// Crawl remote subfolder, syncing down if needed.
            /// Meanwhile, cache and remoteFolders, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
            /// </summary>
            private void CrawlRemoteFolder(IFolder remoteSubFolder, string localFolder, IList remoteFolders)
            {
                SleepWhileSuspended();

                try
                {
                    if (Utils.WorthSyncing(localFolder, remoteSubFolder.Name, repoinfo))
                    {
                        //Logger.Debug("CrawlRemote dir: " + localFolder + Path.DirectorySeparatorChar.ToString() + remoteSubFolder.Name);
                        remoteFolders.Add(remoteSubFolder.Name);
                        string localSubFolder = Path.Combine(localFolder, remoteSubFolder.Name);

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
                                Directory.CreateDirectory(localSubFolder);

                                // Create database entry for this folder.
                                // TODO - Yannick - Add metadata
                                database.AddFolder(localSubFolder, remoteSubFolder.LastModificationDate);
                                Logger.Info("Added folder to database: " + localSubFolder);

                                // Recursive copy of the whole folder.
                                RecursiveFolderCopy(remoteSubFolder, localSubFolder);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not crawl sync remote folder: " + remoteSubFolder.Path, e);
                }
            }

            /// <summary>
            /// Crawl remote document, syncing down if needed.
            /// Meanwhile, cache remoteFiles, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
            /// </summary>
            private void CrawlRemoteDocument(IDocument remoteDocument, string localFolder, IList remoteFiles)
            {
                SleepWhileSuspended();

                try
                {
                    if (Utils.WorthSyncing(localFolder, remoteDocument.Name, repoinfo))
                    {
                        // We use the filename of the document's content stream.
                        // This can be different from the name of the document.
                        // For instance in FileNet it is not usual to have a document where
                        // document.Name is "foo" and document.ContentStreamFileName is "foo.jpg".
                        string remoteDocumentFileName = remoteDocument.ContentStreamFileName;
                        //Logger.Debug("CrawlRemote doc: " + localFolder + Path.DirectorySeparatorChar.ToString() + remoteDocumentFileName);

                        // If this file does not have a filename, ignore it.
                        // It sometimes happen on IBM P8 CMIS server, not sure why.
                        if (remoteDocumentFileName == null)
                        {
                            Logger.Warn("Skipping download of '" + remoteDocument.Name + "' with null content stream in " + localFolder);
                            return;
                        }

                        remoteFiles.Add(remoteDocumentFileName);

                        string filePath = Path.Combine(localFolder, remoteDocumentFileName);

                        if (File.Exists(filePath))
                        {
                            // Check modification date stored in database and download if remote modification date if different.
                            DateTime? serverSideModificationDate = ((DateTime)remoteDocument.LastModificationDate).ToUniversalTime();
                            DateTime? lastDatabaseUpdate = database.GetServerSideModificationDate(filePath);

                            if (lastDatabaseUpdate == null)
                            {
                                Logger.Info("Downloading file absent from database: " + filePath);
                                DownloadFile(remoteDocument, localFolder);
                            }
                            else
                            {
                                // If the file has been modified since last time we downloaded it, then download again.
                                if (serverSideModificationDate > lastDatabaseUpdate)
                                {
                                    if (database.LocalFileHasChanged(filePath))
                                    {
                                        Logger.Info("Conflict with file: " + remoteDocumentFileName + ", backing up locally modified version and downloading server version");
                                        // Rename locally modified file.
                                        String newFilePath = Utils.ConflictPath(filePath);
                                        File.Move(filePath, newFilePath);

                                        // Download server version
                                        DownloadFile(remoteDocument, localFolder);
                                        repo.OnConflictResolved();

                                        // Get LastModifiedBy.
                                        IEnumerator<IProperty> e = remoteDocument.Properties.GetEnumerator();
                                        string lastModifiedBy = null;
                                        while (e.MoveNext())
	                                    {
	                                        IProperty property = e.Current;
	                                        if(property.Id.Equals("LastModifiedBy"))
                                            {
                                                lastModifiedBy = (string)property.Value;
                                                break;
                                            }
	                                    }

                                        string message = String.Format(
                                            // Properties_Resources.ResourceManager.GetString("ModifiedSame", CultureInfo.CurrentCulture),
                                            "User {0} modified the file {1} at the same time as you.",
                                            lastModifiedBy, filePath)
                                            + "\n\n"
                                            // + Properties_Resources.ResourceManager.GetString("YourVersion", CultureInfo.CurrentCulture);
                                            + "Your version has been saved with a '_your-version' suffix, please merge your important changes from it and then delete it.";
                                        Logger.Info(message);
                                        Utils.NotifyUser(message);
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
                            if (database.ContainsFile(filePath))
                            {
                                // File has been recently removed locally, so remove it from server too.
                                Logger.Info("Removing locally deleted file on server: " + filePath);
                                remoteDocument.DeleteAllVersions();
                                // Remove it from database.
                                database.RemoveFile(filePath);
                            }
                            else
                            {
                                // New remote file, download it.
                                Logger.Info("New remote file: " + filePath);
                                DownloadFile(remoteDocument, localFolder);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not crawl sync remote document: " + remoteDocument.Name, e);
                }
            }


            /// <summary>
            /// Crawl local files in a given directory (not recursive).
            /// </summary>
            private void CrawlLocalFiles(string localFolder, IFolder remoteFolder, IList remoteFiles)
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
            private void CrawlLocalFile(string filePath, IFolder remoteFolder, IList remoteFiles)
            {
                SleepWhileSuspended();

                try
                {
                    string fileName = Path.GetFileName(filePath);

                    if (Utils.WorthSyncing(Path.GetDirectoryName(filePath), fileName, repoinfo))
                    {
                        if (!remoteFiles.Contains(fileName))
                        {
                            // This local file is not on the CMIS server now, so
                            // check whether it used invalidFolderNameRegex to exist on server or not.
                            if (database.ContainsFile(filePath))
                            {
                                if (database.LocalFileHasChanged(filePath))
                                {
                                    // If file has changed locally, move to 'your_version' and warn about conflict
                                    if (BIDIRECTIONAL)
                                    {
                                        // Local file was updated, sync up.
                                        Logger.Info("Uploading locally edited remotely removed file from the repository: " + filePath);
                                        UploadFile(filePath, remoteFolder);
                                    }
                                    else
                                    {
                                        Logger.Info("Conflict with file: " + filePath + ", backing up locally modified version.");
                                        // Rename locally modified file.
                                        String newFilePath = Utils.ConflictPath(filePath);
                                        File.Move(filePath, newFilePath);

                                        // Delete file from database.
                                        database.RemoveFile(filePath);

                                        repo.OnConflictResolved();
                                    }
                                }
                                else
                                {
                                    // File has been deleted on server, so delete it locally.
                                    Logger.Info("Removing remotely deleted file: " + filePath);
                                    File.Delete(filePath);

                                    // Delete file from database.
                                    database.RemoveFile(filePath);
                                }
                            }
                            else
                            {
                                if (BIDIRECTIONAL)
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
                                if (BIDIRECTIONAL)
                                {
                                    // Upload new version of file content.
                                    Logger.Info("Uploading file update on repository: " + filePath);
                                    UpdateFile(filePath, remoteFolder);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not crawl sync local file: " + filePath, e);
                }
            }


            /// <summary>
            /// Crawl local folders in a given directory (not recursive).
            /// </summary>
            private void CrawlLocalFolders(string localFolder, IFolder remoteFolder, IList remoteFolders)
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
            private void CrawlLocalFolder(string localSubFolder, IFolder remoteFolder, IList remoteFolders)
            {
                SleepWhileSuspended();
                try
                {
                    string folderName = Path.GetFileName(localSubFolder);
                    if (Utils.WorthSyncing(Path.GetDirectoryName(localSubFolder), folderName, repoinfo))
                    {
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
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not crawl sync local folder: " + localSubFolder, e);
                }
            }
        }
    }
}
