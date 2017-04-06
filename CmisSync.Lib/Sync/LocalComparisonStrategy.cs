using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using DotCMIS.Client;
using System.IO;
using CmisSync.Lib.Database;
using CmisSync.Lib.Cmis;
using DotCMIS.Exceptions;

namespace CmisSync.Lib.Sync
{
    public partial class CmisRepo : RepoBase
    {
        /// <summary>
        /// Synchronization by comparizon with local database.
        /// </summary>
        public partial class SynchronizedFolder
        {
            /// <summary>
            /// Detect what has changed using the local database, and apply these
            /// modifications to the remote server.
            /// </summary>
            /// <param name="rootFolder">Full path of the local synchronized folder, for instance "/User Homes/nicolas.raoul/demos"</param>
            public bool ApplyLocalChanges(string rootFolder)
            {
                try
                {
                    var deletedFolders = new List<string>();
                    var deletedFiles = new List<string>();
                    var modifiedFiles = new List<string>();
                    var addedFolders = new List<string>();
                    var addedFiles = new List<string>();

                    // Check for added folders and files.
                    FindNewLocalObjects(rootFolder, ref addedFolders, ref addedFiles);

                    // Check for deleted and modified folders and files.
                    FindModifiedOrDeletedLocalObjects(rootFolder, ref deletedFolders, ref deletedFiles, ref modifiedFiles);

                    // TODO: Try to make sense of related changes, for instance renamed folders.
                    // TODO: Check local metadata modification cache.

                    int numberOfChanges = deletedFolders.Count + deletedFiles.Count + modifiedFiles.Count + addedFolders.Count + addedFiles.Count;
                    Logger.Debug(numberOfChanges + " local changes to apply.");

                    if (numberOfChanges == 0)
                    {
                        return true; // Success: Did nothing.
                    }

                    // Apply changes to the server.
                    activityListener.ActivityStarted();
                    bool success = ApplyDeletedFolders(ref deletedFolders);
                    success &= ApplyDeletedFiles(ref deletedFiles);
                    success &= ApplyModifiedFiles(ref modifiedFiles);
                    success &= ApplyAddedFolders(ref addedFolders);
                    success &= ApplyAddedFiles(ref addedFiles);

                    Logger.Debug("Finished applying local changes.");
                    return success;
                }
                finally
                {
                    activityListener.ActivityStopped();
                }
            }


            /// <summary>
            /// Check for added folders and files.
            /// </summary>
            public void FindNewLocalObjects(string folder, ref List<string> addedFolders, ref List<string> addedFiles)
            {
                // Check files in this folder.
                string[] files;
                try
                {
                    files = Directory.GetFiles(folder);
                }
                catch (Exception e)
                {
                    Logger.Warn("Could not get the files list from folder: " + folder, e);
                    return;
                }

                foreach (string file in files)
                {
                    // Check whether this file is present in database.
                    string filePath = Path.Combine(folder, file);
                    if ( ! database.ContainsLocalFile(filePath))
                    {
                        addedFiles.Add(filePath);
                    }
                }

                // Check folders and recurse.
                string[] subFolders;
                try
                {
                    subFolders = Directory.GetDirectories(folder);
                }
                catch (Exception e)
                {
                    Logger.Warn("Could not get the folders list from folder: " + folder, e);
                    return;
                }

                foreach (string subFolder in subFolders)
                {
                    // Check whether this sub-folder is present in database.
                    string folderPath = Path.Combine(folder, subFolder);
                    if (database.ContainsLocalPath(folderPath))
                    {
                        // Recurse.
                        FindNewLocalObjects(folderPath, ref addedFolders, ref addedFiles);
                    }
                    else
                    {
                        // New folder, add to list and don't recurse.
                        addedFolders.Add(folderPath);
                    }
                }
            }


            /// <summary>
            /// Check for deleted and modified folders and files.
            /// </summary>
            public void FindModifiedOrDeletedLocalObjects(String rootFolder, ref List<string> deletedFolders,
                ref List<string> deletedFiles, ref List<string> modifiedFiles)
            {
                // Crawl through all entries in the database, and record the ones that have changed on the filesystem.
                // Check for deleted folders.
                var folders = database.GetLocalFolders();
                foreach (string folder in folders)
                {
                    if (!Directory.Exists(Utils.PathCombine(rootFolder, folder)))
                    {
                        deletedFolders.Add(folder);
                    }
                }
                var files = database.GetChecksummedFiles();
                foreach (ChecksummedFile file in files)
                {
                    // Check for deleted files.
                    if (File.Exists(Path.Combine(rootFolder, file.RelativePath)))
                    {
                        // Check for modified files.
                        if (file.HasChanged(rootFolder))
                        {
                            modifiedFiles.Add(file.RelativePath);
                        }
                    }
                    else
                    {
                        deletedFiles.Add(file.RelativePath);
                    }
                }

                // Ignore deleted files and folders that are sub-items of a deleted folder.
                // Folder removal is done recursively so removing sub-items would be redundant.

                foreach (string deletedFolder in new List<string>(deletedFolders)) // Copy the list to avoid modifying it while iterating.
                {
                    // Ignore deleted files contained in the deleted folder.
                    deletedFiles.RemoveAll(deletedFile => deletedFile.StartsWith(deletedFolder));

                    // Ignore deleted folders contained in the deleted folder.
                    deletedFolders.RemoveAll(otherDeletedFolder => Utils.FirstFolderContainsSecond(deletedFolder, otherDeletedFolder));
                }
            }


