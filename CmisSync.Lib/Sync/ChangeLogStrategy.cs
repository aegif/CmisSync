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
                    foreach (IChangeEvent change in changes.ChangeEventList)
                    {
                        ApplyRemoteChange(change);
                    }

                    // Save change log token locally.
                    // TODO only if successful
                    lastTokenOnClient = changes.LatestChangeLogToken;
                    Logger.Info("Sync the changes on server, update ChangeLog token: " + lastTokenOnClient);
                    database.SetChangeLogToken(lastTokenOnClient);
                    lastTokenOnServer = session.Binding.GetRepositoryService().GetRepositoryInfo(session.RepositoryInfo.Id, null).LatestChangeLogToken;
                }
                while (!lastTokenOnServer.Equals(lastTokenOnClient));
            }


            /// <summary>
            /// Apply a remote change.
            /// </summary>
            private bool ApplyRemoteChange(IChangeEvent change)
            {
                Logger.Info("Sync | Change type:" + change.ChangeType.ToString() + " id:" + change.ObjectId + " properties:" + change.Properties);
                IFolder remoteFolder;
                IDocument remoteDocument;
                switch (change.ChangeType)
                {
                    // Case when an object has been created or updated.
                    case ChangeType.Created:
                    case ChangeType.Updated:
                        ICmisObject cmisObject = session.GetObject(change.ObjectId);
                        if (null != (remoteDocument = cmisObject as IDocument))
                        {
                            string remoteDocumentPath = Path.Combine(remoteDocument.Paths.ToArray());
                            remoteDocumentPath.Replace("\\", "/");
                            if (!remoteDocumentPath.StartsWith(remoteFolderPath))
                            {
                                Logger.Info("Change in unrelated document: " + remoteDocumentPath);
                                return true;    // The change is not under the folder we care about.
                            }
                            string relativePath = remoteDocumentPath.Substring(remoteFolderPath.Length);
                            if (relativePath[0] == '/')
                            {
                                relativePath = relativePath.Substring(1);
                            }
                            foreach (string name in relativePath.Split('/'))
                            {
                                if (!Utils.WorthSyncing(name))
                                {
                                    Logger.Info("Change in unworth syncing document: " + remoteDocumentPath);
                                    return true;
                                }
                            }
                            string relativeFolderPath = Path.GetDirectoryName(relativePath);
                            string localFolderPath = Path.Combine(repoinfo.TargetDirectory, relativeFolderPath);
                            return SyncDownloadFile(remoteDocument, localFolderPath);
                        }
                        else if (null != (remoteFolder = cmisObject as IFolder))
                        {
                            string localFolder = Path.Combine(repoinfo.TargetDirectory, remoteFolder.Path);
                            if(!this.repoinfo.isPathIgnored(remoteFolder.Path))
                                return RecursiveFolderCopy(remoteFolder, localFolder);
                        }
                        break;

                    // Case when an object has been deleted.
                    case ChangeType.Deleted:
                        cmisObject = session.GetObject(change.ObjectId);
                        if (null != (remoteDocument = cmisObject as IDocument))
                        {
                            string remoteDocumentPath = remoteDocument.Paths.First();
                            if (!remoteDocumentPath.StartsWith(remoteFolderPath))
                            {
                                Logger.Info("Sync | Change in unrelated document: " + remoteDocumentPath);
                                break; // The change is not under the folder we care about.
                            }
                            string relativePath = remoteDocumentPath.Substring(remoteFolderPath.Length + 1);
                            string relativeFolderPath = Path.GetDirectoryName(relativePath);
                            relativeFolderPath = relativeFolderPath.Replace('/', '\\'); // TODO OS-specific separator
                            string localFolderPath = Path.Combine(repoinfo.TargetDirectory, relativeFolderPath);
                            // TODO DeleteFile(localFolderPath); // Delete on filesystem and in database
                        }
                        else if (null != (remoteFolder = cmisObject as IFolder))
                        {
                            string localFolder = Path.Combine(repoinfo.TargetDirectory, remoteFolder.Path);
                            if(!this.repoinfo.isPathIgnored(remoteFolder.Path))
                                RemoveFolderLocally(localFolder); // Remove from filesystem and database.
                        }
                        break;

                    // Case when access control or security policy has changed.
                    case ChangeType.Security:
                        // TODO
                        break;
                }

                return true;
            }
        }
    }
}
