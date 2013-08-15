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
                while (repo.Status == SyncStatus.Suspend)
                {
                    Logger.Info(String.Format("Sync of {0} is suspend, next retry in {1}ms", repoinfo.Name, repoinfo.PollInterval));
                    System.Threading.Thread.Sleep((int)repoinfo.PollInterval);
                }

                // Lists of files/folders, to delete those that have been removed on the server.
                IList remoteFiles = new ArrayList();
                IList remoteSubfolders = new ArrayList();

                // Crawl remote children.
                // Logger.LogInfo("Sync", String.Format("Crawl remote folder {0}", this.remoteFolderPath));
                bool success = CrawlRemote(remoteFolder, localFolder, remoteFiles, remoteSubfolders);

                // Crawl local files.
                // Logger.LogInfo("Sync", String.Format("Crawl local files in the local folder {0}", localFolder));
                success = CrawlLocalFiles(localFolder, remoteFolder, remoteFiles) && success;

                // Crawl local folders.
                // Logger.LogInfo("Sync", String.Format("Crawl local folder {0}", localFolder));
                success = CrawlLocalFolders(localFolder, remoteFolder, remoteSubfolders) && success;

                return success;
            }


            /// <summary>
            /// Crawl remote content, syncing down if needed.
            /// Meanwhile, cache remoteFiles and remoteFolders, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
            /// </summary>
            private bool CrawlRemote(IFolder remoteFolder, string localFolder, IList remoteFiles, IList remoteFolders)
            {
                bool success = true;

                foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
                {
                    while (repo.Status == SyncStatus.Suspend)
                    {
                        Logger.Info("Sync of " + repoinfo.Name + " is suspended, will retry in " + repoinfo.PollInterval + "ms");
                        System.Threading.Thread.Sleep((int)repoinfo.PollInterval); // TODO Should not sleep, but skip instead.
                    }

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

                        success = SyncDownloadFile(remoteDocument, localFolder, remoteFiles) && success;
                    }
                    #endregion
                }

                return success;
            }


            /// <summary>
            /// Crawl local files in a given directory (not recursive).
            /// </summary>
            private bool CrawlLocalFiles(string localFolder, IFolder remoteFolder, IList remoteFiles)
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
                    while (repo.Status == SyncStatus.Suspend)
                    {
                        Logger.Info(String.Format("Sync of {0} is suspend, next retry in {1}ms", repoinfo.Name, repoinfo.PollInterval));
                        System.Threading.Thread.Sleep((int)repoinfo.PollInterval);
                    }

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
                                        success = UpdateFile(filePath, remoteFolder) && success;
                                    }
                                }
                            }
                        }
                    }
                }

                return success;
            }


            /// <summary>
            /// Crawl local folders in a given directory (not recursive).
            /// </summary>
            private bool CrawlLocalFolders(string localFolder, IFolder remoteFolder, IList remoteFolders)
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
                    while (repo.Status == SyncStatus.Suspend)
                    {
                        Logger.Info(String.Format("Sync of {0} is suspend, next retry in {1}ms", repoinfo.Name, repoinfo.PollInterval));
                        System.Threading.Thread.Sleep((int)repoinfo.PollInterval);
                    }
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
