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
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(SyncItem));

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
        /// Example: C:\Users\nico\CmisSync\A Project
        /// </summary>
        protected string localRoot;

        /// <summary>
        /// Remote root of the collection.
        /// Example: /sites/aproject
        /// </summary>
        protected string remoteRoot;

        /// <summary>
        /// Local path of the item, relative to the local root
        /// Example: adir\afile.txt
        /// </summary>
        protected string localRelativePath;

        /// <summary>
        /// Remote path of the item, relative to the remote root
        /// Example: adir/a<file
        /// </summary>
        protected string remoteRelativePath;

        /// <summary>
        /// Whether the item is a folder or a file.
        /// </summary>
        protected bool isFolder;
        public bool IsFolder
        {
            get
            {
                return isFolder;
            }
        }

        /// <summary>
        /// Reference to the CmisSync database.
        /// It is useful to get the remote path that matches a local path, or vice versa
        /// </summary>
        protected Database.Database database;

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
        abstract public string LocalLeafname
        {
            get;
        }

        /// <summary></summary>
        abstract public string RemoteLeafname
        {
            get;
        }

        /// <summary>
        /// Whether the file exists locally.
        /// </summary>
        virtual public bool FileExistsLocal()
        {
            bool exists = File.Exists(LocalPath);
            Logger.Debug("File.Exists(" + LocalPath + ") = " + exists);
            return exists;
        }
    }


    ///////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Factory to easily create SyncItem objects.
    /// </summary>
    public static class SyncItemFactory
    {
        public static SyncItem CreateFromLocalPath(string path, bool isFolder, RepoInfo repoInfo, Database.Database database)
        {
            return new LocalPathSyncItem(path, isFolder, repoInfo, database);
        }


        public static SyncItem CreateFromLocalPath(string folder, string fileName, bool isFolder, RepoInfo repoInfo, Database.Database database)
        {
            return new LocalPathSyncItem(Path.Combine(folder, fileName), isFolder, repoInfo, database);
        }

        /// <summary>
        /// Only use for documents!
        /// </summary>
        public static SyncItem CreateFromRemoteDocument(string remoteDocumentPath, string localFilename, RepoInfo repoInfo, Database.Database database)
        {
            string remoteFolderPath = remoteDocumentPath.Substring(0, remoteDocumentPath.LastIndexOf(CmisUtils.CMIS_FILE_SEPARATOR));
            string remoteDocumentName = remoteDocumentPath.Substring(remoteDocumentPath.LastIndexOf(CmisUtils.CMIS_FILE_SEPARATOR) + 1); // 1 is the length of CMIS_FILE_SEPARATOR as it is a character

            RemotePathSyncItem item = new RemotePathSyncItem(remoteFolderPath, remoteDocumentName, localFilename, false, repoInfo, database);
            return item;
        }

        /// <summary>
        /// Only use for documents!
        /// </summary>
        public static SyncItem CreateFromRemoteDocument(string remoteDocumentPath, IDocument remoteDocument, RepoInfo repoInfo, Database.Database database)
        {
            string remoteFolderPath = remoteDocumentPath.Substring(0, remoteDocumentPath.LastIndexOf(CmisUtils.CMIS_FILE_SEPARATOR));
            string remoteRoot = repoInfo.RemotePath;
            string relativeRemoteDocumentPath = remoteDocumentPath.Substring(remoteRoot.Length).TrimStart(CmisUtils.CMIS_FILE_SEPARATOR);

            string remoteDocumentName = repoInfo.CmisProfile.localFilename(remoteDocument);

            RemotePathSyncItem item = new RemotePathSyncItem(relativeRemoteDocumentPath, remoteDocumentName, repoInfo, database);
            return item;
        }

        /// <summary>
        /// Create sync item from the path of a remote folder.
        /// </summary>
        /// <param name="remoteFolderPath">Example: /sites/aproject/adir</param>
        public static SyncItem CreateFromRemoteFolder(string remoteFolderPath, RepoInfo repoInfo, Database.Database database)
        {
            return new RemotePathSyncItem(remoteFolderPath, true, repoInfo, database);
        }

        /// <summary>
        /// Specify all local and remote paths.
        /// That's the only case where a database is not needed as no conversions are needed.
        /// </summary>
        public static SyncItem CreateFromPaths(string localPathPrefix, string localPath, string remotePathPrefix, string remotePath, bool isFolder)
        {
            return new LocalPathSyncItem(localPathPrefix, localPath, remotePathPrefix, remotePath, isFolder);
        }
    }
    

    ///////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// SyncItem created from a local file or folder.
    /// Its match might or might not exist yet on the server side.
    /// </summary>
    public class LocalPathSyncItem : SyncItem
    {
        public LocalPathSyncItem(string localPath, bool isFolder, RepoInfo repoInfo, Database.Database database)
        {
            this.isFolder = isFolder;
            this.database = database;
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.localRelativePath = localPath;
            if (localPath.StartsWith(this.localRoot))
            {
                this.localRelativePath = localPath.Substring(localRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
        }


        public LocalPathSyncItem(string localPrefix, string localPath, string remotePrefix, string remotePath, bool isFolder)
        {
            this.localRoot = localPrefix;
            this.remoteRoot = remotePrefix;
            this.localRelativePath = localPath;
            this.remoteRelativePath = remotePath;
            this.isFolder = isFolder;
        }
            
        
        public override string LocalRelativePath
        {
            get
            {
                return localRelativePath;
            }
        }

        
        public override string RemoteRelativePath
        {
            get
            {
                if (remoteRelativePath == null)
                {
                    remoteRelativePath = database.LocalToRemote(LocalRelativePath, isFolder);
                }
                return remoteRelativePath;
            }
        }

        
        public override string LocalPath
        {
            get
            {
                return Path.Combine(localRoot, LocalRelativePath);
            }
        }

        
        public override string RemotePath
        {
            get
            {
                return CmisUtils.PathCombine(remoteRoot, RemoteRelativePath);
            }
        }

        
        public override string LocalLeafname
        {
            get
            {
                return Path.GetFileName(LocalRelativePath);
            }
        }

        
        public override string RemoteLeafname
        {
            get
            {
                return CmisUtils.GetLeafname(RemoteRelativePath);
            }
        }
    }


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
        public RemotePathSyncItem(string remoteFolderPath, bool isFolder, RepoInfo repoInfo, Database.Database database)
        {
            this.isFolder = isFolder;
            this.database = database;
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.remoteRelativePath = remoteFolderPath;
            if (remoteRelativePath.StartsWith(this.remoteRoot))
            {
                this.remoteRelativePath = remoteRelativePath.Substring(this.remoteRoot.Length).TrimStart(CmisUtils.CMIS_FILE_SEPARATOR);
            }
        }


        /// <summary>
        /// Create from the path of a remote file, and the local filename to use.
        /// </summary>
        /// <param name="remoteRelativePath">Example: adir/a<file</param>
        /// <param name="localFilename">Example: afile.txt</param>
        public RemotePathSyncItem(string remoteRelativePath, string localFilename, RepoInfo repoInfo, Database.Database database)
        {
            this.isFolder = false;
            this.database = database;
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.remoteRelativePath = remoteRelativePath;
            if (remoteRelativePath.StartsWith(this.remoteRoot))
            {
                this.remoteRelativePath = this.remoteRelativePath.Substring(localRoot.Length).TrimStart(CmisUtils.CMIS_FILE_SEPARATOR);
            }

            int lastSeparator = remoteRelativePath.LastIndexOf(CmisUtils.CMIS_FILE_SEPARATOR);
            string remoteRelativeFolder = lastSeparator >= 0 ?
                remoteRelativePath.Substring(0, lastSeparator)
                : String.Empty;
            string remoteRelativePathWithCorrectLeafname = CmisUtils.PathCombine(remoteRelativeFolder, localFilename);
            localRelativePath = database.RemoteToLocal(remoteRelativePathWithCorrectLeafname, isFolder);
        }


        public RemotePathSyncItem(string remoteFolderPath, string remoteDocumentName, string localFilename, bool isFolder, RepoInfo repoInfo, Database.Database database)
        {
            this.isFolder = isFolder;
            this.database = database;
            this.localRoot = repoInfo.TargetDirectory;
            this.remoteRoot = repoInfo.RemotePath;

            this.remoteRelativePath = remoteFolderPath;
            if (remoteRelativePath.StartsWith(this.remoteRoot))
            {
                this.remoteRelativePath = remoteRelativePath.Substring(this.remoteRoot.Length).TrimStart(CmisUtils.CMIS_FILE_SEPARATOR);
            }
            this.localRelativePath = localFilename;
        }


        public override string LocalRelativePath
        {
            get
            {
                if (localRelativePath == null)
                {
                    localRelativePath = database.RemoteToLocal(RemoteRelativePath, isFolder);
                }
                return localRelativePath;
            }
        }

        
        public override string RemoteRelativePath
        {
            get
            {
                return remoteRelativePath;
            }
        }
        
        
        public override string LocalPath
        {
            get
            {
                return Path.Combine(localRoot, LocalRelativePath);
            }
        }

        
        public override string RemotePath
        {
            get
            {
                return CmisUtils.PathCombine(remoteRoot , RemoteRelativePath);
            }
        }

        
        public override string LocalLeafname
        {
            get
            {
                return LocalRelativePath.Substring(LocalRelativePath.LastIndexOf(Path.DirectorySeparatorChar) + 1); // 1 for the DirectorySeparatorChar
            }
        }

        
        public override string RemoteLeafname
        {
            get
            {
                return CmisUtils.GetLeafname(RemoteRelativePath);
            }
        }
    }

    ///////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Converter between local path representation to remote path representation.
    /// Example:
    ///  - Remote: aproject/adir/a<file
    ///  - Local: A Project\adir\afile.txt
    /// </summary>
    public interface IPathRepresentationConverter
    {
        string LocalToRemote(string localPath);

        string RemoteToLocal(string remotePath);
    }

    /// <summary>
    /// Identity converter.
    /// </summary>
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

    /// <summary>Path representation converter.</summary>
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