            /// <summary>
            /// Apply: Deleted folders.
            /// </summary>
            public bool ApplyDeletedFolders(ref List<string> deletedFolders)
            {
                bool success = true;
                foreach (string deletedFolder in deletedFolders)
                {
                    SyncItem deletedItem = SyncItemFactory.CreateFromLocalPath(deletedFolder, true, repoInfo, database);
                    try
                    {
                        var deletedIFolder = session.GetObjectByPath(deletedItem.RemotePath) as IFolder;

                        // Check whether the remote folder has changes we haven't gotten yet (conflict)
                        var changed = HasFolderChanged(deletedIFolder);

                        // Delete the remote folder if unchanged, otherwise let full sync handle the conflict.
                        var remotePath = deletedItem.RemotePath;
                        var localPath = deletedItem.LocalPath;
                        var remoteFolders = new List<string>();

                        if (changed)
                        {

                            // TODO: Internationalization
                            string message = String.Format("Restoring folder {0} because its sub-items have been modified on the server. You can delete it again.", localPath);
                            Utils.NotifyUser(message);

                            // TODO: Handle folder conflict
                            // Delete local database entry.
                            database.RemoveFolder(SyncItemFactory.CreateFromLocalPath(deletedFolder, true, repoInfo, database));

                            DownloadDirectory(deletedIFolder, remotePath, localPath);

                            return false;
                        }
                        else
                        {
                            DeleteRemoteFolder(deletedIFolder, deletedItem, Utils.UpperFolderLocal(deletedItem.LocalPath));
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ArgumentNullException || e is CmisObjectNotFoundException)
                        {
                            // Typical error when the document does not exist anymore on the server
                            // TODO Make DotCMIS generate a more precise exception.

                            Logger.Error("The folder has probably been deleted on the server already: " + deletedFolder, e);

                            // Delete local database entry.
                            database.RemoveFolder(SyncItemFactory.CreateFromLocalPath(deletedFolder, true, repoInfo, database));

                            // Note: This is not a failure per-se, so we don't need to modify the "success" variable.
                        }
                        else
                        {
                            Logger.Error("Error applying local folder deletion to the server: " + deletedFolder, e);
                            success = false;
                        }
                    }
                }
                return success;
            }

            private bool HasFolderChanged(IFolder deletedIFolder)
            {
                // TODO Does not work if newly-created.

                // ChangeLog 
                string lastTokenOnClient = database.GetChangeLogToken();
                string lastTokenOnServer = CmisUtils.GetChangeLogToken(session);

                if (lastTokenOnClient == lastTokenOnServer || lastTokenOnClient == null) return false;

                // TODO: Extract static code, because same code was writtern in SynchronizedFolder
                Config.Feature features = null;
                if (ConfigManager.CurrentConfig.GetFolder(repoInfo.Name) != null)
                    features = ConfigManager.CurrentConfig.GetFolder(repoInfo.Name).SupportedFeatures;
                int maxNumItems = (features != null && features.MaxNumberOfContentChanges != null) ?  // TODO if there are more items, either loop or force CrawlSync
                    (int)features.MaxNumberOfContentChanges : 500;

                var changes = session.GetContentChanges(lastTokenOnClient, IsPropertyChangesSupported, maxNumItems);

                return CheckInsideChange(deletedIFolder, changes);
            }

            private bool CheckInsideChange(IFolder targetIFolder, IChangeEvents changeTokens)
            {
                var children = targetIFolder.GetChildren();
                var leafFolders = children.OfType<IFolder>();
                var leafFiles = children.OfType<IDocument>();

                var changed =
                    leafFolders.Any(childFolder => changeTokens.ChangeEventList.Any(change => childFolder.Id == change.ObjectId))
                    || leafFiles.Any(childFile => changeTokens.ChangeEventList.Any(change => childFile.VersionSeriesId == change.Properties["versionSeriesId"][0] as string))
                    ;
                if (changed) return true;

                foreach (var leafFolder in leafFolders)
                {
                    changed = CheckInsideChange(leafFolder, changeTokens);
                    if (changed) return true;
                }
                return false;
            }




