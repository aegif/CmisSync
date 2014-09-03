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
                    CrawlRemote(remoteFolder, repoinfo.TargetDirectory, null, null);
                    
                    Logger.Info("Succeeded to sync from remote, update ChangeLog token: " + lastTokenOnServer);
                    // *** SetChangeLogToken
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
                                    goto case ChangeType.Updated;
                                case ChangeType.Updated:
                                    if(change.ChangeType == ChangeType.Updated)
                                        Logger.Info("Remote object ("+change.ObjectId + ") has been changed remotely.");
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
                            Logger.Warn("Exception when apply the change: ", e);
                            success = false;
                        }
                    }

                    if (success)
                    {
                        // Save change log token locally.
                        if (changes.HasMoreItems == true)
                        {
                            lastTokenOnClient = changes.LatestChangeLogToken;
                        }
                        else
                        {
                            lastTokenOnClient = lastTokenOnServer;
                        }
                        Logger.Info("Sync the changes on server, update ChangeLog token: " + lastTokenOnClient);
                        // *** SetChangeLogToken
                        database.SetChangeLogToken(lastTokenOnClient);
                        session.Binding.GetRepositoryService().GetRepositoryInfos(null);    //  refresh
                        lastTokenOnServer = session.Binding.GetRepositoryService().GetRepositoryInfo(session.RepositoryInfo.Id, null).LatestChangeLogToken;
                    }
                    else
                    {
                        Logger.Warn("Failure to sync the changes on server, force crawl sync from remote");
                        // *** SetChangeLogToken
                        CrawlRemote(remoteFolder, repoinfo.TargetDirectory, null, null);
                        Logger.Info("Succeeded to sync from remote, update ChangeLog token: " + lastTokenOnServer);
                        database.SetChangeLogToken(lastTokenOnServer);
                        return;
                    }
                }
                while (!lastTokenOnServer.Equals(lastTokenOnClient));
            }


            /// <summary>
            /// Apply a remote change for Created or Updated.
            /// </summary>
            private bool ApplyRemoteChangeUpdate(IChangeEvent change)
            {
                ICmisObject cmisObject = null;
                IFolder remoteFolder = null;
                IDocument remoteDocument = null;
                string remotePath = null;
                ICmisObject remoteObject = null;
                IFolder remoteParent = null;

                try
                {
                    cmisObject = session.GetObject(change.ObjectId);
                }
                catch(CmisObjectNotFoundException)
                {
                    Logger.Info("Ignore the missed object for " + change.ObjectId);
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Warn("Exception when GetObject for " + change.ObjectId + " :", e);
                    return false;
                }

                remoteDocument = cmisObject as IDocument;
                remoteFolder = cmisObject as IFolder;
                if (remoteDocument == null && remoteFolder == null)
                {
                    Logger.Info("Change in no sync object: " + change.ObjectId);
                    return true;
                }
                if (remoteDocument != null)
                {
                    if (!Utils.IsFileWorthSyncing(remoteDocument.Name, repoinfo))
                    {
                        Logger.Info("Change in remote unworth syncing file: " + remoteDocument.Paths);
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
                if (remoteFolder != null)
                {
                    remotePath = remoteFolder.Path;
                    remoteParent = remoteFolder.FolderParent;
                    foreach (string name in remotePath.Split('/'))
                    {
                        if (!String.IsNullOrEmpty(name) && Utils.IsInvalidFolderName(name))
                        {
                            Logger.Info(String.Format("Change in illegal syncing path name {0}: {1}", name, remotePath));
                            return true;
                        }
                    }
                }

                if (!remotePath.StartsWith(this.remoteFolderPath))
                {
                    Logger.Info("Change in unrelated path: " + remotePath);
                    return true;    // The change is not under the folder we care about.
                }

                if (this.repoinfo.isPathIgnored(remotePath))
                {
                    Logger.Info("Change in ignored path: " + remotePath);
                    return true;
                }

                string relativePath = remotePath.Substring(remoteFolderPath.Length);
                if (relativePath.Length <= 0)
                {
                    Logger.Info("Ignore change in root path: " + remotePath);
                    return true;
                }
                if (relativePath[0] == '/')
                {
                    relativePath = relativePath.Substring(1);
                }

                try
                {
                    remoteObject = session.GetObjectByPath(remotePath);
                }
                catch(CmisObjectNotFoundException)
                {
                    Logger.Info(String.Format("Ignore remote path {0} deleted from id {1}", remotePath, cmisObject.Id));
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Warn("Exception when GetObject for " + remotePath + " : ", e);
                    return false;
                }

                if (remoteObject.Id != cmisObject.Id)
                {
                    Logger.Info(String.Format("Ignore remote path {0} changed from id {1} to id {2}", remotePath, cmisObject.Id, remoteObject.Id));
                    return true;
                }

                string localPath = Path.Combine(repoinfo.TargetDirectory, relativePath).Replace('/', Path.DirectorySeparatorChar);
                if (!DownloadFolder(remoteParent, Path.GetDirectoryName(localPath)))
                {
                    Logger.Warn("Failed to download the parent folder for " + localPath);
                    return false;
                }

                if (null != remoteDocument)
                {
                    Logger.Info(String.Format("New remote file ({0}) found.", remotePath));
                    //  check moveObject
                    // *** GetFilePath
                    string savedDocumentPath = database.GetRemoteFilePath(change.ObjectId);
                    if ((null != savedDocumentPath) && (savedDocumentPath != localPath))
                    {
                        if (File.Exists(localPath))
                        {
                            File.Delete(savedDocumentPath);
                            // *** Remove File
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
                            // *** Move File
                            database.MoveFile(SyncItemFactory.CreateFromRemotePath(savedDocumentPath, repoinfo), SyncItemFactory.CreateFromLocalPath(savedDocumentPath, repoinfo));
                        }
                    }

                    return SyncDownloadFile(remoteDocument, Path.GetDirectoryName(localPath));
                }

                if (null != remoteFolder)
                {
                    Logger.Info(String.Format("New remote folder ({0}) found.", remotePath));
                    //  check moveObject
                    // *** GetFolderPath
                    string savedFolderPath = database.GetFolderPath(change.ObjectId);
                    if ((null != savedFolderPath) && (savedFolderPath != localPath))
                    {
// TODO                        MoveFolderLocally(savedFolderPath, localPath);
                        CrawlSync(remoteFolder, localPath);
                    }
                    else
                    {
                        SyncDownloadFolder(remoteFolder, Path.GetDirectoryName(localPath));
                        CrawlSync(remoteFolder,localPath);
                    }
                }

                return true;
            }


            /// <summary>
            /// Apply a remote change for Deleted.
            /// </summary>
            private bool ApplyRemoteChangeDelete(IChangeEvent change)
            {
                try
                {
                    ICmisObject remoteObject = session.GetObject(change.ObjectId);
                    if (null != remoteObject)
                    {
                        //  should be moveObject
                        Logger.Info("Ignore moveObject for id " + change.ObjectId);
                        return true;
                    }
                }
                catch (CmisObjectNotFoundException)
                {
                }
                catch (Exception e)
                {
                    Logger.Warn("Exception when GetObject for " + change.ObjectId + " : ", e);
                }
                // *** GetFilePath
                string savedDocumentPath = database.GetRemoteFilePath(change.ObjectId); // FIXME use SyncItem to differentiate between local path and remote path
                if (null != savedDocumentPath)
                {
                    Logger.Info("Remove local document: " + savedDocumentPath);
                    if(File.Exists(savedDocumentPath))
                        File.Delete(savedDocumentPath);
                    // *** Remove File
                    database.RemoveFile(SyncItemFactory.CreateFromRemotePath(savedDocumentPath, repoinfo));
                    Logger.Info("Removed local document: " + savedDocumentPath);
                    return true;
                }

                // *** GetFolderPath
                string savedFolderPath = database.GetFolderPath(change.ObjectId);
                if (null != savedFolderPath)
                {
                    Logger.Info("Remove local folder: " + savedFolderPath);
                    if(Directory.Exists(savedFolderPath)) {
                        Directory.Delete(savedFolderPath, true);
                        // *** Remove Folder
                        database.RemoveFolder(SyncItemFactory.CreateFromRemotePath(savedFolderPath, repoinfo));
                    }
                    Logger.Info("Removed local folder: " + savedFolderPath);
                    return true;
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
