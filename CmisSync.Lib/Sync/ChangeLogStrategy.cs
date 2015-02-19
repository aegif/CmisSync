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

namespace CmisSync.Lib.Sync
{
    public partial class CmisRepo : RepoBase
    {
        /// <summary>
        /// Synchronization with a particular CMIS folder.
        /// </summary>
        public partial class SynchronizedFolder
        {

            /// <summary>
            /// Synchronize using the ChangeLog feature of CMIS.
            /// Not all CMIS servers support this feature, so sometimes CrawlStrategy is used instead.
            /// </summary>
            private void ChangeLogSync(IFolder remoteFolder)
            {
                // Get last change log token on server side.
                session.Binding.GetRepositoryService().GetRepositoryInfos(null);    //  refresh
                string lastTokenOnServer = session.Binding.GetRepositoryService().GetRepositoryInfo(session.RepositoryInfo.Id, null).LatestChangeLogToken;

                // Get last change token that had been saved on client side.
                // TODO catch exception invalidArgument which means that changelog has been truncated and this token is not found anymore.
                string lastTokenOnClient = database.GetChangeLogToken();

                if (lastTokenOnClient == lastTokenOnServer)
                {
                    Logger.Debug("No change from remote, wait for the next time.");
                    return;
                }

                if (lastTokenOnClient == null)
                {
                    // Token is null, which means no sync has ever happened yet, so just sync everything from remote.
                    CrawlRemote(remoteFolder, repoinfo.TargetDirectory, new List<string>(), new List<string>());
                    
                    Logger.Info("Succeeded to sync from remote, update ChangeLog token: " + lastTokenOnServer);

                    database.SetChangeLogToken(lastTokenOnServer);
                }

                do
                {
                    Config.Feature f = null;
                    if(ConfigManager.CurrentConfig.getFolder(repoinfo.Name)!=null)
                        f = ConfigManager.CurrentConfig.getFolder(repoinfo.Name).SupportedFeatures;
                    int maxNumItems = (f!=null && f.MaxNumberOfContentChanges!=null)? (int)f.MaxNumberOfContentChanges: 100;
                    // Check which files/folders have changed.
                    IChangeEvents changes = session.GetContentChanges(lastTokenOnClient, IsPropertyChangesSupported, maxNumItems);
                    // Replicate each change to the local side.
                    bool success = true;
                    foreach (IChangeEvent change in changes.ChangeEventList)
                    {
                        try
                        {
                            switch (change.ChangeType)
                            {
                                case ChangeType.Created:
                                    Logger.Info("New remote object ("+change.ObjectId+") found.");
                                    goto case ChangeType.Updated; // TODO optimisation: process creation only?
                                case ChangeType.Updated:
                                    if(change.ChangeType == ChangeType.Updated)
                                        Logger.Info("Remote object ("+change.ObjectId + ") has been updated remotely.");
                                    success = ApplyRemoteChangeUpdate(change) && success;
                                    break;
                                case ChangeType.Deleted:
                                    Logger.Info("Remote object ("+change.ObjectId+") has been deleted remotely.");
                                    success = ApplyRemoteChangeDelete(change) && success;
                                    break;
                                default:
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Warn("Exception when applying change: ", e);
                            success = false;
                        }
                    }

                    if (success)
                    {
                        // Save ChangeLog token locally.
                        if (changes.HasMoreItems == true)
                        {
                            lastTokenOnClient = changes.LatestChangeLogToken;
                        }
                        else
                        {
                            lastTokenOnClient = lastTokenOnServer;
                        }
                        Logger.Info("Sync the changes on server, update ChangeLog token: " + lastTokenOnClient);
                        database.SetChangeLogToken(lastTokenOnClient);
                        session.Binding.GetRepositoryService().GetRepositoryInfos(null);    //  refresh
                        lastTokenOnServer = session.Binding.GetRepositoryService().GetRepositoryInfo(session.RepositoryInfo.Id, null).LatestChangeLogToken;
                    }
                    else
                    {
                        Logger.Warn("Failure to sync the changes on server, force crawl sync from remote");
                        CrawlRemote(remoteFolder, repoinfo.TargetDirectory, null, null); // TODO why not a full CrawlSync?
                        Logger.Info("Succeeded to sync from remote, update ChangeLog token: " + lastTokenOnServer);
                        database.SetChangeLogToken(lastTokenOnServer);
                        return;
                    }
                }
                while (!lastTokenOnServer.Equals(lastTokenOnClient));
            }


            /// <summary>
            /// Apply a remote change for Created or Updated.
            /// <returns>Whether the change was applied successfully</returns>
            /// </summary>
            private bool ApplyRemoteChangeUpdate(IChangeEvent change)
            {
                ICmisObject cmisObject = null;
                IFolder remoteFolder = null;
                IDocument remoteDocument = null;
                string remotePath = null;
                IFolder remoteParent = null;

                // Get the remote changed object.
                try
                {
                    cmisObject = session.GetObject(change.ObjectId);
                }
                catch(CmisObjectNotFoundException)
                {
                    Logger.Info("Ignore change as its target object can not be found anymore:" + change.ObjectId);
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Warn("Ignore change as an exception occurred:" + change.ObjectId + " :", e);
                    return false;
                }

                // Check whether change is about a document or folder.
                remoteDocument = cmisObject as IDocument;
                remoteFolder = cmisObject as IFolder;
                if (remoteDocument == null && remoteFolder == null)
                {
                    Logger.Info("Ignore change as it is not about a document nor folder: " + change.ObjectId);
                    return true;
                }

                // Check whether it is a document worth syncing.
                if (remoteDocument != null)
                {
                    if ( ! Utils.IsFileWorthSyncing(remoteDocument.Name, repoinfo))
                    {
                        Logger.Info("Ignore change as it is about a document unworth syncing: " + remoteDocument.Paths);
                        return true;
                    }
                    if (remoteDocument.Paths.Count == 0)
                    {
                        Logger.Info("Ignore the unfiled object: " + remoteDocument.Name);
                        return true;
                    }
                    // TODO: Support Multiple Paths
                    remotePath = remoteDocument.Paths[0];
                    remoteParent = remoteDocument.Parents[0];
                }

                // Check whether it is a folder worth syncing.
                if (remoteFolder != null)
                {
                    remotePath = remoteFolder.Path;
                    remoteParent = remoteFolder.FolderParent;
                    foreach (string name in remotePath.Split('/'))
                    {
                        if ( ! String.IsNullOrEmpty(name) && Utils.IsInvalidFolderName(name))
                        {
                            Logger.Info(String.Format("Change in illegal syncing path name {0}: {1}", name, remotePath));
                            return true;
                        }
                    }
                }

                // Ignore the change if not in a synchronized folder.
                if ( ! remotePath.StartsWith(this.remoteFolderPath))
                {
                    Logger.Info("Ignore change as it is not in the synchronized folder's path: " + remotePath);
                    return true;
                }
                if (this.repoinfo.isPathIgnored(remotePath))
                {
                    Logger.Info("Ignore change as it is in a path configured to be ignored: " + remotePath);
                    return true;
                }
                string relativePath = remotePath.Substring(remoteFolderPath.Length);
                if (relativePath.Length <= 0)
                {
                    Logger.Info("Ignore change as it is above the synchronized folder's path:: " + remotePath);
                    return true;
                }

                /*
                if (relativePath[0] == '/')
                {
                    relativePath = relativePath.Substring(1);
                }

                string localPath = Path.Combine(repoinfo.TargetDirectory, relativePath).Replace('/', Path.DirectorySeparatorChar);
                if ( ! DownloadFolder(remoteParent, Path.GetDirectoryName(localPath)))
                {
                    Logger.Warn("Failed to download the parent folder for " + localPath);
                    return false;
                }

                // Case of a document.
                if (null != remoteDocument)
                {
                    Logger.Info(String.Format("New remote document ({0}) found.", remotePath));
                    string savedDocumentPath = database.GetRemoteFilePath(change.ObjectId);
                    if ((null != savedDocumentPath) && (savedDocumentPath != localPath))
                    {
                        if (File.Exists(localPath))
                        {
                            File.Delete(savedDocumentPath);
                            database.RemoveFile(SyncItemFactory.CreateFromRemotePath(savedDocumentPath, repoinfo));
                        }
                        else
                        {
                            if (File.Exists(savedDocumentPath))
                            {
                                if (!Directory.Exists(Path.GetDirectoryName(localPath)))
                                {
                                    Logger.Warn("Creating local directory: "+ localPath);
                                    Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                                }
                                File.Move(savedDocumentPath, localPath);
                            }
                            database.MoveFile(SyncItemFactory.CreateFromRemotePath(savedDocumentPath, repoinfo), SyncItemFactory.CreateFromLocalPath(savedDocumentPath, repoinfo));
                        }
                    }

                    return SyncDownloadFile(remoteDocument, Path.GetDirectoryName(localPath));
                }

                // Case of a folder.
                if (null != remoteFolder)
                {
                    Logger.Info(String.Format("New remote folder ({0}) found.", remotePath));
                    string savedFolderPath = database.GetFolderPath(change.ObjectId);
                    if ((null != savedFolderPath) && (savedFolderPath != localPath))
                    {
                        Utils.MoveFolderLocally(savedFolderPath, localPath);
                        // TODO CrawlSync folder content?
                    }
                    else
                    {
                        SyncDownloadFolder(remoteFolder, Path.GetDirectoryName(localPath));
                        CrawlSync(remoteFolder,localPath);
                    }
                }
                */
                return false; // Still need to perform a CrawlSync
            }


            /// <summary>
            /// Apply a remote change for Deleted.
            /// </summary>
            private bool ApplyRemoteChangeDelete(IChangeEvent change)
            {
                // Maybe it is a folder?
                string path = database.GetFolderPath(change.ObjectId);
                if (path != null)
                {
                    // Remove the folder from filesystem.
                    Directory.Delete(path);
                    // Remove the folder from database.
                    database.RemoveFolder(SyncItemFactory.CreateFromLocalPath(path, repoinfo)); // TODO optimisation: remove by id
                }
                else
                {
                    // Or maybe it is a file?
                    path = database.GetFilePath(change.ObjectId);
                    if (path != null)
                    {
                        // Remove the file from filesystem.
                        File.Delete(path);
                        // Remove the file from database.
                        database.RemoveFile(SyncItemFactory.CreateFromLocalPath(path, repoinfo)); // TODO optimisation: remove by id
                    }
                }
                return true;
            }

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
                return SyncDownloadFolder(remoteFolder, Path.GetDirectoryName(localFolder));
            }
        }
    }
}