            /// <summary>
            /// Apply: Deleted files.
            /// </summary>
            public bool ApplyDeletedFiles(ref List<string> deletedFiles)
            {
                bool success = true;
                foreach (string deletedFile in deletedFiles)
                {
                    SyncItem deletedItem = SyncItemFactory.CreateFromLocalPath(deletedFile, false, repoInfo, database);
                    try
                    {
                        IDocument deletedDocument = (IDocument)session.GetObjectByPath(deletedItem.RemotePath);

                        try
                        {
                            CrawlRemoteDocument(deletedDocument, deletedItem.RemotePath, deletedItem.LocalPath, null);
                        }
                        catch (CmisPermissionDeniedException e)
                        {
                            Logger.Info("This user cannot delete file : " + deletedFile, e);
                            DownloadFile(deletedDocument, deletedItem.RemotePath, deletedItem.LocalPath);
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ArgumentNullException || e is CmisObjectNotFoundException)
                        {
                            // Typical error when the document does not exist anymore on the server
                            // TODO Make DotCMIS generate a more precise exception.
                            Logger.Info("The document has probably been deleted on the server already: " + deletedFile, e);

                            // Delete local database entry.
                            database.RemoveFile(deletedItem);

                            // Note: This is not a failure per-se, so we don't need to modify the "success" variable.
                        }
                        else
                        {

                            // Could be a network error.
                            Logger.Error("Error applying local file deletion to the server: " + deletedFile, e);
                            success = false;
                        }
                    }
                }
                return success;
            }


            /// <summary>
            /// Apply: Modified files.
            /// </summary>
            public bool ApplyModifiedFiles(ref List<string> modifiedFiles)
            {
                bool success = true;
                foreach (string modifiedFile in modifiedFiles)
                {
                    SyncItem modifiedItem = SyncItemFactory.CreateFromLocalPath(modifiedFile, true, repoInfo, database);
                    try
                    {
                        IDocument modifiedDocument = (IDocument)session.GetObjectByPath(modifiedItem.RemotePath);
                        
                        CrawlRemoteDocument(modifiedDocument, modifiedItem.RemotePath, modifiedItem.LocalPath, null);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error applying local file modification to the server: " + modifiedFile, e);
                        success = false;
                    }
                }
                return success;
            }


            /// <summary>
            /// Apply: Added folders.
            /// </summary>
            public bool ApplyAddedFolders(ref List<string> addedFolders)
            {
                bool success = true;
                foreach (string addedFolder in addedFolders)
                {
                    string destinationFolderPath = Path.GetDirectoryName(addedFolder);
                    SyncItem destinationFolderItem = SyncItemFactory.CreateFromLocalPath(destinationFolderPath, true, repoInfo, database);
                    SyncItem addedFolderItem = SyncItemFactory.CreateFromLocalPath(addedFolder, true, repoInfo, database);
                    try
                    {
                        IFolder destinationFolder = (IFolder)session.GetObjectByPath(destinationFolderItem.RemotePath);

                        IList<string> remoteFolders = new List<string>();

                        if (CmisUtils.FolderExists(session, addedFolderItem.RemotePath))
                        {
                            remoteFolders.Add(addedFolderItem.RemoteLeafname);
                        }

                        // TODO more efficient: first create said folder, then call CrawlSync in it.
                        CrawlSync(destinationFolder, destinationFolderItem.RemotePath, destinationFolderItem.LocalPath);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error applying local folder addition to the server: " + addedFolder, e);
                        success = false;
                    }
                }
                return success;
            }


            /// <summary>
            /// Apply: Added files.
            /// </summary>
            public bool ApplyAddedFiles(ref List<string> addedFiles)
            {
                bool success = true;
                foreach (string addedFile in addedFiles)
                {
                    string destinationFolderPath = Path.GetDirectoryName(addedFile);
                    SyncItem folderItem = SyncItemFactory.CreateFromLocalPath(destinationFolderPath, true, repoInfo, database);
                    SyncItem fileItem = SyncItemFactory.CreateFromLocalPath(addedFile, false, repoInfo, database);
                    try
                    {
                        IFolder destinationFolder = (IFolder)session.GetObjectByPath(folderItem.RemotePath);

                        // Fill documents list, needed by the crawl method.
                        IList<string> remoteFiles = new List<string>();

                        if (CmisUtils.DocumentExists(session, fileItem.RemotePath))
                        {
                            remoteFiles.Add(fileItem.RemoteLeafname);
                        }

                        // Crawl this particular file.
                        CheckLocalFile(fileItem.LocalPath, destinationFolder, remoteFiles);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error applying local file addition to the server: " + addedFile, e);
                        success = false;
                    }
                }
                return success;
            }
        }
    }
}