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
                        success = ApplyRemoteChange(change) && success;
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
            /// Apply a remote change.
            /// </summary>
            private bool ApplyRemoteChange(IChangeEvent change)
            {
                Logger.Debug("Change type:" + change.ChangeType.ToString() + " id:" + change.ObjectId + " properties:" + change.Properties);
                switch (change.ChangeType)
                {
                    case ChangeType.Created:
                    case ChangeType.Updated:
                    case ChangeType.Deleted:
                        break;
                    // Case when access control or security policy has changed.
                    case ChangeType.Security:
                    // TODO
                    default:
                        return true;
                }

                ICmisObject cmisObject;
                try
                {
                    cmisObject = session.GetObject(change.ObjectId);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Exception when GetObject for " + change.ObjectId + " : " + Utils.ToLogString(ex));
                    return false;
                }
                IFolder remoteFolder = null;
                IDocument remoteDocument = null;

                string remotePath;
                remoteDocument = cmisObject as IDocument;
                if (remoteDocument != null)
                {
                    remotePath = Path.Combine(remoteDocument.Paths.ToArray()).Replace('\\','/');
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

                ICmisObject remoteObject = null;
                try
                {
                    remoteObject = session.GetObjectByPath(remotePath);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Exception when GetObject for " + remotePath + " : " + Utils.ToLogString(ex));
                }


                switch (change.ChangeType)
                {
                    // Case when an object has been created or updated.
                    case ChangeType.Created:
                    case ChangeType.Updated:
                        if (null == remoteObject)
                        {
                            Logger.Info(String.Format("Ignore remote path {0} deleted from {1}", remotePath, cmisObject.Id));
                            return true;
                        }
                        if (remoteObject.Id != cmisObject.Id)
                        {
                            Logger.Info(String.Format("Ignore remote path {0} changed from {1} to {2}", remotePath, cmisObject.Id, remoteObject.Id));
                            return true;
                        }

                        if (null != remoteDocument)
                        {
                            string localFolderPath = Path.Combine(repoinfo.TargetDirectory, Path.GetDirectoryName(relativePath));
                            return SyncDownloadFile(remoteDocument, localFolderPath);
                        }

                        if (null != remoteFolder)
                        {
                            string localFolderPath = Path.Combine(repoinfo.TargetDirectory, relativePath);
                            Directory.CreateDirectory(localFolderPath);
                            database.AddFolder(localFolderPath, remoteFolder.LastModificationDate);
                            Logger.Info(String.Format("Create local folder {0} for remote folder {1}.", localFolderPath, remotePath));
                            return true;
                        }
                        break;

                    // Case when an object has been deleted.
                    case ChangeType.Deleted:
                        if (null != remoteObject)
                        {
                            Logger.Info(String.Format("Ignore remote path {0} created from deleted {1} to {2}", remotePath, cmisObject.Id, remoteObject.Id));
                        }

                        if (null != remoteDocument)
                        {
                            string localDocumentPath = Path.Combine(repoinfo.TargetDirectory, relativePath);
                            if (File.Exists(localDocumentPath))
                            {
                                File.Delete(localDocumentPath);
                                database.RemoveFile(localDocumentPath);
                                Logger.Info("Remove locally document: " + localDocumentPath);
                            }
                            return true;
                        }
                        
                        if (null != remoteFolder)
                        {
                            string localFolderPath = Path.Combine(repoinfo.TargetDirectory, relativePath);
                            if (Directory.Exists(localFolderPath))
                            {
                                Directory.Delete(localFolderPath,true);
                                database.RemoveFolder(localFolderPath);
                                Logger.Info("Remove locally folder: " + localFolderPath);
                            }
                            return true;
                        }
                        break;

                    // Case when access control or security policy has changed.
                    case ChangeType.Security:
                    // TODO
                    default:
                        break;
                }

                return true;
            }
        }
    }
}
