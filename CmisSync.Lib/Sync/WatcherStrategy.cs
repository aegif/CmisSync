using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.Common;

using DotCMIS;
using DotCMIS.Client;
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
            /// <summary>
            /// Watchers the sync.
            /// </summary>
            /// <param name="remoteFolder">Remote folder.</param>
            /// <param name="localFolder">Local folder.</param>
            private void WatcherSync(string remoteFolder, string localFolder)
            {
                Logger.Debug(remoteFolder + " : " + localFolder);
                foreach (string pathname in repo.Watcher.GetChangeList())
                {
                    if (repoinfo.isPathIgnored(pathname))
                    {
                        repo.Watcher.RemoveChange(pathname);
                        continue;
                    }

                    if (!pathname.StartsWith(localFolder))
                    {
                        Debug.Assert(false, String.Format("Invalid pathname {0} for target {1}.", pathname, localFolder));
                    }

                    if (pathname == localFolder)
                    {
                        repo.Watcher.RemoveChange(pathname);
                        continue;
                    }

                    Watcher.ChangeTypes change = repo.Watcher.GetChangeType(pathname);
                    bool worthSync = true;
                    string name = pathname.Substring(localFolder.Length + 1);
                    string[] folderNames = name.Split(Path.DirectorySeparatorChar);
                    for (int i = 0; i < folderNames.Length - 1; i++)
                    {
                        if (Utils.IsInvalidFolderName(folderNames[i]))
                        {
                            repo.Watcher.RemoveChange(pathname);
                            worthSync = false;
                            break;
                        }
                    }
                    if (change == Watcher.ChangeTypes.Changed || change == Watcher.ChangeTypes.Created)
                    {
                        if ((File.Exists(pathname) && !Utils.IsDirectoryWorthSyncing(folderNames[folderNames.Length - 1], repoinfo)) ||
                           (Directory.Exists(pathname) && Utils.IsInvalidFolderName(folderNames[folderNames.Length - 1])))
                        {
                            repo.Watcher.RemoveChange(pathname);
                            worthSync = false;
                        }
                    }
                    if (!worthSync)
                    {
                        continue;
                    }

                    Logger.Debug(String.Format("Detect change {0} for {1}.", change, pathname));
                    switch (change)
                    {
                        case Watcher.ChangeTypes.Created:
                        case Watcher.ChangeTypes.Changed:
                            WatcherSyncUpdate(remoteFolder, localFolder, pathname);
                            break;
                        case Watcher.ChangeTypes.Deleted:
                            WatcherSyncDelete(remoteFolder, localFolder, pathname);
                            break;
                        default:
                            Debug.Assert(false, String.Format("Invalid change {0} for pathname {1}.", change, pathname));
                            break;
                    }
                }
            }

            /// <summary>
            /// Watchers the sync update.
            /// </summary>
            /// <param name="remoteFolder">Remote folder.</param>
            /// <param name="localFolder">Local folder.</param>
            /// <param name="pathname">Pathname.</param>
            private void WatcherSyncUpdate(string remoteFolder, string localFolder, string pathname)
            {
                string name = pathname.Substring(localFolder.Length + 1);
                string remotePathname = CmisUtils.PathCombine(remoteFolder, name);

                IFolder remoteBase = null;
                if (File.Exists(pathname) || Directory.Exists(pathname))
                {
                    string remoteBaseName = Path.GetDirectoryName(remotePathname).Replace('\\', '/');
                    try
                    {
                        remoteBase = (IFolder)session.GetObjectByPath(remoteBaseName);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn(String.Format("Exception when query remote {0}: ", remoteBaseName), e);
                    }
                    if (null == remoteBase)
                    {
                        Logger.Warn(String.Format("The remote base folder {0} for local {1} does not exist, ignore for the update action", remoteBaseName, pathname));
                        return;
                    }
                }
                else
                {
                    Logger.Info(String.Format("The file/folder {0} is deleted, ignore for the update action", pathname));
                    return;
                }

                try
                {
                    if (File.Exists(pathname))
                    {
                        bool success = false;
                        repo.Watcher.RemoveChange(pathname, Watcher.ChangeTypes.Created);
                        repo.Watcher.RemoveChange(pathname, Watcher.ChangeTypes.Changed);
                        // *** ContainsFile
                        if (database.ContainsFile(SyncItemFactory.CreateFromLocalPath(pathname, repoinfo)))
                        {
                            // *** LocalFileHasChanged
                            if (database.LocalFileHasChanged(pathname))
                            {
                                success = UpdateFile(pathname, remoteBase);
                                Logger.Info(String.Format("Update {0}: {1}", pathname, success));
                            }
                            else
                            {
                                success = true;
                                Logger.Info(String.Format("File {0} remains unchanged, ignore for the update action", pathname));
                            }
                        }
                        else
                        {
                            success = UploadFile(pathname, remoteBase);
                            Logger.Info(String.Format("Upload {0}: {1}", pathname, success));
                        }
                        if (!success)
                        {
                            Logger.Warn("Failure to update: " + pathname);
                            repo.Watcher.InsertChange(pathname, Watcher.ChangeTypes.Changed);
                        }
                        return;
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn(String.Format("Exception while sync to update file {0} : ", pathname), e);
                    return;
                }

                try
                {
                    if (Directory.Exists(pathname))
                    {
                        if (repoinfo.isPathIgnored(remotePathname))
                        {
                            repo.Watcher.RemoveChange(pathname);
                            return;
                        }

                        // *** ContainsFolder
                        if (database.ContainsFolder(pathname))
                        {
                            Logger.Info(String.Format("Database exists for {0}, ignore for the update action", pathname));
                            repo.Watcher.RemoveChange(pathname);
                        }
                        else
                        {
                            Logger.Info("Uploading local folder to server: " + pathname);
                            UploadFolderRecursively(remoteBase, pathname);
                            repo.Watcher.RemoveChange(pathname);
                        }
                        return;
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn(String.Format("Exception while sync to update folder {0}: ", pathname), e);
                    return;
                }

                Logger.Info(String.Format("The file/folder {0} is deleted, ignore for the update action", pathname));
            }

            /// <summary>
            /// Watchers the sync delete.
            /// </summary>
            /// <param name="remoteFolder">Remote folder.</param>
            /// <param name="localFolder">Local folder.</param>
            /// <param name="pathname">Pathname.</param>
            private void WatcherSyncDelete(string remoteFolder, string localFolder, string pathname)
            {
                string name = pathname.Substring(localFolder.Length + 1);
                string remoteName = Path.Combine(remoteFolder, name).Replace('\\', '/');
                DbTransaction transaction = null;
                try
                {
                    transaction = database.BeginTransaction();
                    // *** ContainsFiles
                    if (database.ContainsFile(SyncItemFactory.CreateFromLocalPath(pathname, repoinfo))) // FIXME remote or local?
                    {
                        Logger.Info("Removing locally deleted file on server: " + pathname);
                        try
                        {
                            IDocument remote = (IDocument)session.GetObjectByPath(remoteName);
                            if (remote != null)
                            {
                                remote.DeleteAllVersions();
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Warn(String.Format("Exception when operate remote {0}: ", remoteName), e);
                        }
                        // *** Remove File
                        database.RemoveFile(SyncItemFactory.CreateFromLocalPath(pathname, repoinfo));
                    }
                    // *** ContainsFolder
                    else if (database.ContainsFolder(pathname))
                    {
                        Logger.Info("Removing locally deleted folder on server: " + pathname);
                        try
                        {
                            IFolder remote = (IFolder)session.GetObjectByPath(remoteName);
                            if (remote != null)
                            {
                                remote.DeleteTree(true, null, true);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Warn(String.Format("Exception when operate remote {0}: ", remoteName), e);
                        }
                        // *** Remove File
                        database.RemoveFolder(SyncItemFactory.CreateFromLocalPath(pathname, repoinfo));
                    }
                    else
                    {
                        Logger.Info("Ignore the delete action for the local created and deleted file/folder: " + pathname);
                    }
                    transaction.Commit();
                }
                catch (Exception e)
                {
                    if (transaction != null)
                    {
                        transaction.Rollback();
                    }
                    Logger.Warn(String.Format("Exception while sync to delete file/folder {0}: ", pathname), e);
                    return;
                }
                finally
                {
                    if (transaction != null)
                    {
                        transaction.Dispose();
                    }
                }

                repo.Watcher.RemoveChange(pathname, Watcher.ChangeTypes.Deleted);

                return;
            }
        }
    }
}