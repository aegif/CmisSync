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
                    // Token is null, which means no sync has ever happened yet, so just copy everything.
                    // RecursiveFolderCopy(remoteFolder, localRootFolder);
                    RecursiveFolderCopy(remoteFolder, repoinfo.TargetDirectory);
                }
                else
                {
                    // If there are remote changes, apply them.
                    if (lastTokenOnServer.Equals(lastTokenOnClient))
                    {
                        Logger.Info("Sync | No changes on server, ChangeLog token: " + lastTokenOnServer);
                    }
                    else
                    {
                        // Check which files/folders have changed.
                        int maxNumItems = 1000;
                        IChangeEvents changes = session.GetContentChanges(lastTokenOnClient, true, maxNumItems);

                        // Replicate each change to the local side.
                        foreach (IChangeEvent change in changes.ChangeEventList)
                        {
                            ApplyRemoteChange(change);
                        }

                        // Save change log token locally.
                        // TODO only if successful
                        Logger.Info("Sync | Updating ChangeLog token: " + lastTokenOnServer);
                        database.SetChangeLogToken(lastTokenOnServer);
                    }

                    // Upload local changes by comparing with database.
                    // TODO
                }
            }


            /// <summary>
            /// Apply a remote change.
            /// </summary>
            private void ApplyRemoteChange(IChangeEvent change)
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
                            DownloadFile(remoteDocument, localFolderPath);
                        }
                        else if (null != (remoteFolder = cmisObject as IFolder))
                        {
                            string localFolder = Path.Combine(repoinfo.TargetDirectory, remoteFolder.Path);
                            if(!this.repoinfo.isPathIgnored(remoteFolder.Path))
                                RecursiveFolderCopy(remoteFolder, localFolder);
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
            }
        }
    }
}
