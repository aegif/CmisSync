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
                    (int)features.MaxNumberOfContentChanges : 100;

                IChangeEvents changes;

                // Get last change token that had been saved on client side.
                string lastTokenOnClient = database.GetChangeLogToken();

                // Get last change log token on server side.
                string lastTokenOnServer = CmisUtils.GetChangeLogToken(session);

                if (lastTokenOnClient == lastTokenOnServer)
                {
                    Logger.Debug("No changes to sync, tokens on server and client are equal: \"" + lastTokenOnClient + "\"");
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
                do
                {
                    // Check which documents/folders have changed.
                    changes = session.GetContentChanges(lastTokenOnClient, IsPropertyChangesSupported, maxNumItems);

                    // Apply changes.
                    foreach (IChangeEvent change in changes.ChangeEventList)
                    {
                        // Check whether change is applicable.
                        // For instance, we dont care about changes to non-synced folders.
                        if (ChangeIsApplicable(change))
                        {
                            // Launch a CrawlSync (which means syncing everything indistinctively).
                            CrawlSyncAndUpdateChangeLogToken(remoteFolder, remotePath, localFolder);

                            // A single CrawlSync takes care of all pending changes, so no need to analyze the rest of the changes.
                            // It will also update the last client-side ChangeLog token, more accurately than we can do here.
                            return;
                        }
                    }

                    // No applicable changes, update ChangeLog token.
                    lastTokenOnClient = changes.LatestChangeLogToken; // But dont save to database as latest server token is actually a later token.
                }
                // Repeat if there were two many changes to fit in a single response.
                // Only reached if none of the changes in this iteration were non-applicable.
                while (changes.HasMoreItems ?? false);

                database.SetChangeLogToken(lastTokenOnServer);
            }


            /// <summary>
            /// Check whether a change is relevant for the current synchronized folder.
            /// </summary>
            private bool ChangeIsApplicable(IChangeEvent change)
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
