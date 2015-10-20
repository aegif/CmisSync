using System;
using System.IO;
using CmisSync.Lib.Cmis;


namespace CmisSync.Lib
{
    /// <summary></summary>
    abstract public class SyncItem
    {
        /// <summary>local Repository root folder</summary>
        protected string localRootPath;

        /// <summary>remote Repository root folder</summary>
        protected string remoteRootPath;

        /// <summary>local relative path</summary>
        protected string localRelativePath;

        /// <summary>remote relative path</summary>
        protected string remoteRelativePath;

        /// <summary></summary>
        protected SyncItem()
        {
        }

        /// <summary></summary>
        abstract public string LocalRelativePath
        {
            get;
        }

        /// <summary></summary>
        abstract public string RemoteRelativePath
        {
            get;
        }

        /// <summary></summary>
        abstract public string LocalPath
        {
            get;
        }

        /// <summary></summary>
        abstract public string RemotePath
        {
            get;
        }

        /// <summary></summary>
        abstract public string LocalFileName
        {
            get;
        }

        /// <summary></summary>
        abstract public string RemoteFileName
        {
            get;
        }

        /// <summary></summary>
        /// <returns></returns>
        virtual public bool ExistsLocal()
        {
            return File.Exists(LocalPath);
        }
    }

    /// <summary></summary>
    public static class SyncItemFactory
    {
        /// <summary></summary>
        /// <param name="path"></param>
        /// <param name="repoInfo"></param>
        /// <returns></returns>
        public static SyncItem CreateFromLocalPath(string path, Config.SyncConfig.SyncFolder repoInfo)
        {
            return new LocalPathSyncItem(path, repoInfo);
        }

        /// <summary></summary>
        /// <param name="folder"></param>
        /// <param name="fileName"></param>
        /// <param name="repoInfo"></param>
        /// <returns></returns>
        public static SyncItem CreateFromLocalPath(string folder, string fileName, Config.SyncConfig.SyncFolder repoInfo)
        {
            return new LocalPathSyncItem(Path.Combine(folder, fileName), repoInfo);
        }

        /// <summary></summary>
        /// <param name="path"></param>
        /// <param name="repoInfo"></param>
        /// <returns></returns>
        public static SyncItem CreateFromRemotePath(string path, Config.SyncConfig.SyncFolder repoInfo)
        {
            return new RemotePathSyncItem(path, repoInfo);
        }

        /// <summary></summary>
        /// <param name="folder"></param>
        /// <param name="fileName"></param>
        /// <param name="repoInfo"></param>
        /// <returns></returns>
        public static SyncItem CreateFromRemotePath(string folder, string fileName, Config.SyncConfig.SyncFolder repoInfo)
        {
            return new RemotePathSyncItem(Path.Combine(folder, fileName), repoInfo);
        }

        /// <summary></summary>
        /// <param name="localFolder"></param>
        /// <param name="remoteFileName"></param>
        /// <param name="repoInfo"></param>
        /// <returns></returns>
        public static SyncItem CreateFromLocalFolderAndRemoteName(string localFolder, string remoteFileName, Config.SyncConfig.SyncFolder repoInfo)
        {
            return new LocalPathSyncItem(localFolder, remoteFileName, repoInfo);
        }

        /// <summary></summary>
        /// <param name="remoteFolder"></param>
        /// <param name="LocalFileName"></param>
        /// <param name="repoInfo"></param>
        /// <returns></returns>
        public static SyncItem CreateFromRemoteFolderAndLocalName(string remoteFolder, string LocalFileName, Config.SyncConfig.SyncFolder repoInfo)
        {
            return new RemotePathSyncItem(remoteFolder, LocalFileName, repoInfo);
        }

        /// <summary></summary>
        /// <param name="localPathPrefix"></param>
        /// <param name="localPath"></param>
        /// <param name="remotePathPrefix"></param>
        /// <param name="remotePath"></param>
        /// <returns></returns>
        public static SyncItem CreateFromPaths(string localPathPrefix, string localPath, string remotePathPrefix, string remotePath)
        {
            return new LocalPathSyncItem(localPathPrefix, localPath, remotePathPrefix, remotePath);
        }
    }
               
