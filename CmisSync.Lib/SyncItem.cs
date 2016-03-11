using System;
using System.IO;
using CmisSync.Lib.Cmis;
using log4net;
using DotCMIS.Client;
using CmisSync.Lib.Database;


namespace CmisSync.Lib
{
    /// <summary></summary>
    abstract public class SyncItem
    {
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SyncItem));

        // The examples below are for this item:
        //
        // Local: C:\Users\nico\CmisSync\A Project\adir\afile.txt
        // Remote: /sites/aproject/adir/a<file
        //
        // Notice how:
        // - Slashes and antislashes can differ
        // - File names can differ
        // - Remote and local have different sets of fobidden characters
        //
        // For that reason, never convert a local path to a remote path (or vice versa) without checking the database.

        /// <summary>
        /// Local root of the collection.
        /// Example: C:\Users\nico\CmisSync\myproject
        /// </summary>
        protected string localRoot;

        /// <summary>
        /// Remote root of the collection.
        /// Example: 
        /// </summary>
        protected string remoteRoot;

        /// <summary>
        /// Local path of the item, relative to the local root
        /// Example: mydir\myfile.txt
        /// </summary>
        protected string localPath;

        /// <summary>
        /// Remote path of the item, relative to the remote root
        /// Example: 
        /// </summary>
        public string remotePath;

        /// <summary>
        /// Reference to the CmisSync database.
        /// It is useful to get the remote path that matches a local path, or vice versa
        /// </summary>
        protected Database.Database database;

        /// <summary></summary>
        protected SyncItem() // TODO remove, does not seem to be used
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
            bool exists = File.Exists(LocalPath);
            Logger.Debug("File.Exists(" + LocalPath + ") = " + exists);
            return exists;
        }
    }

    /// <summary></summary>
    public static class SyncItemFactory
    {
        /// <summary></summary>
        /// <param name="path"></param>
        /// <param name="repoInfo"></param>
        /// <returns></returns>
        public static SyncItem CreateFromLocalPath(string path, RepoInfo repoInfo)
        {
            return new LocalPathSyncItem(path, repoInfo);
        }

        /// <summary></summary>
        /// <param name="folder"></param>
        /// <param name="fileName"></param>
        /// <param name="repoInfo"></param>
        /// <returns></returns>
        public static SyncItem CreateFromLocalPath(string folder, string fileName, RepoInfo repoInfo)
        {
            return new LocalPathSyncItem(Path.Combine(folder, fileName), repoInfo);
        }

        /// <summary>
        /// Only use for documents!
        /// </summary>
        public static SyncItem CreateFromRemoteDocument(string remoteDocumentPath, string localFilename, RepoInfo repoInfo)
        {
            string localRoot = PathRepresentationConverter.RemoteToLocal(repoInfo.TargetDirectory); //FIXME
            string remoteRoot = PathRepresentationConverter.LocalToRemote(repoInfo.RemotePath); // FIXME

            string remoteFolderPath = remoteDocumentPath.Substring(0, remoteDocumentPath.LastIndexOf(CmisUtils.CMIS_FILE_SEPARATOR));
            string remoteDocumentName = remoteDocumentPath.Substring(remoteDocumentPath.LastIndexOf(CmisUtils.CMIS_FILE_SEPARATOR));

            RemotePathSyncItem item = new RemotePathSyncItem(remoteFolderPath, remoteDocumentName, localFilename, repoInfo);
            return item;
        }

        public static SyncItem CreateFromRemoteFolder(string path, RepoInfo repoInfo)
        {
            return new RemotePathSyncItem(path, repoInfo); // FIXME create that definition when others fixed
        }

        /// <summary></summary>
        /// <param name="folder"></param>
        /// <param name="fileName"></param>
        /// <param name="repoInfo"></param>
        /// <returns></returns>
        /*public static SyncItem CreateFromRemotePath(string folder, string fileName, RepoInfo repoInfo)
        {
            return new RemotePathSyncItem(Path.Combine(folder, fileName), document, repoInfo); // FIXME
        }*/

        /// <summary></summary>
        /// <param name="localFolder"></param>
        /// <param name="remoteFileName"></param>
        /// <param name="repoInfo"></param>
        /// <returns></returns>
        public static SyncItem CreateFromLocalFolderAndRemoteName(string localFolder, string remoteFileName, RepoInfo repoInfo)
        {
            return new LocalPathSyncItem(localFolder, remoteFileName, repoInfo);
        }

        /// <summary></summary>
        /// <param name="remoteFolder"></param>
        /// <param name="LocalFileName"></param>
        /// <param name="repoInfo"></param>
        /// <returns></returns>
        public static SyncItem CreateFromRemoteFolderAndLocalName(string remoteFolder, string LocalFileName, RepoInfo repoInfo)
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
        public LocalPathSyncItem(string localPath, RepoInfo repoInfo)
        {
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.localPath = localPath;
            if (localPath.StartsWith(this.localRoot))
            {
                this.localPath = localPath.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
        }

        /// <summary></summary>
        /// <param name="localFolder"></param>
        /// <param name="remoteRelativePath"></param>
        /// <param name="repoInfo"></param>
        public LocalPathSyncItem(string localFolder, string remoteRelativePath, RepoInfo repoInfo)
        {
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.localPath = Path.Combine(localFolder, PathRepresentationConverter.RemoteToLocal(remoteRelativePath)); // FIXME
            if (localPath.StartsWith(this.localRoot))
            {
                this.localPath = localPath.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            string localRootRelative = localFolder;
            if (localFolder.StartsWith(this.localRoot))
            {
                localRootRelative = localFolder.Substring(localRoot.Length).TrimStart(CmisUtils.CMIS_FILE_SEPARATOR);
            }
            this.remotePath = CmisUtils.PathCombine(PathRepresentationConverter.LocalToRemote(localRootRelative), remoteRelativePath); // FIXME
        }

        /// <summary></summary>
        /// <param name="localPrefix"></param>
        /// <param name="localPath"></param>
        /// <param name="remotePrefix"></param>
        /// <param name="remotePath"></param>
        public LocalPathSyncItem(string localPrefix, string localPath, string remotePrefix, string remotePath, Database.Database database)
        {
            this.localRoot = localPrefix;
            this.remoteRoot = remotePrefix;
            this.localPath = localPath;
            this.remotePath = remotePath;
        }
            
        /// <summary></summary>
        public override string LocalRelativePath
        {
            get
            {
                return localPath;
            }
        }

        /// <summary></summary>
        public override string RemoteRelativePath
        {
            get
            {
                // Get the remote path from database, as it can be different from the name path
                database.GetRemoteFilePath

                // If not present in database, it means it is a new locally-created file, so PathRepresentationConverter.LocalToRemote is OK.
                this.remotePath = PathRepresentationConverter.LocalToRemote(this.localPath);
                //return remotePath;
            }
        }

        /// <summary></summary>
        public override string LocalPath
        {
            get
            {
                return Path.Combine(this.localRoot, this.localPath);
            }
        }

        /// <summary></summary>
        public override string RemotePath
        {
            get
            {
                return Path.Combine(this.remoteRoot, this.remotePath);
            }
        }

        /// <summary></summary>
        public override string LocalFileName
        {
            get
            {
                return Path.GetFileName(this.localPath);
            }
        }

        /// <summary></summary>
        public override string RemoteFileName
        {
            get
            {
                return Path.GetFileName(this.remotePath);
            }
        }
    }

    /// <summary></summary>
    public class RemotePathSyncItem : SyncItem
    {
        /// <summary>
        /// Use only for folders, not for documents!
        /// </summary>
        public RemotePathSyncItem(string remoteFolderPath, RepoInfo repoInfo)
        {
            this.localRoot = PathRepresentationConverter.RemoteToLocal(repoInfo.TargetDirectory);
            this.remoteRoot = PathRepresentationConverter.LocalToRemote(repoInfo.RemotePath);

            this.remotePath = remoteFolderPath;
            if (remotePath.StartsWith(this.remoteRoot))
            {
                this.remotePath = remotePath.Substring(this.remoteRoot.Length).TrimStart(CmisUtils.CMIS_FILE_SEPARATOR);
            }
            this.localPath = PathRepresentationConverter.RemoteToLocal(this.remotePath); // does not work with contentStreamFilename
        }

        /// <summary></summary>
        /// <param name="remoteFolder"></param>
        /// <param name="localRelativePath"></param>
        /// <param name="repoInfo"></param>
        public RemotePathSyncItem(string remoteFolder, string localRelativePath, RepoInfo repoInfo)
        {
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.remotePath = Path.Combine(remoteFolder, PathRepresentationConverter.LocalToRemote(localRelativePath));
            if (this.remotePath.StartsWith(this.remoteRoot))
            {
                this.remotePath = this.remotePath.Substring(this.remoteRoot.Length).TrimStart(CmisUtils.CMIS_FILE_SEPARATOR);
            }
            string remoteRootRelative = remoteFolder;
            if (remoteFolder.StartsWith(this.remoteRoot))
            {
                remoteRootRelative = remoteFolder.Substring(localRoot.Length).TrimStart(CmisUtils.CMIS_FILE_SEPARATOR);
            }
            this.localPath = Path.Combine(PathRepresentationConverter.RemoteToLocal(remoteRootRelative), localRelativePath);
        }

        public RemotePathSyncItem(string remoteFolderPath, string remoteDocumentName, string localFilename, RepoInfo repoInfo)
        {
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.remotePath = remoteFolderPath;
            if (remotePath.StartsWith(this.remoteRoot))
            {
                this.remotePath = remotePath.Substring(this.remoteRoot.Length).TrimStart(CmisUtils.CMIS_FILE_SEPARATOR);
            }
            this.localPath = localFilename;
        }

        /// <summary></summary>
        public override string LocalRelativePath
        {
            get
            {
                return localPath;
            }
        }

        /// <summary></summary>
        public override string RemoteRelativePath
        {
            get
            {
                return remotePath;
            }
        }
        
        /// <summary></summary>
        public override string LocalPath
        {
            get
            {
                return Utils.PathCombine(localRoot, localPath);
            }
        }

        /// <summary></summary>
        public override string RemotePath
        {
            get
            {
                return Path.Combine(remoteRoot, remotePath);
            }
        }

        /// <summary></summary>
        public override string LocalFileName
        {
            get
            {
                return Path.GetFileName(localPath);
            }
        }

        /// <summary></summary>
        public override string RemoteFileName
        {
            get
            {
                return Path.GetFileName(remotePath);
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
            return localPath;
        }

        /// <summary></summary>
        /// <param name="remotePath"></param>
        /// <returns></returns>
        public string RemoteToLocal(string remotePath)
        {
            return remotePath;
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

