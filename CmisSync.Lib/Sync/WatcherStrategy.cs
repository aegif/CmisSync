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
                SleepWhileSuspended();
                Queue<FileSystemEventArgs> changeQueue = repo.Watcher.GetChangeQueue();
                repo.Watcher.Clear();
                if (Logger.IsDebugEnabled)
                {
                    foreach (FileSystemEventArgs change in changeQueue)
                    {
                        if (change is CmisSync.Lib.Watcher.MovedEventArgs) Logger.DebugFormat("Moved: {0} -> {1}", ((CmisSync.Lib.Watcher.MovedEventArgs)change).OldFullPath, change.FullPath);
                        else if (change is RenamedEventArgs) Logger.DebugFormat("Renamed: {0} -> {1}", ((RenamedEventArgs)change).OldFullPath, change.FullPath);
                        else Logger.DebugFormat("{0}: {1}", change.ChangeType, change.FullPath);
                    }
                }
                while (changeQueue.Count > 0)
                {
                    activityListener.ActivityStarted();
                    FileSystemEventArgs earliestChange = changeQueue.Dequeue();
                    string pathname = earliestChange.FullPath;
                    if (!pathname.StartsWith(localFolder))
                    {
                        Logger.ErrorFormat("Invalid pathname {0} for target {1}.", pathname, localFolder);
                        return;
                    }
                    if (pathname == localFolder)
                    {
                        continue;
                    }
                    if (earliestChange is CmisSync.Lib.Watcher.MovedEventArgs)
                    {
                        //Move
                        CmisSync.Lib.Watcher.MovedEventArgs change = (CmisSync.Lib.Watcher.MovedEventArgs)earliestChange;
                        Logger.DebugFormat("Processing 'Moved': {0} -> {1}.", change.OldFullPath, pathname);
                        WatchSyncMove(remoteFolder, localFolder, change.OldFullPath, pathname);
                    }
                    else if (earliestChange is RenamedEventArgs)
                    {
                        //Rename
                        RenamedEventArgs change = (RenamedEventArgs)earliestChange;
                        Logger.DebugFormat("Processing 'Renamed': {0} -> {1}.", change.OldFullPath, pathname);
                        WatchSyncMove(remoteFolder, localFolder, change.OldFullPath, pathname);
                    }
                    else
                    {
                        Logger.DebugFormat("Processing '{0}': {1}.", earliestChange.ChangeType, pathname);
                        switch (earliestChange.ChangeType)
                        {
                            case WatcherChangeTypes.Created:
                            case WatcherChangeTypes.Changed:
                                WatcherSyncUpdate(remoteFolder, localFolder, pathname);
                                break;
                            case WatcherChangeTypes.Deleted:
                                WatcherSyncDelete(remoteFolder, localFolder, pathname);
                                break;
                            default:
                                Logger.ErrorFormat("Invalid change -> '{0}': {1}.", earliestChange.ChangeType, pathname);
                                break;
                        }
                    }
                    activityListener.ActivityStopped();
                }
            }


            /// <summary>
            /// Sync move file.
            /// </summary>
            private void WatchSyncMove(string remoteFolder, string localFolder, string oldPathname, string newPathname)
            {
                SleepWhileSuspended();
                string oldDirectory = Path.GetDirectoryName(oldPathname);
                string oldFilename = Path.GetFileName(oldPathname);
                string oldLocalName = oldPathname.Substring(localFolder.Length + 1);
                string oldRemoteName = Path.Combine(remoteFolder, oldLocalName).Replace('\\', '/'); // FIXME
                string oldRemoteBaseName = Path.GetDirectoryName(oldRemoteName).Replace('\\', '/');
                bool oldPathnameWorthSyncing = Utils.WorthSyncing(oldDirectory, oldFilename, repoinfo);
                string newDirectory = Path.GetDirectoryName(newPathname);
                string newFilename = Path.GetFileName(newPathname);
                string newLocalName = newPathname.Substring(localFolder.Length + 1);
                string newRemoteName = Path.Combine(remoteFolder, newLocalName).Replace('\\', '/');
                string newRemoteBaseName = Path.GetDirectoryName(newRemoteName).Replace('\\', '/');
                bool newPathnameWorthSyncing = Utils.WorthSyncing(newDirectory, newFilename, repoinfo);
                bool rename = oldDirectory.Equals(newDirectory) && !oldFilename.Equals(newFilename);
                bool move = !oldDirectory.Equals(newDirectory) && oldFilename.Equals(newFilename);
                if ((rename && move) || (!rename && !move))
                {
                    Logger.ErrorFormat("Not a valid rename/move: {0} -> {1}", oldPathname, newPathname);
                    return;
                }
                try
                {
                    if (oldPathnameWorthSyncing && newPathnameWorthSyncing)
                    {
                        if (database.ContainsFile(SyncItemFactory.CreateFromLocalPath(oldPathname, repoinfo)))
                        {
                            if (database.ContainsFile(SyncItemFactory.CreateFromLocalPath(newPathname, repoinfo)))
                            {
                                //database already contains path so revert back to delete/update
                                WatcherSyncDelete(remoteFolder, localFolder, oldPathname);
                                WatcherSyncUpdate(remoteFolder, localFolder, newPathname);
                            }
                            else
                            {
                                if (rename)
                                {
                                    //rename file...
                                    IDocument remoteDocument = (IDocument)session.GetObjectByPath(oldRemoteName);
                                    RenameFile(oldDirectory, newFilename, remoteDocument);
                                }
                                else //move
                                {
                                    //move file...
                                    IDocument remoteDocument = (IDocument)session.GetObjectByPath(oldRemoteName);
                                    IFolder oldRemoteFolder = (IFolder)session.GetObjectByPath(oldRemoteBaseName);
                                    IFolder newRemoteFolder = (IFolder)session.GetObjectByPath(newRemoteBaseName);
                                    MoveFile(oldDirectory, newDirectory, oldRemoteFolder, newRemoteFolder, remoteDocument);
                                }
                            }
                        }
                        else if (database.ContainsFolder(oldPathname))
                        {
                            if (database.ContainsFolder(newPathname))
                            {
                                //database already contains path so revert back to delete/update
                                WatcherSyncDelete(remoteFolder, localFolder, oldPathname);
                                WatcherSyncUpdate(remoteFolder, localFolder, newPathname);
                            }
                            else
                            {
                                if (rename)
                                {
                                    //rename folder...
                                    IFolder remoteFolderObject = (IFolder)session.GetObjectByPath(oldRemoteName);
                                    RenameFolder(oldDirectory, newFilename, remoteFolderObject);
                                }
                                else //move
                                {
                                    //move folder...
                                    IFolder remoteFolderObject = (IFolder)session.GetObjectByPath(oldRemoteName);
                                    IFolder oldRemoteFolder = (IFolder)session.GetObjectByPath(oldRemoteBaseName);
                                    IFolder newRemoteFolder = (IFolder)session.GetObjectByPath(newRemoteBaseName);
                                    MoveFolder(oldDirectory, newDirectory, oldRemoteFolder, newRemoteFolder, remoteFolderObject);
                                }
                            }
                        }
                        else
                        {
                            //File/Folder has not been synced before so simply update
                            WatcherSyncUpdate(remoteFolder, localFolder, newPathname);
                        }
                    }
                    else if (oldPathnameWorthSyncing && !newPathnameWorthSyncing)
                    {
                        //New path not worth syncing
                        WatcherSyncDelete(remoteFolder, localFolder, oldPathname);
                    }
                    else if (!oldPathnameWorthSyncing && newPathnameWorthSyncing)
                    {
                        //Old path not worth syncing
                        WatcherSyncUpdate(remoteFolder, localFolder, newPathname);
                    }
                    else
                    {
                        //Neither old or new path worth syncing
                    }
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could process watcher sync move: " + oldPathname + " -> " + newPathname, e);
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
                SleepWhileSuspended();
                string filename = Path.GetFileName(pathname);
                if (!Utils.WorthSyncing(Path.GetDirectoryName(pathname), filename, repoinfo))
                {
                    return;
                }
                try
                {
                    string name = pathname.Substring(localFolder.Length + 1);
                    string remoteName = Path.Combine(remoteFolder, name).Replace('\\', '/');
                    IFolder remoteBase = null;
                    if (File.Exists(pathname) || Directory.Exists(pathname))
                    {
                        string remoteBaseName = Path.GetDirectoryName(remoteName).Replace('\\', '/');
                        remoteBase = (IFolder)session.GetObjectByPath(remoteBaseName);
                        if (null == remoteBase)
                        {
                            Logger.WarnFormat("The remote base folder {0} for local {1} does not exist, ignore for the update action", remoteBaseName, pathname);
                            return;
                        }
                    }
                    else
                    {
                        Logger.InfoFormat("The file/folder {0} is deleted, ignore for the update action", pathname);
                        return;
                    }
                    if (File.Exists(pathname))
                    {
                        bool success = false;
                        if (database.ContainsFile(SyncItemFactory.CreateFromLocalPath(pathname, repoinfo)))
                        {
                            if (database.LocalFileHasChanged(pathname))
                            {
                                success = UpdateFile(pathname, remoteBase);
                                Logger.InfoFormat("Update {0}: {1}", pathname, success);
                            }
                            else
                            {
                                success = true;
                                Logger.InfoFormat("File {0} remains unchanged, ignore for the update action", pathname);
                            }
                        }
                        else
                        {
                            success = UploadFile(pathname, remoteBase);
                            Logger.InfoFormat("Upload {0}: {1}", pathname, success);
                        }
                        if (!success)
                        {
                            Logger.WarnFormat("Failure to update: {0}", pathname);
                        }
                        return;
                    }
                    if (Directory.Exists(pathname))
                    {
                        if (database.ContainsFolder(pathname))
                        {
                            Logger.InfoFormat("Folder exists in Database {0}, ignore for the update action", pathname);
                        }
                        else
                        {
                            Logger.InfoFormat("Create locally created folder on server: {0}", pathname);
                            UploadFolderRecursively(remoteBase, pathname);
                        }
                        return;
                    }
                    Logger.InfoFormat("The file/folder {0} is deleted, ignore for the update action", pathname);
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could process watcher sync update: " + pathname, e);
                }
            }

            /// <summary>
            /// Watchers the sync delete.
            /// </summary>
            /// <param name="remoteFolder">Remote folder.</param>
            /// <param name="localFolder">Local folder.</param>
            /// <param name="pathname">Pathname.</param>
            private void WatcherSyncDelete(string remoteFolder, string localFolder, string pathname)
            {
                SleepWhileSuspended();
                string filename = Path.GetFileName(pathname);
                if (!Utils.WorthSyncing(Path.GetDirectoryName(pathname), filename, repoinfo))
                {
                    return;
                }
                try
                {
                    string name = pathname.Substring(localFolder.Length + 1);
                    string remoteName = Path.Combine(remoteFolder, name).Replace('\\', '/'); // FIXME
                    if (database.ContainsFile(SyncItemFactory.CreateFromLocalPath(pathname, repoinfo)))
                    {
                        Logger.InfoFormat("Removing locally deleted file on server: {0}", pathname);
                        try
                        {
                            IDocument remote = (IDocument)session.GetObjectByPath(remoteName);
                            if (remote != null)
                            {
                                remote.DeleteAllVersions();
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(String.Format("Exception when operate remote {0}", remoteName), ex);
                        }
                        database.RemoveFile(SyncItemFactory.CreateFromLocalPath(pathname, repoinfo));
                    }
                    else if (database.ContainsFolder(pathname))
                    {
                        Logger.InfoFormat("Removing locally deleted folder on server: {0}", pathname);
                        try
                        {
                            IFolder remote = (IFolder)session.GetObjectByPath(remoteName);
                            if (remote != null)
                            {
                                remote.DeleteTree(true, null, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(String.Format("Exception when operate remote {0}", remoteName), ex);
                        }
                        database.RemoveFolder(SyncItemFactory.CreateFromLocalPath(pathname, repoinfo));
                    }
                    else
                    {
                        Logger.InfoFormat("Ignore the delete action for the local created and deleted file/folder: {0}", pathname);
                    }
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could process watcher sync update: " + pathname, e);
                }
            }
        }
    }
}