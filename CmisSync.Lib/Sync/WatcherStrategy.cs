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
using System.Threading;


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
            /// Synchronization based on local filesystem monitoring ("watcher").
            /// </summary>
            /// <param name="remoteFolder">Remote folder.</param>
            /// <param name="localFolder">Local folder.</param>
            /// <returns>Whether something has changed in the local folder</returns>
            private bool WatcherSync(string remoteFolder, string localFolder)
            {
                Logger.Debug(remoteFolder + " : " + localFolder);
                bool locallyModified = false;
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
                        Logger.DebugFormat("Path {0} does not apply for target {1}.", pathname, localFolder);
                        activityListener.ActivityStopped();
                        continue;
                    }
                    if (pathname == localFolder)
                    {
                        continue;
                    }
                    if (earliestChange is CmisSync.Lib.Watcher.MovedEventArgs)
                    {
                        // Move
                        CmisSync.Lib.Watcher.MovedEventArgs change = (CmisSync.Lib.Watcher.MovedEventArgs)earliestChange;
                        Logger.DebugFormat("Processing 'Moved': {0} -> {1}.", change.OldFullPath, pathname);
                        bool done = WatchSyncMove(remoteFolder, localFolder, change.OldFullPath, pathname);
                        locallyModified |= !done;
                    }
                    else if (earliestChange is RenamedEventArgs)
                    {
                        // Rename
                        RenamedEventArgs change = (RenamedEventArgs)earliestChange;
                        Logger.DebugFormat("Processing 'Renamed': {0} -> {1}.", change.OldFullPath, pathname);
                        bool done = WatchSyncMove(remoteFolder, localFolder, change.OldFullPath, pathname);
                        locallyModified |= !done;
                    }
                    else
                    {
                        Logger.DebugFormat("Processing '{0}': {1}.", earliestChange.ChangeType, pathname);
                        switch (earliestChange.ChangeType)
                        {
                            case WatcherChangeTypes.Created:
                            case WatcherChangeTypes.Changed:
                                bool done = WatcherSyncUpdate(remoteFolder, localFolder, pathname);
                                locallyModified |= !done;
                                break;
                            case WatcherChangeTypes.Deleted:
                                done = WatcherSyncDelete(remoteFolder, localFolder, pathname);
                                locallyModified |= !done;
                                break;
                            default:
                                Logger.ErrorFormat("Ignoring change with unhandled type -> '{0}': {1}.", earliestChange.ChangeType, pathname);
                                break;
                        }
                    }
                    activityListener.ActivityStopped();
                }
                return locallyModified;
            }


            /// <summary>
            /// An event was received from the filesystem watcher, analyze the change and apply it.
            /// <returns>Whether the move has now been synchronized, so that no further action is needed</returns>
            /// </summary>
            private bool WatchSyncMove(string remoteFolder, string localFolder, string oldPathname, string newPathname)
            {
                bool success = true;
                SleepWhileSuspended();
                string oldDirectory = Path.GetDirectoryName(oldPathname);
                string oldFilename = Path.GetFileName(oldPathname);
                string oldLocalName = oldPathname.Substring(localFolder.Length + 1);
                string oldRemoteName = Path.Combine(remoteFolder, oldLocalName).Replace('\\', '/'); // FIXME
                string oldRemoteBaseName = Path.GetDirectoryName(oldRemoteName).Replace('\\', '/');
                bool oldPathnameWorthSyncing = Utils.WorthSyncing(oldDirectory, oldFilename, repoInfo);
                string newDirectory = Path.GetDirectoryName(newPathname);
                string newFilename = Path.GetFileName(newPathname);
                string newLocalName = newPathname.Substring(localFolder.Length + 1);
                string newRemoteName = Path.Combine(remoteFolder, newLocalName).Replace('\\', '/');
                string newRemoteBaseName = Path.GetDirectoryName(newRemoteName).Replace('\\', '/');
                bool newPathnameWorthSyncing = Utils.WorthSyncing(newDirectory, newFilename, repoInfo);
                bool rename = oldDirectory.Equals(newDirectory) && !oldFilename.Equals(newFilename);
                bool move = !oldDirectory.Equals(newDirectory) && oldFilename.Equals(newFilename);
                if ((rename && move) || (!rename && !move))
                {
                    Logger.ErrorFormat("Not a valid rename/move: {0} -> {1}", oldPathname, newPathname);
                    return true; // It is not our problem that watcher data is not valid.
                }
                try
                {
                    if (oldPathnameWorthSyncing && newPathnameWorthSyncing)
                    {
                        if (database.ContainsLocalFile(oldPathname))
                        {
                            if (database.ContainsLocalFile(newPathname))
                            {
                                //database already contains path so revert back to delete/update
                                success &= WatcherSyncDelete(remoteFolder, localFolder, oldPathname);
                                success &= WatcherSyncUpdate(remoteFolder, localFolder, newPathname);
                            }
                            else
                            {
                                if (rename)
                                {
                                    //rename file...
                                    IDocument remoteDocument = (IDocument)session.GetObjectByPath(oldRemoteName);
                                    success &= RenameFile(oldDirectory, newFilename, remoteDocument);
                                }
                                else //move
                                {
                                    //move file...
                                    IDocument remoteDocument = (IDocument)session.GetObjectByPath(oldRemoteName);
                                    IFolder oldRemoteFolder = (IFolder)session.GetObjectByPath(oldRemoteBaseName);
                                    IFolder newRemoteFolder = (IFolder)session.GetObjectByPath(newRemoteBaseName);
                                    success &= MoveFile(oldDirectory, newDirectory, oldRemoteFolder, newRemoteFolder, remoteDocument);
                                }
                            }
                        }
                        else if (database.ContainsFolder(oldPathname))
                        {
                            if (database.ContainsFolder(newPathname))
                            {
                                //database already contains path so revert back to delete/update
                                success &= WatcherSyncDelete(remoteFolder, localFolder, oldPathname);
                                success &= WatcherSyncUpdate(remoteFolder, localFolder, newPathname);
                            }
                            else
                            {
                                if (rename)
                                {
                                    //rename folder...
                                    IFolder remoteFolderObject = (IFolder)session.GetObjectByPath(oldRemoteName);
                                    success &= RenameFolder(oldDirectory, newFilename, remoteFolderObject);
                                }
                                else //move
                                {
                                    //move folder...
                                    IFolder remoteFolderObject = (IFolder)session.GetObjectByPath(oldRemoteName);
                                    IFolder oldRemoteFolder = (IFolder)session.GetObjectByPath(oldRemoteBaseName);
                                    IFolder newRemoteFolder = (IFolder)session.GetObjectByPath(newRemoteBaseName);
                                    success &= MoveFolder(oldDirectory, newDirectory, oldRemoteFolder, newRemoteFolder, remoteFolderObject);
                                }
                            }
                        }
                        else
                        {
                            //File/Folder has not been synced before so simply update
                            success &= WatcherSyncUpdate(remoteFolder, localFolder, newPathname);
                        }
                    }
                    else if (oldPathnameWorthSyncing && !newPathnameWorthSyncing)
                    {
                        //New path not worth syncing
                        success &= WatcherSyncDelete(remoteFolder, localFolder, oldPathname);
                    }
                    else if (!oldPathnameWorthSyncing && newPathnameWorthSyncing)
                    {
                        //Old path not worth syncing
                        success &= WatcherSyncUpdate(remoteFolder, localFolder, newPathname);
                    }
                    else
                    {
                        //Neither old or new path worth syncing
                    }
                }
                catch (Exception e)
                {
                    success = false;
                    ProcessRecoverableException("Could process watcher sync move: " + oldPathname + " -> " + newPathname, e);
                }
                return success;
            }


            /// <summary>
            /// Sync update.
            /// </summary>
            /// <param name="remoteFolder">Remote folder.</param>
            /// <param name="localFolder">Local folder.</param>
            /// <param name="pathname">Pathname.</param>
            /// <returns>Whether the update has now been synchronized, so that no further action is needed</returns>
            private bool WatcherSyncUpdate(string remoteFolder, string localFolder, string pathname)
            {
                SleepWhileSuspended();
                string filename = Path.GetFileName(pathname);
                if (!Utils.WorthSyncing(Path.GetDirectoryName(pathname), filename, repoInfo))
                {
                    return true;
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
                            return true; // Ignore is not a failure.
                        }
                    }
                    else
                    {
                        Logger.InfoFormat("The file/folder {0} is deleted, ignore for the update action", pathname);
                        return true; 
                    }
                    if (File.Exists(pathname))
                    {
                        bool success = false;
                        if (database.ContainsLocalFile(pathname))
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
                        if (success)
                        {
                            return true;
                        }
                        else
                        {
                            Logger.WarnFormat("Failure to update: {0}", pathname);
                            return false;
                        }
                    }
                    if (Directory.Exists(pathname))
                    {
                        bool success = true;
                        if (database.ContainsFolder(pathname))
                        {
                            Logger.InfoFormat("Folder exists in Database {0}, ignore for the update action", pathname);
                        }
                        else
                        {
                            Logger.InfoFormat("Create locally created folder on server: {0}", pathname);
                            success &= UploadFolderRecursively(remoteBase, pathname);
                        }
                        return success;
                    }
                    Logger.InfoFormat("The file/folder {0} is deleted, ignore for the update action", pathname);
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could process watcher sync update: " + pathname, e);
                    return false;
                }
                return true;
            }

            /// <summary>
            /// Watchers the sync delete.
            /// </summary>
            /// <param name="remoteFolder">Remote folder.</param>
            /// <param name="localFolder">Local folder.</param>
            /// <param name="pathname">Pathname.</param>
            /// <returns>Whether the delete has now been synchronized, so that no further action is needed</returns>
            private bool WatcherSyncDelete(string remoteFolder, string localFolder, string pathname)
            {
                SleepWhileSuspended();

                // In many programs (like Microsoft Word), deletion is often just a save:
                // 1. Save data to temporary file ~wrdxxxx.tmp
                // 2. Delete Example.doc
                // 3. Rename ~wrdxxxx.tmp to Example.doc
                // See https://support.microsoft.com/en-us/kb/211632
                // So, upon deletion, wait a bit for any save operation to hopefully finalize, then sync.
                // This is not 100% foolproof, as saving can last for more than GRACE_TIME, but probably
                // the best we can do without mind-reading third-party programs.
                int GRACE_TIME = 15000; // 15 seconds.
                Thread.Sleep(GRACE_TIME);
                return false; // Perform a sync.
            }
        }
    }
}