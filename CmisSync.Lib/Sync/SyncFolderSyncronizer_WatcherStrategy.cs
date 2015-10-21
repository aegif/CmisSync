using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.Common;

using DotCMIS;
using DotCMIS.Client;
using CmisSync.Lib.Cmis;
using System.Threading;
using DotCMIS.Exceptions;
using CmisSync.Lib.Utils;
using System.Collections.Generic;


namespace CmisSync.Lib.Sync
{
    public partial class SyncFolderSyncronizer : SyncFolderSyncronizerBase
    {
        /// <summary>
        /// Synchronization based on local filesystem monitoring ("watcher").
        /// </summary>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="localFolder">Local folder.</param>
        /// <returns>Wether the sync has been a complete success or a partial failure (may need a subsequent crawl sync)</returns>
        private bool WatcherSync(string remoteFolder, string localFolder)
        {
            bool success = true;
            Logger.Debug("WatcherSync(" + remoteFolder + ", " + localFolder + ")");
            CheckPendingCancelation();
            LinkedList<FileSystemEventArgs> changes = watcher.GetChanges();
            
            if (Logger.IsDebugEnabled)
            {
                foreach (FileSystemEventArgs change in changes)
                {
                    if (change is CmisSync.Lib.Watcher.MovedEventArgs) Logger.DebugFormat("Moved: {0} -> {1}", ((CmisSync.Lib.Watcher.MovedEventArgs)change).OldFullPath, change.FullPath);
                    else if (change is RenamedEventArgs) Logger.DebugFormat("Renamed: {0} -> {1}", ((RenamedEventArgs)change).OldFullPath, change.FullPath);
                    else Logger.DebugFormat("{0}: {1}", change.ChangeType, change.FullPath);
                }
            }

            foreach(FileSystemEventArgs earliestChange in changes)
            {
                string pathname = earliestChange.FullPath;
                if (!pathname.StartsWith(localFolder))
                {
                    Logger.WarnFormat("Path {0} does not apply for target {1}.", pathname, localFolder);
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
                    success &= WatchSyncMove(remoteFolder, localFolder, change.OldFullPath, pathname);
                }
                else if (earliestChange is RenamedEventArgs)
                {
                    //Rename
                    RenamedEventArgs change = (RenamedEventArgs)earliestChange;
                    Logger.DebugFormat("Processing 'Renamed': {0} -> {1}.", change.OldFullPath, pathname);
                    success &= WatchSyncMove(remoteFolder, localFolder, change.OldFullPath, pathname);
                }
                else
                {
                    Logger.DebugFormat("Processing '{0}': {1}.", earliestChange.ChangeType, pathname);
                    switch (earliestChange.ChangeType)
                    {
                        case WatcherChangeTypes.Created:
                        case WatcherChangeTypes.Changed:
                            success &= WatcherSyncUpdate(remoteFolder, localFolder, pathname);
                            break;
                        case WatcherChangeTypes.Deleted:
                            success &= WatcherSyncDelete(remoteFolder, localFolder, pathname);
                            break;
                        default:
                            Logger.ErrorFormat("Invalid change -> '{0}': {1}.", earliestChange.ChangeType, pathname);
                            break;
                    }
                }
            }
            return success;
        }


        /// <summary>
        /// An event was received from the filesystem watcher, analyze the change and apply it.
        /// </summary>
        private bool WatchSyncMove(string remoteFolder, string localFolder, string oldPathname, string newPathname)
        {
            bool success = true;
            CheckPendingCancelation();
            string oldDirectory = Path.GetDirectoryName(oldPathname);
            string oldFilename = Path.GetFileName(oldPathname);
            string newDirectory = Path.GetDirectoryName(newPathname);
            string newFilename = Path.GetFileName(newPathname);
            bool rename = oldDirectory.Equals(newDirectory) && !oldFilename.Equals(newFilename);
            bool move = !oldDirectory.Equals(newDirectory) && oldFilename.Equals(newFilename);
            if ((rename && move) || (!rename && !move))
            {
                Logger.ErrorFormat("Not a valid rename/move: {0} -> {1}", oldPathname, newPathname);
                return true; // It is not our problem that watcher data is not valid.
            }
            try
            {
                bool oldPathnameWorthSyncing = SyncUtils.IsWorthSyncing(oldDirectory, oldFilename, SyncFolderInfo);
                bool newPathnameWorthSyncing = SyncUtils.IsWorthSyncing(newDirectory, newFilename, SyncFolderInfo);
                if (oldPathnameWorthSyncing && newPathnameWorthSyncing)
                {

                    string oldLocalName = SyncUtils.GetLocalRelativePath(oldPathname, localFolder);
                    string oldRemoteName = CmisPath.Combine(remoteFolder, PathRepresentationConverter.LocalToRemote(oldLocalName));
                    string oldRemoteBaseName = CmisPath.GetDirectoryName(oldRemoteName);
                    string newLocalName = SyncUtils.GetLocalRelativePath(newPathname, localFolder);
                    string newRemoteName = CmisPath.Combine(remoteFolder, PathRepresentationConverter.LocalToRemote(newLocalName));
                    string newRemoteBaseName = CmisPath.GetDirectoryName(newRemoteName);

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
            CheckPendingCancelation();
            string filename = Path.GetFileName(pathname);
            if (!SyncUtils.IsWorthSyncing(Path.GetDirectoryName(pathname), filename, SyncFolderInfo))
            {
                return true;
            }
            try
            {
                string relativeNamePath = SyncUtils.GetLocalRelativePath(pathname, localFolder);
                string remoteName = CmisPath.Combine(remoteFolder, PathRepresentationConverter.LocalToRemote(relativeNamePath));
                IFolder remoteBase = null;
                if (File.Exists(pathname) || Directory.Exists(pathname))
                {
                    string remoteBaseName = CmisPath.GetDirectoryName(remoteName);
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
        /// <param name="localNamePath">Pathname.</param>
        private bool WatcherSyncDelete(string remoteFolder, string localFolder, string localNamePath)
        {
            CheckPendingCancelation();

            //Note that with the continueOnFailure parameter set to true, folders and documents are deleted individually. If a document or folder cannot be deleted, the method moves to the next document or folder in the list. When the method completes, it returns a list of the document IDs and folder IDs that were not deleted.
            //With the continueOnFailure parameter set to false, all of the folders and documents can be deleted in a single batch, which, depending on the repository design, may improve performance. If a document or folder cannot be deleted, an exception is raised. Some repository implementations will attempt the delete transactionally, so if it fails, no objects are deleted. In other repositories a failed delete may have deleted some, but not all, objects in the tree.

            SyncItem item = getSyncItemFromLocalPath(localNamePath);
            
            IFolder folder;
            try
            {
                folder = tryGetObjectByPath(item.RemotePath);
            }
            catch (CmisObjectNotFoundException e)
            {
                Logger.Warn("Unable to locate the remote file or folder: " + item.RemotePath, e);
                return true;
            }
            try
            {
                IList<string> result = folder.DeleteTree(true, null, false);
                bool succes = result == null || result.Count == 0;

                if (succes)
                {
                    database.RemoveFolder(item);
                }
                return succes;
            }
            catch (Exception e)
            {
                HandleException(new DeleteRemoteFolderException(item.RemotePath, e));
                return false;
            }
        }
    }
}