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
using DotCMIS.Exceptions;


namespace CmisSync.Lib.Sync
{
    public partial class SyncFolderSyncronizer : SyncFolderSyncronizerBase
    {
        /// <summary>
        /// Synchronization based on local filesystem monitoring ("watcher").
        /// </summary>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="localFolder">Local folder.</param>
        private void WatcherSync(string remoteFolder, string localFolder)
        {
            Logger.Debug("WatcherSync(" + remoteFolder + ", " + localFolder + ")");
            SleepWhileSuspended();
            Queue<FileSystemEventArgs> changeQueue = watcher.GetChangeQueue();
            this.watcher.Clear();
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

                FileSystemEventArgs earliestChange = changeQueue.Dequeue();
                string pathname = earliestChange.FullPath;
                if (!pathname.StartsWith(localFolder))
                {
                    Logger.ErrorFormat("Invalid pathname {0} for target {1}.", pathname, localFolder);
                    continue;
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
            }
        }


        /// <summary>
        /// An event was received from the filesystem watcher, analyze the change and apply it.
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
            bool oldPathnameWorthSyncing = SyncUtils.IsWorthSyncing(oldDirectory, oldFilename, SyncFolderInfo);
            string newDirectory = Path.GetDirectoryName(newPathname);
            string newFilename = Path.GetFileName(newPathname);
            string newLocalName = newPathname.Substring(localFolder.Length + 1);
            string newRemoteName = Path.Combine(remoteFolder, newLocalName).Replace('\\', '/');
            string newRemoteBaseName = Path.GetDirectoryName(newRemoteName).Replace('\\', '/');
            bool newPathnameWorthSyncing = SyncUtils.IsWorthSyncing(newDirectory, newFilename, SyncFolderInfo);
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
                    if (database.ContainsFile(SyncItemFactory.CreateFromLocalPath(oldPathname, SyncFolderInfo)))
                    {
                        if (database.ContainsFile(SyncItemFactory.CreateFromLocalPath(newPathname, SyncFolderInfo)))
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
                                IDocument remoteDocument = (IDocument)getSession().GetObjectByPath(oldRemoteName);
                                success &= RenameRemoteFile(oldDirectory, newFilename, remoteDocument);
                            }
                            else //move
                            {
                                //move file...
                                IDocument remoteDocument = (IDocument)getSession().GetObjectByPath(oldRemoteName);
                                IFolder oldRemoteFolder = (IFolder)getSession().GetObjectByPath(oldRemoteBaseName);
                                IFolder newRemoteFolder = (IFolder)getSession().GetObjectByPath(newRemoteBaseName);
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
                                IFolder remoteFolderObject = (IFolder)getSession().GetObjectByPath(oldRemoteName);
                                success &= RenameRemoteFolder(oldDirectory, newFilename, remoteFolderObject);
                            }
                            else //move
                            {
                                //move folder...
                                IFolder remoteFolderObject = (IFolder)getSession().GetObjectByPath(oldRemoteName);
                                IFolder oldRemoteFolder = (IFolder)getSession().GetObjectByPath(oldRemoteBaseName);
                                IFolder newRemoteFolder = (IFolder)getSession().GetObjectByPath(newRemoteBaseName);
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
                if(rename){
                    HandleException(new RenameRemoteFolderException(oldPathname, newPathname, e));
                }
            }
            return success;
        }


        /// <summary>
        /// Sync update.
        /// </summary>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="localFolder">Local folder.</param>
        /// <param name="pathname">Pathname.</param>
        private bool WatcherSyncUpdate(string remoteFolder, string localFolder, string pathname)
        {
            SleepWhileSuspended();
            string filename = Path.GetFileName(pathname);
            if (!SyncUtils.IsWorthSyncing(Path.GetDirectoryName(pathname), filename, SyncFolderInfo))
            {
                return true;
            }
            try
            {
                //FIXME
                string name = pathname.Substring(localFolder.Length + 1);
                string remoteName = Path.Combine(remoteFolder, name).Replace('\\', '/');
                IFolder remoteBase = null;
                if (File.Exists(pathname) || Directory.Exists(pathname))
                {
                    string remoteBaseName = Path.GetDirectoryName(remoteName).Replace('\\', '/');
                    remoteBase = (IFolder)getSession().GetObjectByPath(remoteBaseName);
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
                    if (database.ContainsFile(SyncItemFactory.CreateFromLocalPath(pathname, SyncFolderInfo)))
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
                HandleException(new UploadFileException(pathname, e));
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