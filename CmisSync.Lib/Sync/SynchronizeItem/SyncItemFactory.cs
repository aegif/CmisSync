using System;
using System.IO;
using CmisSync.Lib.Cmis;
using DotCMIS.Client;
using log4net;

namespace CmisSync.Lib.Sync.SynchronizeItem
{

    ///////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Factory to easily create SyncItem objects.
    /// </summary>
    public static class SyncItemFactory
    {
        // Log.
        static readonly ILog Logger = LogManager.GetLogger(typeof(SyncItemFactory));

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
            //Logger.Debug("CreateFromRemoteDocument remoteDocumentPath:" + remoteDocumentPath + " remoteDocument:" + remoteDocument + " repoInfo:" + repoInfo + " database:" + database);
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
    
}
