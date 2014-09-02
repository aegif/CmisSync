using System;
using System.IO;
using CmisSync.Lib.Cmis;


namespace CmisSync.Lib
{
    abstract public class SyncItem
    {
        // local Repository root folder
        protected string localRoot;

        // remote Repository root folder
        protected string remoteRoot;

        // local relative path
        protected string localPath;

        // remote relative path
        protected string remotePath;


        protected SyncItem()
        {
        }

        abstract public string LocalRelativePath
        {
            get;
        }

        abstract public string RemoteRelativePath
        {
            get;
        }

        abstract public string LocalPath
        {
            get;
        }
       
        abstract public string RemotePath
        {
            get;
        }

        abstract public string LocalFileName
        {
            get;
        }

        abstract public string RemoteFileName
        {
            get;
        }

        virtual public bool ExistsLocal()
        {
            return File.Exists(LocalPath);
        }
    }


    public static class SyncItemFactory
    {

        public static SyncItem CreateFromLocalPath(string path, RepoInfo repoInfo)
        {
            return new LocalPathSyncItem(path, repoInfo);
        }

        public static SyncItem CreateFromLocalPath(string folder, string fileName, RepoInfo repoInfo)
        {
            return new LocalPathSyncItem(Path.Combine(folder, fileName), repoInfo);
        }

        public static SyncItem CreateFromRemotePath(string path, RepoInfo repoInfo)
        {
            return new RemotePathSyncItem(path, repoInfo);
        }
            
        public static SyncItem CreateFromRemotePath(string folder, string fileName, RepoInfo repoInfo)
        {
            return new RemotePathSyncItem(Path.Combine(folder, fileName), repoInfo);
        }

        public static SyncItem CreateFromLocalFolderAndRemoteName(string localFolder, string remoteFileName, RepoInfo repoInfo)
        {
            return new LocalPathSyncItem(localFolder, remoteFileName, repoInfo);
        }

        public static SyncItem CreateFromRemoteFolderAndLocalName(string remoteFolder, string LocalFileName, RepoInfo repoInfo)
        {
            return new RemotePathSyncItem(remoteFolder, LocalFileName, repoInfo);
        }

        public static SyncItem CreateFromPaths(string localPathPrefix, string localPath, string remotePathPrefix, string remotePath)
        {
            return new LocalPathSyncItem(localPathPrefix, localPath, remotePathPrefix, remotePath);
        }
    }
               

    public class LocalPathSyncItem : SyncItem
    {
        public LocalPathSyncItem(string localPath, RepoInfo repoInfo)
        {
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.localPath = localPath;
            if (localPath.StartsWith(this.localRoot))
            {
                this.localPath = localPath.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            this.remotePath = PathRepresentationConverter.LocalToRemote(this.localPath);
        }

        public LocalPathSyncItem(string localFolder, string remoteRelativePath, RepoInfo repoInfo)
        {
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.localPath = Path.Combine(localFolder, PathRepresentationConverter.RemoteToLocal(remoteRelativePath));
            if (localPath.StartsWith(this.localRoot))
            {
                this.localPath = localPath.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            string localRootRelative = localFolder;
            if (localFolder.StartsWith(this.localRoot))
            {
                localRootRelative = localFolder.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            this.remotePath = CmisUtils.PathCombine(PathRepresentationConverter.LocalToRemote(localRootRelative), remoteRelativePath);
        }

        public LocalPathSyncItem(string localPrefix, string localPath, string remotePrefix, string remotePath)
        {
            this.localRoot = localPrefix;
            this.remoteRoot = remotePrefix;
            this.localPath = localPath;
            this.remotePath = remotePath;
        }
            

        public override string LocalRelativePath
        {
            get
            {
                return localPath;
            }
        }

        public override string RemoteRelativePath
        {
            get
            {
                return remotePath;
            }
        }

        public override string LocalPath
        {
            get
            {
                return Path.Combine(this.localRoot, this.localPath);
            }
        }

        public override string RemotePath
        {
            get
            {
                return Path.Combine(this.remoteRoot, this.remotePath);
            }
        }

        public override string LocalFileName
        {
            get
            {
                return Path.GetFileName(this.localPath);
            }
        }

        public override string RemoteFileName
        {
            get
            {
                return Path.GetFileName(this.remotePath);
            }
        }
    }


    public class RemotePathSyncItem : SyncItem
    {
        public RemotePathSyncItem(string remotePath, RepoInfo repoInfo)
        {
            this.localRoot = PathRepresentationConverter.RemoteToLocal(repoInfo.TargetDirectory);
            this.remoteRoot = PathRepresentationConverter.LocalToRemote(repoInfo.RemotePath);

            this.remotePath = remotePath;
            if (remotePath.StartsWith(this.remoteRoot))
            {
                this.remotePath = remotePath.Substring(this.remoteRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            this.localPath = PathRepresentationConverter.RemoteToLocal(this.remotePath);
        }

        public RemotePathSyncItem(string remoteFolder, string localRelativePath, RepoInfo repoInfo)
        {
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.remotePath = Path.Combine(remoteFolder, PathRepresentationConverter.LocalToRemote(localRelativePath));
            if (this.remotePath.StartsWith(this.remoteRoot))
            {
                this.remotePath = this.remotePath.Substring(this.localRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            string remoteRootRelative = remoteFolder;
            if (remoteFolder.StartsWith(this.remoteRoot))
            {
                remoteRootRelative = remoteFolder.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            this.localPath = Path.Combine(PathRepresentationConverter.RemoteToLocal(remoteRootRelative), localRelativePath);
        }

        public override string LocalRelativePath
        {
            get
            {
                return localPath;
            }
        }

        public override string RemoteRelativePath
        {
            get
            {
                return remotePath;
            }
        }
            
        public override string LocalPath
        {
            get
            {
                return Path.Combine(localRoot, localPath);
            }
        }

        public override string RemotePath
        {
            get
            {
                return Path.Combine(remoteRoot, remotePath);
            }
        }

        public override string LocalFileName
        {
            get
            {
                return Path.GetFileName(localPath);
            }
        }

        public override string RemoteFileName
        {
            get
            {
                return Path.GetFileName(remotePath);
            }
        }
    }


    /// <summary>
    /// Path representation converter.
    /// </summary>
    public interface IPathRepresentationConverter
    {
        string LocalToRemote(string localPath);

        string RemoteToLocal(string remotePath);
    }


    public class DefaultPathRepresentationConverter : IPathRepresentationConverter
    {
        public string LocalToRemote(string localPath)
        {
            return localPath;
        }
        public string RemoteToLocal(string remotePath)
        {
            return remotePath;
        }
    }

    /// <summary>
    /// Path representation converter.
    /// </summary>
    public static class PathRepresentationConverter
    {
        private static IPathRepresentationConverter PathConverter = new DefaultPathRepresentationConverter();

        static public void SetConverter(IPathRepresentationConverter converter)
        {
            PathConverter = converter;
        }

        static public string LocalToRemote(string localPath)
        {
            return PathConverter.LocalToRemote(localPath);
        }

        static public string RemoteToLocal(string remotePath)
        {
            return PathConverter.RemoteToLocal(remotePath);
        }
    }
}