    /// <summary></summary>
    public class LocalPathSyncItem : SyncItem
    {

        /// <summary></summary>
        /// <param name="localPath"></param>
        /// <param name="repoInfo"></param>
        public LocalPathSyncItem(string localPath, Config.SyncConfig.SyncFolder repoInfo)
        {
            this.localRootPath = repoInfo.LocalPath;
            this.remoteRootPath = repoInfo.RemotePath;

            this.localRelativePath = localPath;
            if (localPath.StartsWith(this.localRootPath))
            {
                this.localRelativePath = localPath.Substring(this.localRootPath.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            this.remoteRelativePath = PathRepresentationConverter.LocalToRemote(this.localRelativePath);
        }

        /// <summary></summary>
        /// <param name="localFolder"></param>
        /// <param name="fileName"></param>
        /// <param name="repoInfo"></param>
        public LocalPathSyncItem(string localFolder, string fileName, Config.SyncConfig.SyncFolder repoInfo)
        {
            if (!isValidFileName(fileName)) {
                throw new ArgumentException();
            }

            this.localRootPath = repoInfo.LocalPath;
            this.remoteRootPath = repoInfo.RemotePath;

            this.localRelativePath = Path.Combine(localFolder, fileName);
            if (localRelativePath.StartsWith(this.localRootPath))
            {
                this.localRelativePath = localRelativePath.Substring(this.localRootPath.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            this.remoteRelativePath = PathRepresentationConverter.LocalToRemote(this.localRelativePath);
        }

        private bool isValidFileName(string fileName)
        {
            return !string.IsNullOrEmpty(fileName) &&
              fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        /// <summary></summary>
        /// <param name="localPrefix"></param>
        /// <param name="localPath"></param>
        /// <param name="remotePrefix"></param>
        /// <param name="remotePath"></param>
        public LocalPathSyncItem(string localPrefix, string localPath, string remotePrefix, string remotePath)
        {
            this.localRootPath = localPrefix;
            this.remoteRootPath = remotePrefix;
            this.localRelativePath = localPath;
            this.remoteRelativePath = remotePath;
        }
            
        /// <summary></summary>
        public override string LocalRelativePath
        {
            get
            {
                return localRelativePath;
            }
        }

        /// <summary></summary>
        public override string RemoteRelativePath
        {
            get
            {
                return remoteRelativePath;
            }
        }

        /// <summary></summary>
        public override string LocalPath
        {
            get
            {
                return Path.Combine(this.localRootPath, this.localRelativePath);
            }
        }

        /// <summary></summary>
        public override string RemotePath
        {
            get
            {
                return CmisPath.Combine(this.remoteRootPath, this.remoteRelativePath);
            }
        }

        /// <summary></summary>
        public override string LocalFileName
        {
            get
            {
                return Path.GetFileName(this.localRelativePath);
            }
        }

        /// <summary></summary>
        public override string RemoteFileName
        {
            get
            {
                return CmisPath.GetFileName(this.remoteRelativePath);
            }
        }
    }

    /// <summary></summary>
    public class RemotePathSyncItem : SyncItem
    {
        /// <summary></summary>
        /// <param name="remotePath">either relative or absolute</param>
        /// <param name="syncFolderInfo"></param>
        public RemotePathSyncItem(string remotePath, Config.SyncConfig.SyncFolder syncFolderInfo)
        {
            this.localRootPath = syncFolderInfo.LocalPath;
            this.remoteRootPath = syncFolderInfo.RemotePath;

            this.remoteRelativePath = remotePath;
            if (remotePath.StartsWith(this.remoteRootPath))
            {
                this.remoteRelativePath = remotePath.Substring(this.remoteRootPath.Length).TrimStart(CmisPath.DirectorySeparatorChar);
            }
            this.localRelativePath = PathRepresentationConverter.RemoteToLocal(this.remoteRelativePath);
        }

        /// <summary></summary>
        /// <param name="remoteFolder"></param>
        /// <param name="fileName"></param>
        /// <param name="syncFolderInfo"></param>
        public RemotePathSyncItem(string remoteFolder, string fileName, Config.SyncConfig.SyncFolder syncFolderInfo)
        {
            if (!isValidFileName(fileName))
            {
                throw new ArgumentException();
            }

            this.localRootPath = syncFolderInfo.LocalPath;
            this.remoteRootPath = syncFolderInfo.RemotePath;

            this.remoteRelativePath = CmisPath.Combine(remoteFolder, fileName);
            if (this.remoteRelativePath.StartsWith(this.remoteRootPath))
            {
                this.remoteRelativePath = this.remoteRelativePath.Substring(this.localRootPath.Length).TrimStart(CmisPath.DirectorySeparatorChar);
            }
            this.localRelativePath = PathRepresentationConverter.RemoteToLocal(this.remoteRelativePath);
        }

        private bool isValidFileName(string fileName)
        {
            //TODO: does the server has other limitations about the file name?
            return !string.IsNullOrEmpty(fileName) &&
              fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        /// <summary></summary>
        public override string LocalRelativePath
        {
            get
            {
                return localRelativePath;
            }
        }

        /// <summary></summary>
        public override string RemoteRelativePath
        {
            get
            {
                return remoteRelativePath;
            }
        }
        
        /// <summary></summary>
        public override string LocalPath
        {
            get
            {
                return Path.Combine(localRootPath, localRelativePath);
            }
        }

        /// <summary></summary>
        public override string RemotePath
        {
            get
            {
                return Path.Combine(remoteRootPath, remoteRelativePath);
            }
        }

        /// <summary></summary>
        public override string LocalFileName
        {
            get
            {
                return Path.GetFileName(localRelativePath);
            }
        }

        /// <summary></summary>
        public override string RemoteFileName
        {
            get
            {
                return Path.GetFileName(remoteRelativePath);
            }
        }
    }

    /// <summary>Path representation converter.</summary>
    public interface IPathRepresentationConverter
    {
        /// <summary></summary>
        /// <param name="localPath"></param>
        /// <returns></returns>
        string LocalToRemote(string localPath);

        /// <summary></summary>
        /// <param name="remotePath"></param>
        /// <returns></returns>
        string RemoteToLocal(string remotePath);
    }

    /// <summary></summary>
    public class DefaultPathRepresentationConverter : IPathRepresentationConverter
    {
        /// <summary></summary>
        /// <param name="localPath"></param>
        /// <returns></returns>
        public string LocalToRemote(string localPath)
        {
            if(Path.IsPathRooted(localPath)){
                throw new ArgumentException("Can convert only a relative path");
            }
            return localPath.Replace(Path.DirectorySeparatorChar, CmisPath.DirectorySeparatorChar);
            //TODO: any other differences?
        }

        /// <summary></summary>
        /// <param name="remotePath"></param>
        /// <returns></returns>
        public string RemoteToLocal(string remotePath)
        {
            if (CmisPath.IsPathRooted(remotePath))
            {
                throw new ArgumentException("Can convert only a relative path");
            }
            return remotePath.Replace(CmisPath.DirectorySeparatorChar, Path.DirectorySeparatorChar);
            //TODO: any other differences?
        }
    }

    /// <summary>Path representation converter.</summary>
    public static class PathRepresentationConverter
    {
        private static IPathRepresentationConverter PathConverter = new DefaultPathRepresentationConverter();

        /// <summary></summary>
        /// <param name="converter"></param>
        static public void SetConverter(IPathRepresentationConverter converter)
        {
            PathConverter = converter;
        }

        /// <summary></summary>
        /// <param name="localPath"></param>
        /// <returns></returns>
        static public string LocalToRemote(string localPath)
        {
            return PathConverter.LocalToRemote(localPath);
        }

        /// <summary></summary>
        /// <param name="remotePath"></param>
        /// <returns></returns>
        static public string RemoteToLocal(string remotePath)
        {
            return PathConverter.RemoteToLocal(remotePath);
        }
    }
}

