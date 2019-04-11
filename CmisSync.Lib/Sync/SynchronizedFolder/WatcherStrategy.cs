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
using CmisSync.Lib.Utilities.FileUtilities;
using System.Threading;

using CmisSync.Lib.Sync.SynchronizeItem;

namespace CmisSync.Lib.Sync.CmisRepoFolder
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
        /// <returns>Whether any local change has now been synchronized, so that no further action is needed</returns>
        private bool WatcherSync(string remoteFolder, string localFolder)
        {
            Logger.Debug(remoteFolder + " : " + localFolder);
            bool success = true;
            SleepWhileSuspended();
            Queue<WatcherEvent> changeQueue = repo.Watcher.GetChangeQueue();
            repo.Watcher.Clear();
            if (Logger.IsDebugEnabled)
            {
                foreach (WatcherEvent change in changeQueue)
                {
                    if (change.GetFileSystemEventArgs() is CmisSync.Lib.Watcher.MovedEventArgs)
                    {
                        Logger.DebugFormat("Moved: {0} -> {1}",
                            ((CmisSync.Lib.Watcher.MovedEventArgs)change.GetFileSystemEventArgs()).OldFullPath,
                            change.GetFileSystemEventArgs().FullPath);
                    }
                    else if (change.GetFileSystemEventArgs() is RenamedEventArgs)
                    {
                        Logger.DebugFormat("Renamed: {0} -> {1}",
                            ((RenamedEventArgs)change.GetFileSystemEventArgs()).OldFullPath,
                            change.GetFileSystemEventArgs().FullPath);
                    }
                    else
                    {
                        Logger.DebugFormat("{0}: {1}",
                            change.GetFileSystemEventArgs().ChangeType,
                            change.GetFileSystemEventArgs().FullPath);
                    }
                }
            }
            while (changeQueue.Count > 0)
            {
                activityListener.ActivityStarted();
                try
                {
                WatcherEvent earliestChange = changeQueue.Dequeue();
                string pathname = earliestChange.GetFileSystemEventArgs().FullPath;
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
                if (earliestChange.GetFileSystemEventArgs() is CmisSync.Lib.Watcher.MovedEventArgs)
                {
                    // Move
                    CmisSync.Lib.Watcher.MovedEventArgs change =
                        (CmisSync.Lib.Watcher.MovedEventArgs)earliestChange.GetFileSystemEventArgs();
                    Logger.DebugFormat("Processing 'Moved': {0} -> {1}.", change.OldFullPath, pathname);
                    success &= WatchSyncMove(remoteFolder, localFolder, change.OldFullPath, pathname, earliestChange.GetGrace());
                }
                else if (earliestChange.GetFileSystemEventArgs() is RenamedEventArgs)
                {
                    // Rename
                    RenamedEventArgs change =
                        (RenamedEventArgs)earliestChange.GetFileSystemEventArgs();
                    Logger.DebugFormat("Processing 'Renamed': {0} -> {1}.", change.OldFullPath, pathname);
                    success &= WatchSyncMove(remoteFolder, localFolder, change.OldFullPath, pathname, earliestChange.GetGrace());
                }
                else
                {
                    Logger.DebugFormat("Processing '{0}': {1}.",
                        earliestChange.GetFileSystemEventArgs().ChangeType, pathname);
                    switch (earliestChange.GetFileSystemEventArgs().ChangeType)
                    {
                        case WatcherChangeTypes.Created:
                        case WatcherChangeTypes.Changed:
                            success &= WatcherSyncUpdate(remoteFolder, localFolder, pathname);
                            break;
                        case WatcherChangeTypes.Deleted:
                            success &= WatcherSyncDelete(remoteFolder, localFolder, pathname, earliestChange.GetGrace());
                            break;
                        default:
                            Logger.ErrorFormat("Ignoring change with unhandled type -> '{0}': {1}.",
                                earliestChange.GetFileSystemEventArgs().ChangeType, pathname);
                            break;
                    }
                }
                }
                catch(Exception ex)
                {
                    success = false;
                }
                activityListener.ActivityStopped();
            }
            return success;
        }


        /// <summary>
        /// An event was received from the filesystem watcher, analyze the change and apply it.
        /// <returns>Whether the move has now been synchronized, so that no further action is needed</returns>
        /// </summary>
        private bool WatchSyncMove(string remoteFolder, string localFolder, string oldPathname, string newPathname, Grace grace)
        {
            bool success = true;
            SleepWhileSuspended();

            try
            {
            // Old item.
            string oldDirectory = Path.GetDirectoryName(oldPathname);
            string oldFilename = Path.GetFileName(oldPathname);
            string oldLocalName = oldPathname.Substring(localFolder.Length + 1);
            SyncItem oldItem = database.GetSyncItemFromLocalPath(oldPathname);
                if (oldItem == null)
                {
                    // The change is about a file which was not in database yet, we can't move it. Further action is needed.
                    return false;
                }
            string oldRemoteName = oldItem.RemotePath;
            string oldRemoteBaseName = CmisFileUtil.GetUpperFolderOfCmisPath(oldRemoteName);
            bool oldPathnameWorthSyncing = Utils.WorthSyncing(oldDirectory, oldFilename, repoInfo);

            // New item.
            bool isFolder = Utils.IsFolder(newPathname);
            string newDirectory = Path.GetDirectoryName(newPathname); // TODO do this only if isFolder is true, modify rest of the logic accordingly.
            string newFilename = Path.GetFileName(newPathname);
            string newLocalName = newPathname.Substring(localFolder.Length + 1);
            SyncItem newItem = SyncItemFactory.CreateFromLocalPath(newPathname, isFolder, repoInfo, database);
            string newRemoteName = newItem.RemotePath;
                string newRemoteBaseName = CmisFileUtil.GetUpperFolderOfCmisPath(newRemoteName);
            bool newPathnameWorthSyncing = Utils.WorthSyncing(newDirectory, newFilename, repoInfo);

            // Operations.
            bool rename = oldDirectory.Equals(newDirectory) && !oldFilename.Equals(newFilename);
            bool move = !oldDirectory.Equals(newDirectory) && oldFilename.Equals(newFilename);
            if ((rename && move) || (!rename && !move))
            {
                Logger.ErrorFormat("Not a valid rename/move: {0} -> {1}", oldPathname, newPathname);
                return true; // It is not our problem that watcher data is not valid.
            }
                if (oldPathnameWorthSyncing && newPathnameWorthSyncing)
                {
                    if (database.ContainsLocalFile(oldPathname))
                    {
                        if (database.ContainsLocalFile(newPathname))
                        {
                            //database already contains path so revert back to delete/update
                            success &= WatcherSyncDelete(remoteFolder, localFolder, oldPathname, grace);
                            success &= WatcherSyncUpdate(remoteFolder, localFolder, newPathname);
                        }
                        else
                        {
                            if (rename)
                            {
                                //rename file...
                                IDocument remoteDocument = (IDocument)session.GetObjectByPath(oldRemoteName, true);
                                success &= RenameFile(oldDirectory, newFilename, remoteDocument);
                            }
                            else //move
                            {
                                //move file...
                                IDocument remoteDocument = (IDocument)session.GetObjectByPath(oldRemoteName, true);
                                IFolder oldRemoteFolder = (IFolder)session.GetObjectByPath(oldRemoteBaseName, true);
                                IFolder newRemoteFolder = (IFolder)session.GetObjectByPath(newRemoteBaseName, true);
                                success &= MoveFile(oldDirectory, newDirectory, oldRemoteFolder, newRemoteFolder, remoteDocument);
                            }
                        }
                    }
                    else if (database.ContainsFolder(oldPathname))
                    {
                        if (database.ContainsFolder(newPathname))
                        {
                            //database already contains path so revert back to delete/update
                            success &= WatcherSyncDelete(remoteFolder, localFolder, oldPathname, grace);
                            success &= WatcherSyncUpdate(remoteFolder, localFolder, newPathname);
                        }
                        else
                        {
                            if (rename)
                            {
                                //rename folder...
                                IFolder remoteFolderObject = (IFolder)session.GetObjectByPath(oldRemoteName, true);
                                success &= RenameFolder(oldDirectory, newFilename, remoteFolderObject);
                            }
                            else //move
                            {
                                //move folder...
                                IFolder remoteFolderObject = (IFolder)session.GetObjectByPath(oldRemoteName, true);
                                IFolder oldRemoteFolder = (IFolder)session.GetObjectByPath(oldRemoteBaseName, true);
                                IFolder newRemoteFolder = (IFolder)session.GetObjectByPath(newRemoteBaseName, true);
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
                    success &= WatcherSyncDelete(remoteFolder, localFolder, oldPathname, grace);
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
            catch (FileNotFoundException e)
            {
                success = false;
                Logger.Warn("Could process watcher sync move, file or folder not found: " + oldPathname + " -> " + newPathname, e);
            }
            catch (Exception e)
            {
                success = false;
                ProcessRecoverableException("Could not process watcher sync move: " + oldPathname + " -> " + newPathname, e);
            }
            return success;
        }


        /// <summary>
        /// Sync update.
        /// </summary>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="localFolder">Local folder.</param>
        /// <param name="localPath">Pathname.</param>
        /// <returns>Whether the update has now been synchronized, so that no further action is needed</returns>
        private bool WatcherSyncUpdate(string remoteFolder, string localFolder, string localPath)
        {
            SleepWhileSuspended();
            string localFilename = Path.GetFileName(localPath);
            if (!Utils.WorthSyncing(Path.GetDirectoryName(localPath), localFilename, repoInfo))
            {
                return true;
            }
            try
            {

                // Get the remote directory, needed by the update method.
                IFolder remoteBase = null;
                if (File.Exists(localPath) || Directory.Exists(localPath))
                {
                    bool isFolder = Utils.IsFolder(localPath);
                    SyncItem item = SyncItemFactory.CreateFromLocalPath(localPath, isFolder, repoInfo, database);
                    string remoteBaseName = CmisFileUtil.GetUpperFolderOfCmisPath(item.RemotePath);
                    remoteBase = (IFolder)session.GetObjectByPath(remoteBaseName, true);
                    if (null == remoteBase)
                    {
                        Logger.WarnFormat("The remote base folder {0} for local {1} does not exist, ignore for the update action", remoteBaseName, localPath);
                        return true; // Ignore is not a failure.
                    }
                }
                else
                {
                    Logger.InfoFormat("The file/folder {0} is deleted, ignore for the update action", localPath);
                    return true; 
                }

                // Update the item.
                if (File.Exists(localPath))
                {
                    // The item is a file.
                    bool success = false;
                    if (database.ContainsLocalFile(localPath))
                    {
                        if (database.LocalFileHasChanged(localPath))
                        {
                            success = UpdateFile(localPath, remoteBase);
                            Logger.InfoFormat("Update {0}: {1}", localPath, success);
                        }
                        else
                        {
                            success = true;
                            Logger.InfoFormat("File {0} remains unchanged, ignore for the update action", localPath);
                        }
                    }
                    else
                    {
                        success = UploadFile(localPath, remoteBase);
                        Logger.InfoFormat("Upload {0}: {1}", localPath, success);
                    }
                    if (success)
                    {
                        return true;
                    }
                    else
                    {
                        Logger.WarnFormat("Failure to update: {0}", localPath);
                        return false;
                    }
                }
                if (Directory.Exists(localPath))
                {
                    // The item is a folder.
                    bool success = true;
                    if (database.ContainsFolder(localPath))
                    {
                        Logger.InfoFormat("Folder exists in Database {0}, ignore for the update action", localPath);
                    }
                    else
                    {
                        Logger.InfoFormat("Create locally created folder on server: {0}", localPath);
                        success &= UploadFolderRecursively(remoteBase, localPath);
                    }
                    return success;
                }
                Logger.InfoFormat("The file/folder {0} is deleted, ignore for the update action", localPath);
            }
            catch (Exception e)
            {
                ProcessRecoverableException("Could process watcher sync update: " + localPath, e);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Process a detected deletion.
        /// </summary>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="localFolder">Local folder.</param>
        /// <param name="pathname">Pathname.</param>
        /// <returns>Whether the delete has now been synchronized, so that no further action is needed</returns>
        private bool WatcherSyncDelete(string remoteFolder, string localFolder, string pathname, Grace grace)
        {
            SleepWhileSuspended();

            // In many programs (like Microsoft Word), deletion is often just a save:
            // 1. Save data to temporary file ~wrdxxxx.tmp
            // 2. Delete Example.doc
            // 3. Rename ~wrdxxxx.tmp to Example.doc
            // See https://support.microsoft.com/en-us/kb/211632
            // So, upon deletion, wait a bit for any save operation to hopefully finalize, then sync.
            // This is not 100% foolproof, as saving can last for more than the grace time, but probably
            // the best we can do without mind-reading third-party programs.
            grace.WaitGraceTime();

            return false; // Perform a sync.
        }
    }

}
