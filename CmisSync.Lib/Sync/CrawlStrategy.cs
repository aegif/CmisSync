using System;
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
            private bool CrawlSync(IFolder remoteFolder, string localFolder)
            {
                sleepWhileSuspended();

                if (IsGetDescendantsSupported)
                {
                    return CrawlDescendants(remoteFolder, remoteFolder.GetDescendants(-1), localFolder);
                }

                // Lists of files/folders, to delete those that have been removed on the server.
                IList<string> remoteFiles = new List<string>();
                IList<string> remoteSubfolders = new List<string>();

                // Crawl remote children.
                // Logger.LogInfo("Sync", String.Format("Crawl remote folder {0}", this.remoteFolderPath));
                bool success = CrawlRemote(remoteFolder, localFolder, remoteFiles, remoteSubfolders);
                sleepWhileSuspended();
                // Crawl local files.
                // Logger.LogInfo("Sync", String.Format("Crawl local files in the local folder {0}", localFolder));
                success = success && CrawlLocalFiles(localFolder, remoteFolder, remoteFiles);
                sleepWhileSuspended();
                // Crawl local folders.
                // Logger.LogInfo("Sync", String.Format("Crawl local folder {0}", localFolder));
                success = CrawlLocalFolders(localFolder, remoteFolder, remoteSubfolders) && success;

                return success;
            }

            /// <summary>
            /// Takes the loaded and given descendants as children of the given remoteFolder and checks agains the localFolder
            /// </summary>
            /// <param name="remoteFolder">Folder which contains to given children</param>
            /// <param name="children">All children of the given remote folder</param>
            /// <param name="localFolder">The local folder, with which the remoteFolder should be synchronized</param>
            /// <returns></returns>
            private bool CrawlDescendants(IFolder remoteFolder, IList<ITree<IFileableCmisObject>> children, string localFolder)
            {
                bool success = true;

                // Lists of files/folders, to delete those that have been removed on the server.
                IList<string> remoteFiles = new List<string>();
                IList<string> remoteSubfolders = new List<string>();
                if (children != null)
                foreach (ITree<IFileableCmisObject> node in children)
                {
                    #region Cmis Folder
                    if (node.Item is Folder)
                    {
                        // It is a CMIS folder.
                        IFolder remoteSubFolder = (IFolder)node.Item;
                        remoteSubfolders.Add(remoteSubFolder.Name);
                        if (Utils.WorthSyncing(remoteSubFolder.Name) && !repoinfo.isPathIgnored(remoteSubFolder.Path))
                        {
                            string localSubFolder = Path.Combine(localFolder, remoteSubFolder.Name);

                            //Check whether local folder exists.
                            if (Directory.Exists(localSubFolder))
                            {
                                success = CrawlDescendants(remoteSubFolder, node.Children, localSubFolder) && success;
                            }
                            else
                            {
                                success = SyncDownloadFolder(remoteSubFolder, localFolder) && success;
                                if (Directory.Exists(localSubFolder))
                                {
                                    success = RecursiveFolderCopy(remoteSubFolder, localSubFolder) && success;
                                }
                            }
                        }
                    }
                    #endregion

                    #region Cmis Document
                    else if (node.Item is Document)
                    {
                        // It is a CMIS document.
                        IDocument remoteDocument = (IDocument)node.Item;
                        success = SyncDownloadFile(remoteDocument, localFolder, remoteFiles) && success;
                    }
                    #endregion
                }
                success = CrawlLocalFiles(localFolder, remoteFolder, remoteFiles) && success;
                success = CrawlLocalFolders(localFolder, remoteFolder, remoteSubfolders) && success;
                return success;
            }


            /// <summary>
            /// Crawl remote content, syncing down if needed.
            /// Meanwhile, cache remoteFiles and remoteFolders, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
            /// </summary>
            private bool CrawlRemote(IFolder remoteFolder, string localFolder, IList<string> remoteFiles, IList<string> remoteFolders)
            {
                bool success = true;

                foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
                {
                    sleepWhileSuspended();

                    #region Cmis Folder
                    if (cmisObject is DotCMIS.Client.Impl.Folder)
                    {
                        // It is a CMIS folder.
                        IFolder remoteSubFolder = (IFolder)cmisObject;
                        if (Utils.WorthSyncing(remoteSubFolder.Name) && !repoinfo.isPathIgnored(remoteSubFolder.Path))
                        {
                            if (null != remoteFolders)
                            {
                                remoteFolders.Add(remoteSubFolder.Name);
                            }

                            string localSubFolder = Path.Combine(localFolder, remoteSubFolder.Name);
                            // Check whether local folder exists.
                            if (Directory.Exists(localSubFolder))
                            {
                                // Recurse into folder.
                                success = CrawlSync(remoteSubFolder, localSubFolder) && success;
                            }
                            else
                            {
                                success = SyncDownloadFolder(remoteSubFolder, localFolder) && success;
                                if (Directory.Exists(localSubFolder))
                                {
                                    success = RecursiveFolderCopy(remoteSubFolder, localSubFolder) && success;
                                }
                            }
                        }
                    }
                    #endregion

                    #region Cmis Document
                    else
                    {
                        // It is a CMIS document.
                        IDocument remoteDocument = (IDocument)cmisObject;
                        success = success && SyncDownloadFile(remoteDocument, localFolder, remoteFiles);
                    }
                    #endregion
                }

                return success;
            }

            private bool handleDiffLocalAndRemoteDocument(string localFolder, IDocument remoteDocument)
            {
                bool success = true;
                string remoteDocumentFileName = remoteDocument.ContentStreamFileName;
                string filePath = localFolder + Path.DirectorySeparatorChar.ToString() + remoteDocumentFileName;

                if (File.Exists(filePath))
                {
                    // Check modification date stored in database and download if remote modification date if different.
                    DateTime? serverSideModificationDate = ((DateTime)remoteDocument.LastModificationDate).ToUniversalTime();
                    DateTime? lastDatabaseUpdate = database.GetServerSideModificationDate(filePath);
                    DateTime? lastLocalUpdate = File.GetLastWriteTime(filePath);
                    if (lastDatabaseUpdate == null)
                    {
                        Logger.Info("Downloading file absent from database: " + filePath);
                        success = success && DownloadFile(remoteDocument, localFolder);
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
                                String ext = Path.GetExtension(filePath);
                                String filename = Path.GetFileNameWithoutExtension(filePath);
                                String path = Path.GetDirectoryName(filePath);

                                String NewFileName = Utils.SuffixIfExists(filename + "_" + repoinfo.User + "-version");
                                String newFilePath = Path.Combine(path, NewFileName);
                                File.Move(filePath, newFilePath);

                                // Download server version
                                success = success && DownloadFile(remoteDocument, localFolder);
                                repo.OnConflictResolved();

                                // TODO move to OS-dependant layer
                                //System.Windows.Forms.MessageBox.Show("Someone modified a file at the same time as you: " + filePath
                                //    + "\n\nYour version has been saved with a '_your-version' suffix, please merge your important changes from it and then delete it.");
                                // TODO show CMIS property lastModifiedBy
                            }
                            else
                            {
                                Logger.Info("Downloading modified file: " + remoteDocumentFileName);
                                success = success && DownloadFile(remoteDocument, localFolder);
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
                        success = success && DownloadFile(remoteDocument, localFolder);
                    }
                }
                return success;
            }

            private bool handleDiffOfLocalAndRemoteFolder(IFolder remoteSubFolder, string localSubFolder)
            {
                bool success = true;

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

                    // If there was previously a file with this name, delete it.
                    // TODO warn if local changes in the file.
                    if (File.Exists(localSubFolder))
                    {
                        Logger.Warn("Local file \"" + localSubFolder + "\" has been renamed to \"" + localSubFolder + ".conflict\"");
                        File.Move(localSubFolder, localSubFolder + ".conflict");
                    }

                    // Skip if invalid folder name. See https://github.com/nicolas-raoul/CmisSync/issues/196
                    if (Utils.IsInvalidFileName(remoteSubFolder.Name))
                    {
                        Logger.Info("Skipping download of folder with illegal name: " + remoteSubFolder.Name);
                    }
                    else if (repoinfo.isPathIgnored(remoteSubFolder.Path))
                    {
                        Logger.Info("Skipping dowload of ignored folder: " + remoteSubFolder.Name);
                    }
                    else
                    {
                        // Create local folder.remoteDocument.Name
                        Directory.CreateDirectory(localSubFolder);

                        // Create database entry for this folder.
                        // TODO - Yannick - Add metadata
                        database.AddFolder(localSubFolder, remoteSubFolder.Id, remoteSubFolder.LastModificationDate);

                        // Recursive copy of the whole folder.
                        success = success && RecursiveFolderCopy(remoteSubFolder, localSubFolder);
                    }
                }
                return success;
            }


            /// <summary>
            /// Crawl local files in a given directory (not recursive).
            /// </summary>
            private bool CrawlLocalFiles(string localFolder, IFolder remoteFolder, IList<string> remoteFiles)
            {
                string[] files;
                try
                {
                    files = Directory.GetFiles(localFolder);
                }
                catch (Exception e)
                {
                    Logger.Warn(String.Format("Exception while get the file list from folder {0}: {1}", localFolder, Utils.ToLogString(e)));
                    return false;
                }

                bool success = true;
                foreach (string filePath in files)
                {
                    sleepWhileSuspended();

                    string fileName = Path.GetFileName(filePath);

                    if (Utils.WorthSyncing(fileName))
                    {
                        if (!remoteFiles.Contains(fileName))
                        {
                            // This local file is not on the CMIS server now, so
                            // check whether it used invalidFolderNameRegex to exist on server or not.
                            if (database.ContainsFile(filePath))
                            {
                                // If file has changed locally, move to 'your_version' and warn about conflict
                                // TODO

                                // File has been deleted on server, so delete it locally.
                                Logger.Info("Removing remotely deleted file: " + filePath);
                                File.Delete(filePath);

                                // Delete file from database.
                                database.RemoveFile(filePath);
                            }
                            else
                            {
                                if (BIDIRECTIONAL)
                                {
                                    // New file, sync up.
                                    Logger.Info("Uploading file absent on repository: " + filePath);
                                    if (Utils.WorthSyncing(filePath))
                                    {
                                        success = UploadFile(filePath, remoteFolder) && success;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // The file exists both on server and locally.
                            if (!syncFull)
                            {
                                if (database.LocalFileHasChanged(filePath))
                                {
                                    if (BIDIRECTIONAL)
                                    {
                                        // Upload new version of file content.
                                        Logger.Info("Uploading file update on repository: " + filePath);
                                        success = success && UpdateFile(filePath, remoteFolder);
                                    }
                                }
                            }
                        }
                    }
                }

                return success;
            }

            private void sleepWhileSuspended()
            {
                while (repo.Status == SyncStatus.Suspend)
                {
                    Logger.Info(String.Format("Sync of {0} is suspend, next retry in {1}ms", repoinfo.Name, repoinfo.PollInterval));
                    System.Threading.Thread.Sleep((int)repoinfo.PollInterval);
                }
            }


            /// <summary>
            /// Crawl local folders in a given directory (not recursive).
            /// </summary>
            private bool CrawlLocalFolders(string localFolder, IFolder remoteFolder, IList<string> remoteFolders)
            {
                string[] folders;
                try
                {
                    folders = Directory.GetDirectories(localFolder);
                }
                catch (Exception e)
                {
                    Logger.Warn(String.Format("Exception while get the folder list from folder {0}: {1}", localFolder, Utils.ToLogString(e)));
                    return false;
                }

                bool success = true;
                foreach (string localSubFolder in folders)
                {
                    sleepWhileSuspended();
                    string path = localSubFolder.Substring(repoinfo.TargetDirectory.Length).Replace("\\", "/");
                    string folderName = Path.GetFileName(localSubFolder);
                    if (Utils.WorthSyncing(folderName) && !repoinfo.isPathIgnored(path))
                    {
                        if (!remoteFolders.Contains(folderName))
                        {
                            // This local folder is not on the CMIS server now, so
                            // check whether it used to exist on server or not.
                            if (database.ContainsFolder(localSubFolder))
                            {
                                success = RemoveFolderLocally(localSubFolder) && success;
                            }
                            else
                            {
                                if (BIDIRECTIONAL)
                                {
                                    // New local folder, upload recursively.
                                    success = UploadFolderRecursively(remoteFolder, localSubFolder) && success;
                                }
                            }
                        }
                    }
                }
                return success;
            }
        }
    }
}
