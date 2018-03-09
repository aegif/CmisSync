using System;
using System.IO;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Utilities.FileUtilities;
using DotCMIS;

namespace CmisSync.Lib.Sync.SynchronizeItem
{
    ///////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// SyncItem created from a remote file or folder.
    /// Its match might or might not exist yet on the local side.
    /// </summary>
    public class RemotePathSyncItem : SyncItem
    {
        /// <summary>
        /// Use only for folders, not for documents!
        /// </summary>
        /// <param name="remoteFolderPath">Example: /sites/aproject/adir</param>
        public RemotePathSyncItem (string remoteFolderPath, bool isFolder, RepoInfo repoInfo, Database.Database database)
        {
            this.isFolder = isFolder;
            this.database = database;
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.remoteRelativePath = remoteFolderPath;
            if (remoteRelativePath.StartsWith (this.remoteRoot)) {
                this.remoteRelativePath = remoteRelativePath.Substring (this.remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            }
        }


        /// <summary>
        /// Create from the path of a remote file, and the local filename to use.
        /// </summary>
        /// <param name="remoteRelativePath">Example: adir/a<file</param>
        /// <param name="localFilename">Example: afile.txt</param>
        public RemotePathSyncItem (string remoteRelativePath, string localFilename, RepoInfo repoInfo, Database.Database database)
        {
            this.isFolder = false;
            this.database = database;
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.remoteRelativePath = remoteRelativePath;
            if (remoteRelativePath.StartsWith (this.remoteRoot)) {
                this.remoteRelativePath = this.remoteRelativePath.Substring (localRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            }

            int lastSeparator = remoteRelativePath.LastIndexOf (CmisUtils.CMIS_FILE_SEPARATOR);
            string remoteRelativeFolder = lastSeparator >= 0 ?
                remoteRelativePath.Substring (0, lastSeparator)
                : String.Empty;
            string remoteRelativePathWithCorrectLeafname = CmisFileUtil.PathCombine (remoteRelativeFolder, localFilename);
            localRelativePath = database.RemoteToLocal (remoteRelativePathWithCorrectLeafname, isFolder);
        }


        public RemotePathSyncItem (string remoteFolderPath, string remoteDocumentName, string localFilename, bool isFolder, RepoInfo repoInfo, Database.Database database)
        {
            this.isFolder = isFolder;
            this.database = database;
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.remoteRelativePath = remoteFolderPath;
            if (remoteRelativePath.StartsWith (this.remoteRoot)) {
                this.remoteRelativePath = remoteRelativePath.Substring (this.remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            }
            this.localRelativePath = localFilename;
        }


        public override string LocalRelativePath {
            get {
                if (localRelativePath == null) {
                    localRelativePath = database.RemoteToLocal (RemoteRelativePath, isFolder);
                }
                return localRelativePath;
            }
        }


        public override string RemoteRelativePath {
            get {
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
                int separatorIndex = LocalRelativePath.LastIndexOf (Path.DirectorySeparatorChar);
                return LocalRelativePath.Substring (separatorIndex + 1); // +1 for the DirectorySeparatorChar
            }
        }


        public override string RemoteLeafname {
            get {
                return CmisFileUtil.GetLeafname (RemoteRelativePath);
            }
        }
    }
}
