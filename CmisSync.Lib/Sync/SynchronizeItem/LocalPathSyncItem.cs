using System;
using System.IO;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Utilities.FileUtilities;

namespace CmisSync.Lib.Sync.SynchronizeItem
{
    ///////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// SyncItem created from a local file or folder.
    /// Its match might or might not exist yet on the server side.
    /// </summary>
    public class LocalPathSyncItem : SyncItem
    {
        public LocalPathSyncItem (string localPath, bool isFolder, RepoInfo repoInfo, Database.Database database)
        {
            this.isFolder = isFolder;
            this.database = database;
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.localRelativePath = localPath;
            if (localPath.StartsWith (this.localRoot)) {
                this.localRelativePath = localPath.Substring (localRoot.Length).TrimStart (Path.DirectorySeparatorChar);
            }
        }


        public LocalPathSyncItem (string localPrefix, string localPath, string remotePrefix, string remotePath, bool isFolder)
        {
            this.localRoot = localPrefix;
            this.remoteRoot = remotePrefix;
            this.localRelativePath = localPath;
            this.remoteRelativePath = remotePath;
            this.isFolder = isFolder;
        }


        public override string LocalRelativePath {
            get {
                return localRelativePath;
            }
        }


        public override string RemoteRelativePath {
            get {
                if (remoteRelativePath == null) {
                    remoteRelativePath = database.LocalToRemote (LocalRelativePath, isFolder);
                }
                return remoteRelativePath;
            }
        }


        public override string LocalPath {
            get {
                return Path.Combine (localRoot, LocalRelativePath);
            }
        }


        public override string RemotePath {
            get {
                return CmisFileUtil.PathCombine (remoteRoot, RemoteRelativePath);
            }
        }


        public override string LocalLeafname {
            get {
                return Path.GetFileName (LocalRelativePath);
            }
        }


        public override string RemoteLeafname {
            get {
                return CmisFileUtil.GetLeafname (RemoteRelativePath);
            }
        }
    }
}
