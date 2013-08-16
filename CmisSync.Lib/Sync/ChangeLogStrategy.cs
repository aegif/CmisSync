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
                string lastTokenOnServer = session.Binding.GetRepositoryService().GetRepositoryInfo(session.RepositoryInfo.Id, null).LatestChangeLogToken;

                // Get last change token that had been saved on client side.
                // TODO catch exception invalidArgument which means that changelog has been truncated and this token is not found anymore.
                string lastTokenOnClient = database.GetChangeLogToken();

                if (lastTokenOnClient == null)
                {
                    // Token is null, which means no sync has ever happened yet, so just sync everything from remote.
                    if (CrawlRemote(remoteFolder, repoinfo.TargetDirectory, null, null))
                    {
                        Logger.Info("Success to sync from remote, update ChangeLog token: " + lastTokenOnServer);
                        database.SetChangeLogToken(lastTokenOnServer);
                    }
                    else
                    {
                        Logger.Warn("Failure to sync from remote, wait for the next time.");
                    }
                    return;
                }

                do
                {
                    // Check which files/folders have changed.
                    int maxNumItems = 100;
                    IChangeEvents changes = session.GetContentChanges(lastTokenOnClient, true, maxNumItems);

                    // Replicate each change to the local side.
                    bool success = true;
                    foreach (IChangeEvent change in changes.ChangeEventList)
                    {
                        Logger.Debug("Change type:" + change.ChangeType.ToString() + " id:" + change.ObjectId + " properties:" + change.Properties);
                        try
                        {
                            switch (change.ChangeType)
                            {
                                case ChangeType.Created:
                                case ChangeType.Updated:
                                    success = ApplyRemoteChangeUpdate(change) && success;
                                    break;
                                case ChangeType.Deleted:
                                    success = ApplyRemoteChangeDelete(change) && success;
                                    break;
                                default:
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("Exception when apply the change: " + Utils.ToLogString(ex));
                            success = false;
                        }
                    }

                    if (success)
                    {
                        // Save change log token locally.
                        lastTokenOnClient = changes.LatestChangeLogToken;
                        Logger.Info("Sync the changes on server, update ChangeLog token: " + lastTokenOnClient);
                        database.SetChangeLogToken(lastTokenOnClient);
                        lastTokenOnServer = session.Binding.GetRepositoryService().GetRepositoryInfo(session.RepositoryInfo.Id, null).LatestChangeLogToken;
                    }
                    else
                    {
                        Logger.Warn("Failure to sync the changes on server, force crawl sync from remote");
                        if (CrawlRemote(remoteFolder, repoinfo.TargetDirectory, null, null))
                        {
                            Logger.Info("Success to sync from remote, update ChangeLog token: " + lastTokenOnServer);
                            database.SetChangeLogToken(lastTokenOnServer);
                        }
                        else
                        {
                            Logger.Warn("Failure to sync from remote, wait for the next time.");
                        }
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

                try
                {
                    cmisObject = session.GetObject(change.ObjectId);
                }
                catch(CmisObjectNotFoundException)
                {
                    Logger.Warn("Ignore the missed object for " + change.ObjectId);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Warn("Exception when GetObject for " + change.ObjectId + " : " + Utils.ToLogString(ex));
                    return false;
                }

                remoteDocument = cmisObject as IDocument;
                if (remoteDocument != null)
                {
                    remotePath = Path.Combine(remoteDocument.Paths.ToArray()).Replace('\\', '/');
                }
                else
                {
                    remoteFolder = cmisObject as IFolder;
                    if (remoteFolder == null)
                    {
                        Logger.Info("Change in no sync object: " + change.ObjectId);
                        return true;
                    }
                    remotePath = remoteFolder.Path;
                    if (this.repoinfo.isPathIgnored(remotePath))
                    {
                        Logger.Info("Change in ignored path: " + remotePath);
                        return true;
                    }
                }

                if (!remotePath.StartsWith(remoteFolderPath))
                {
                    Logger.Info("Change in unrelated path: " + remotePath);
                    return true;    // The change is not under the folder we care about.
                }

                string relativePath = remotePath.Substring(remoteFolderPath.Length);
                if (relativePath[0] == '/')
                {
                    relativePath = relativePath.Substring(1);
                }
                foreach (string name in relativePath.Split('/'))
                {
                    if (!Utils.WorthSyncing(name))
                    {
                        Logger.Info("Change in unworth syncing path: " + remotePath);
                        return true;
                    }
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
                catch (Exception ex)
                {
                    Logger.Warn("Exception when GetObject for " + remotePath + " : " + Utils.ToLogString(ex));
                    return false;
                }

                if (remoteObject.Id != cmisObject.Id)
                {
                    Logger.Info(String.Format("Ignore remote path {0} changed from id {1} to id {2}", remotePath, cmisObject.Id, remoteObject.Id));
                    return true;
                }

                string localPath = Path.Combine(repoinfo.TargetDirectory, relativePath);

                if (null != remoteDocument)
                {
                    //  check moveObject
                    string savedDocumentPath = database.GetFilePath(change.ObjectId);
                    if ((null != savedDocumentPath) && (savedDocumentPath != localPath))
                    {
                        if (File.Exists(localPath))
                        {
                            File.Delete(savedDocumentPath);
                            database.RemoveFile(savedDocumentPath);
                        }
                        else
                        {
                            if (File.Exists(savedDocumentPath))
                            {
                                File.Move(savedDocumentPath, localPath);
                            }
                            database.MoveFile(savedDocumentPath, localPath);
                        }
                    }

                    return SyncDownloadFile(remoteDocument, Path.GetDirectoryName(localPath));
                }

                if (null != remoteFolder)
                {
                    //  check moveObject
                    string savedFolderPath = database.GetFolderPath(change.ObjectId);
                    if ((null != savedFolderPath) && (savedFolderPath != localPath))
                    {
                        MoveFolderLocally(savedFolderPath, localPath);
                        return CrawlSync(remoteFolder, localPath);
                    }
                    else
                    {
                        return SyncDownloadFolder(remoteFolder, Path.GetDirectoryName(localPath));
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
                catch (Exception ex)
                {
                    Logger.Warn("Exception when GetObject for " + change.ObjectId + " : " + Utils.ToLogString(ex));
                }

                string savedDocumentPath = database.GetFilePath(change.ObjectId);
                if (null != savedDocumentPath)
                {
                    File.Delete(savedDocumentPath);
                    database.RemoveFile(savedDocumentPath);
                    Logger.Info("Remove locally document: " + savedDocumentPath);
                    return true;
                }

                string savedFolderPath = database.GetFolderPath(change.ObjectId);
                if (null != savedFolderPath)
                {
                    Directory.Delete(savedFolderPath,true);
                    database.RemoveFolder(savedFolderPath);
                    Logger.Info("Remove locally folder: " + savedFolderPath);
                    return true;
                }

                return true;
            }
        }
    }
}
