using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using DotCMIS;
using DotCMIS.Client;


namespace CmisSync.Lib.Sync
{
    public partial class CmisRepo : RepoBase
    {
        /// <summary>
        /// Synchronization with a particular CMIS folder.
        /// </summary>
        public partial class SynchronizedFolder
        {
            private void WatcherSync(string remoteFolder, string localFolder)
            {
                Logger.Debug(remoteFolder + " : " + localFolder);
                foreach (string pathname in repo.Watcher.GetChangeList())
                {
                    string name = Path.GetFileName(pathname);
                    if (!Utils.WorthSyncing(name))
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
                    Logger.Debug(String.Format("Detect change {0} for {1}.", change, pathname));
                    switch (change)
                    {
                        case Watcher.ChangeTypes.Created:
                        case Watcher.ChangeTypes.Changed:
                            WatchSyncUpdate(remoteFolder, localFolder, pathname);
                            break;
                        case Watcher.ChangeTypes.Deleted:
                            WatchSyncDelete(remoteFolder, localFolder, pathname);
                            break;
                        default:
                            Debug.Assert(false, String.Format("Invalid change {0} for pathname {1}.", change, pathname));
                            break;
                    }
                }
            }

            private void WatchSyncUpdate(string remoteFolder, string localFolder, string pathname)
            {
                string name = pathname.Substring(localFolder.Length + 1);
                string remoteName = Path.Combine(remoteFolder, name).Replace('\\', '/');

                IFolder remoteBase = null;
                if (File.Exists(pathname) || Directory.Exists(pathname))
                {
                    string remoteBaseName = Path.GetDirectoryName(remoteName).Replace('\\', '/');
                    try
                    {
                        remoteBase = (IFolder)session.GetObjectByPath(remoteBaseName);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(String.Format("Exception when query remote {0}: {1}", remoteBaseName, Utils.ToLogString(ex)));
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

                if (File.Exists(pathname))
                {
                    bool success = false;
                    repo.Watcher.RemoveChange(pathname, Watcher.ChangeTypes.Created);
                    repo.Watcher.RemoveChange(pathname, Watcher.ChangeTypes.Changed);
                    if (database.ContainsFile(pathname))
                    {
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

                if (Directory.Exists(pathname))
                {
                    if (repoinfo.isPathIgnored(remoteName))
                    {
                        repo.Watcher.RemoveChange(pathname);
                        return;
                    }

                    if (database.ContainsFolder(pathname))
                    {
                        Logger.Info(String.Format("Database exists for {0}, ignore for the update action", pathname));
                        repo.Watcher.RemoveChange(pathname);
                    }
                    else
                    {
                        Dictionary<string, object> properties = new Dictionary<string, object>();
                        properties.Add(PropertyIds.Name, Path.GetFileName(pathname));
                        properties.Add(PropertyIds.ObjectTypeId, "cmis:folder");
                        if (null != remoteBase.CreateFolder(properties))
                        {
                            Logger.Info("Create locally created folder on server: " + pathname);
                            repo.Watcher.RemoveChange(pathname);
                        }
                        else
                        {
                            Logger.Warn("Failure to create locally created folder on server: " + pathname);
                        }
                    }
                    return;
                }

                Logger.Info(String.Format("The file/folder {0} is deleted, ignore for the update action", pathname));
            }

            private void WatchSyncDelete(string remoteFolder, string localFolder, string pathname)
            {
                if (Directory.Exists(pathname))
                {
                    Logger.Info(String.Format("A new folder {0} is created, ignore for the delete action", pathname));
                    return;
                }
                if (File.Exists(pathname))
                {
                    Logger.Info(String.Format("A new file {0} is created, ignore for the delete action", pathname));
                    return;
                }

                string name = pathname.Substring(localFolder.Length + 1);
                string remoteName = Path.Combine(remoteFolder,name).Replace('\\','/');

                if (database.ContainsFile(pathname))
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
                    catch (Exception ex)
                    {
                        Logger.Warn(String.Format("Exception when operate remote {0}: {1}", remoteName, Utils.ToLogString(ex)));
                    }
                    database.RemoveFile(pathname);
                }
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
                    catch (Exception ex)
                    {
                        Logger.Warn(String.Format("Exception when operate remote {0}: {1}", remoteName, Utils.ToLogString(ex)));
                    }
                    database.RemoveFolder(pathname);
                }
                else
                {
                    Logger.Info("Ignore the delete action for the local created and deleted file/folder: " + pathname);
                }

                repo.Watcher.RemoveChange(pathname, Watcher.ChangeTypes.Deleted);

                return;
            }
        }
    }
}
