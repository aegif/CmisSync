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
using CmisSync.Lib;
using DotCMIS.Data;
using CmisSync.Lib.Cmis;

namespace CmisSync.Lib.Sync
{
    public partial class CmisRepo : RepoBase
    {
        /// <summary>
        /// Synchronization with a particular CMIS folder.
        /// </summary>
        public partial class SynchronizedFolder
        {
            private int changeLogIterationCounter = 0;

            /// <summary>
            /// Synchronize using the ChangeLog feature of CMIS to trigger CrawlStrategy.
            /// </summary>
            private void ChangeLogThenCrawlSync(IFolder remoteFolder, string remotePath, string localFolder)
            {
                // Once in a while, run a crawl sync, to make up for any server-side ChangeLog bug.
                // The frequency of this is calculated based on the poll interval, so that:
                // Interval=5 seconds -> every 6 hours -> about every 2160 iterations
                // Interval=1 hours -> every 3 days -> about every 72 iterations
                // Thus a good formula is: nb of iterations = 1 + 263907 / (pollInterval + 117)
                double pollInterval = ConfigManager.CurrentConfig.GetFolder(repoInfo.Name).PollInterval;
                if (changeLogIterationCounter > 263907 / (pollInterval/1000 + 117))
                {
                    Logger.Debug("It has been a while since the last crawl sync, so launching a crawl sync now.");
                    CrawlSyncAndUpdateChangeLogToken(remoteFolder, remotePath, localFolder);
                    changeLogIterationCounter = 0;
                    return;
                }
                else
                {
                    changeLogIterationCounter++;
                }

                // Calculate queryable number of changes.
                Config.Feature features = null;
                if (ConfigManager.CurrentConfig.GetFolder(repoInfo.Name) != null)
                    features = ConfigManager.CurrentConfig.GetFolder(repoInfo.Name).SupportedFeatures;
                int maxNumItems = (features != null && features.MaxNumberOfContentChanges != null) ?  // TODO if there are more items, either loop or force CrawlSync
                    (int)features.MaxNumberOfContentChanges : 50;



                // Get last change token that had been saved on client side.
                string lastTokenOnClient = database.GetChangeLogToken();

                // Get last change log token on server side.
                string lastTokenOnServer = CmisUtils.GetChangeLogToken(session);

                if (lastTokenOnClient == lastTokenOnServer)
                {
                    Logger.DebugFormat("No changes to sync, tokens on server and client are equal: \"{0}\"", lastTokenOnClient);
                    return;
                }

                if (lastTokenOnClient == null)
                {
                    // Token is null, which means no sync has ever happened yet, so just sync everything from remote.
                    CrawlRemote(remoteFolder, remotePath, repoInfo.TargetDirectory, new List<string>(), new List<string>());
                    
                    Logger.Info("Synced from remote, updating ChangeLog token: " + lastTokenOnServer);
                    database.SetChangeLogToken(lastTokenOnServer);
                }

                // ChangeLog tokens are different, so checking changes is needed.
                var currentChangeToken = lastTokenOnClient;
                IChangeEvents changes;
                do
                {
                    // Check which documents/folders have changed.
                    changes = session.GetContentChanges(currentChangeToken, IsPropertyChangesSupported, maxNumItems);

                    // First event was already processed previous.
                    var changeEvents = changes.ChangeEventList.Where(p => p != changes.ChangeEventList.FirstOrDefault()).ToList();

                    CrawlChangeLogSyncAndUpdateChangeLogToken(changeEvents, remoteFolder, remotePath, localFolder);

                    currentChangeToken = changes.LatestChangeLogToken; 

                    database.SetChangeLogToken(currentChangeToken);
                }
                // Repeat if there were two many changes to fit in a single response.
                // Only reached if none of the changes in this iteration were non-applicable.
                while (changes.HasMoreItems ?? false);

                database.SetChangeLogToken(lastTokenOnServer);
            }


