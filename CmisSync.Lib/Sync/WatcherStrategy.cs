using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

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
                foreach (string pathname in repo.Watcher.GetChangeList())
                {
                    if (!Utils.WorthSyncing(pathname))
                    {
                        repo.Watcher.RemoveChange(pathname);
                        continue;
                    }

                    if (!pathname.StartsWith(localFolder))
                    {
                        Debug.Assert(false, String.Format("Invalid pathname {0} for target {1}.", pathname, localFolder));
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
                bool isFile = Directory.Exists(pathname) ? false : true;

                string name = pathname.Substring(localFolder.Length);
                //IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath);

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

                string name = pathname.Substring(localFolder.Length);
                string remoteName = Path.Combine(remoteFolder,name).Replace('\\','/');

                if (database.ContainsFile(pathname))
                {
                    Logger.Info("Removing locally deleted file on server: " + pathname);
                    IDocument remote = (IDocument)session.GetObjectByPath(remoteName);
                    if (remote != null)
                    {
                        remote.DeleteAllVersions();
                    }
                    database.RemoveFile(pathname);
                }
                else if (database.ContainsFolder(pathname))
                {
                    Logger.Info("Removing locally deleted folder on server: " + pathname);
                    IFolder remote = (IFolder)session.GetObjectByPath(remoteName);
                    if (remote != null)
                    {
                        remote.DeleteTree(true, null, true);
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