            /// <summary>
            /// Check whether a change is relevant for the current synchronized folder.
            /// </summary>
            /*private bool ChangeIsApplicable(IChangeEvent change)
            {
                ICmisObject cmisObject = null;
                IFolder remoteFolder = null;
                IDocument remoteDocument = null;
                IList<string> remotePaths = null;
                var changeIdForDebug = change.Properties.ContainsKey("cmis:name") ?
                    change.Properties["cmis:name"][0] : change.Properties["cmis:objectId"][0]; // TODO is it different from change.ObjectId ?

                // Get the remote changed object.
                try
                {
                    cmisObject = session.GetObject(change.ObjectId);
                }
                catch (CmisObjectNotFoundException)
                {
                    Logger.Info("Changed object has already been deleted on the server. Syncing just in case: " + changeIdForDebug);
                    // Unfortunately, in this case we can not know whether the object was relevant or not.
                    return true;
                }
                catch (CmisRuntimeException e)
                {
                    if (e.Message.Equals("Unauthorized"))
                    {
                        Logger.Info("We can not read the object id, so it is not an object we can sync anyway: " + changeIdForDebug);
                        return false;
                    }
                    else
                    {
                        Logger.Info("A CMIS exception occured when querying the change. Syncing just in case: " + changeIdForDebug + " :", e);
                        return true;
                    }
                
                }
                catch (CmisPermissionDeniedException e)
                {
                    Logger.Info("Permission denied object  : " + changeIdForDebug + " :", e);
                    return false;
                }
                catch (Exception e)
                {
                    Logger.Warn("An exception occurred, syncing just in case: " + changeIdForDebug + " :", e);
                    return true;
                }

                // Check whether change is about a document or folder.
                remoteDocument = cmisObject as IDocument;
                remoteFolder = cmisObject as IFolder;
                if (remoteDocument == null && remoteFolder == null)
                {
                    Logger.Info("Ignore change as it is not about a document nor folder: " + changeIdForDebug);
                    return false;
                }

                // Check whether it is a document worth syncing.
                if (remoteDocument != null)
                {
                    if (!Utils.IsFileWorthSyncing(repoInfo.CmisProfile.localFilename(remoteDocument), repoInfo))
                    {
                        Logger.Info("Ignore change as it is about a document unworth syncing: " + changeIdForDebug);
                        return false;
                    }
                    if (remoteDocument.Paths.Count == 0)
                    {
                        Logger.Info("Ignore the unfiled object: " + changeIdForDebug);
                        return false;
                    }

                    // We will check the remote document's path(s) at the end of this method.
                    remotePaths = remoteDocument.Paths;
                }

                // Check whether it is a folder worth syncing.
                if (remoteFolder != null)
                {
                    remotePaths = new List<string>();
                    remotePaths.Add(remoteFolder.Path);
                }

                // Check the object's path(s)
                foreach (string remotePath in remotePaths)
                {
                    if (PathIsApplicable(remotePath))
                    {
                        Logger.Debug("Change is applicable. Sync:" + changeIdForDebug);
                        return true;
                    }
                }

                // No path was relevant, so ignore the change.
                return false;
            }*/
            
            
            /// <summary>
            /// Apply CMIS ChangeLog changes.
            /// </summary>
            private void CrawlChangeLogSyncAndUpdateChangeLogToken(IList<IChangeEvent> changeLogs, IFolder remoteFolder, string remotePath, string localFolder)
            {
                SleepWhileSuspended();

                var sw = new System.Diagnostics.Stopwatch();
                activityListener.ActivityStarted();
                try
                {
                    sw.Start();
                    Logger.InfoFormat("Change log sync start : {0} logs", changeLogs.Count());

                    // TODO: Compact changelogs

                    foreach (var change in changeLogs)
                    {
                        var id = change.ObjectId;
                        try
                        {
                            Logger.InfoFormat("Change log : Type={0}, Name={1}, Id={2}", change.ChangeType, change.Properties["cmis:name"].First(), id);
                        }
                        catch
                        {
                            Logger.InfoFormat("Change log : Type={0}, Id={1} ", change.ChangeType, id);
                        }

                        try
                        {
                            var cmisObject = session.GetObject(id);
                            CrawlCmisObject(cmisObject);
                        }
                        catch (CmisObjectNotFoundException ex)
                        {

                            if (change.ChangeType == ChangeType.Deleted)
                            {

                                var local = database.GetSyncItem(id);
                                if (local != null)
                                {
                                    var destFolderPath = Path.GetDirectoryName(local.LocalPath);
                                    var destFolderItem = SyncItemFactory.CreateFromLocalPath(destFolderPath, true, repoInfo, database);

                                    try
                                    {
                                        var destCmisFolder = session.GetObjectByPath(destFolderItem.RemotePath) as IFolder;

                                        if (local.IsFolder)
                                        {
                                            CrawlSync(destCmisFolder, destFolderItem.RemotePath, destFolderItem.LocalPath);
                                        }
                                        else
                                        {
                                            CheckLocalFile(local.LocalPath, destCmisFolder, new List<string>());
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        if (e is ArgumentNullException || e is CmisObjectNotFoundException)
                                        {
                                            // GetObjectByPath failure
                                            Logger.InfoFormat("Remote parent object not found, ignore. {0}", destFolderItem.RemotePath);
                                        }
                                        else
                                        {
                                            throw;
                                        }
                                    }
                                }
                                else
                                {
                                    Logger.InfoFormat("Remote deleted object not in local database, ignore. {0}", id);
                                }
                            }
                            else
                            {
                                Logger.InfoFormat("Remote object not found but delete event, ignore. {0}", id);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug(ex);
                        }
                    }

                    sw.Stop();
                    Logger.InfoFormat("Change log sync end : {1} min / {0} logs", changeLogs.Count(), sw.Elapsed);
                }
                finally
                {
                    activityListener.ActivityStopped();
                }
            }


            /// <summary>
            /// Synchronize changes made to a particular CMIS object.
            /// </summary>
            private void CrawlCmisObject(ICmisObject cmisObject)
            {
                if (cmisObject is DotCMIS.Client.Impl.Folder)
                {
                    var remoteSubFolder = cmisObject as IFolder;


                    // Look for the local equivalent.
                    var localFolderItem = database.GetFolderSyncItemFromRemotePath(remoteSubFolder.Path);
                    while (true)
                    {
                        // If other local folders have the same id, they are obsolete and must be deteled.
                        var foldersToDelete = database.GetAllFoldersWithCmisId(cmisObject.Id).Where(p => p.RemotePath != remoteSubFolder.Path);
                        foreach (var folderToDelete in foldersToDelete)
                        {
                            RemoveFolderLocally(folderToDelete.LocalPath);
                        };

                        if (localFolderItem != null || remoteSubFolder.IsRootFolder) break;

                        // Go up one level before performing the same thing.
                        remoteSubFolder = remoteSubFolder.Parents[0]; //TODO: Fix Parents[0] for multi-parent repositories
                        localFolderItem = database.GetFolderSyncItemFromRemotePath(remoteSubFolder.Path);
                    };

                    CrawlSync(remoteSubFolder, remoteSubFolder.Path, localFolderItem.LocalPath);
                }
                else if (cmisObject is DotCMIS.Client.Impl.Document)
                {
                    var remoteDocument = cmisObject as IDocument;

                    // Apply the change on all paths via which it is applicable.
                    foreach (IFolder remoteIFolder in remoteDocument.Parents)
                    {
                        if (PathIsApplicable(remoteIFolder.Path))
                        {
                            Logger.Debug("Document change is applicable:" + remoteIFolder);

                            var localFolderItem = database.GetFolderSyncItemFromRemotePath(remoteIFolder.Path);
                            var localFolder = localFolderItem.LocalPath;

                            var remoteDocumentPath = CmisUtils.PathCombine(remoteIFolder.Path, repoInfo.CmisProfile.localFilename(remoteDocument));
                            var documentItem = SyncItemFactory.CreateFromRemoteDocument(remoteDocumentPath, remoteDocument, repoInfo, database);

                            CrawlRemoteDocument(remoteDocument, documentItem.RemotePath, localFolder, null);
                        }
                    }
                }
            }
            
            
            /// <summary>
            /// Check whether a path is relevant for the current synchronized folder.
            /// </summary>
            private bool PathIsApplicable(string remotePath)
            {
                // Ignore the change if not in a synchronized folder.
                if ( ! remotePath.StartsWith(this.remoteFolderPath))
                {
                    Logger.Info("Ignore change as it is not in the synchronized folder's path: " + remotePath);
                    return false;
                }

                // Ignore if configured to be ignored.
                if (this.repoInfo.isPathIgnored(remotePath))
                {
                    Logger.Info("Ignore change as it is in a path configured to be ignored: " + remotePath);
                    return false;
                }

                // In other case, the change is probably applicable.
                return true;
            }
        }
    }
}
