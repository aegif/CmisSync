using System;
using System.IO;
using CmisSync.Lib.Sync.SyncTriplet.TripletItem;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync.CmisSyncFolder;
using CmisSync.Lib.Sync.SyncRepo;
using CmisSync.Lib.Utilities.FileUtilities;
using DotCMIS.Client;
using log4net;

namespace CmisSync.Lib.Sync.SyncTriplet
{

    /// <summary>
    /// Sync triplet factory. A factory utility class for creating synctriplets SFPs for synctriplets.
    /// 
    /// <para>Note:</para>
    /// <para>  An SFP is a Semi-Finished-Product of a synctriplet. It is common in local crawler where remote 
    ///   storage's status is unknown. In such case, an SFP of synctriplet created by the local crawler will
    ///   be &lt;LS, DB, ?&gt; and be pushed to semi-product queue for assembler to fill the ? with remote storage
    ///   information.</para>
    /// </summary>
    public static class SyncTripletFactory
    {
        /// <summary>
        /// The logger.
        /// </summary>
        //static ILog logger = LogManager.GetLogger (typeof (SyncTripletFactory));

        private const bool RemoteToLocal = true;
        private const bool LocalToRemote = false;
        private const bool IsFolder = true;
        private const bool IsDocument = false;

        /// <summary>
        /// Creates the SFP from remote document. Given remote folder where the document is.
        /// Therefore it is not necessary to check the document's folder.
        /// </summary>
        /// <returns>The SFP from remote document.</returns>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="remoteDocument">Remote document.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncTriplet CreateSFPFromRemoteDocument (
            IFolder remoteFolder,
            IDocument remoteDocument,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            SyncTriplet res = new SyncTriplet (IsDocument);

            // Create remote storage
            String remoteRoot = cmisSyncFolder.RemotePath;
            String remoteFull = CmisFileUtil.PathCombine (remoteFolder.Path, CmisFileUtil.GetLocalFileName (remoteDocument, cmisSyncFolder.CmisProfile));
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteDocument);

            // Check database
            res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, res.RemoteStorage.RelativePath, IsDocument, RemoteToLocal);

            // Create local storage 
            res.LocalStorage = null;

            res.Name = remoteRelative;
            return res;
        }

        /// <summary>
        /// Creates the SFP from remote document, given document only.
        /// Folder path should be checked among all possible paths to select the one
        /// start with CmisSyncFolder.RemotePath by SyncFileUtil.GetApplicablePath
        /// </summary>
        /// <returns>The SFP from remote document.</returns>
        /// <param name="remoteDocument">Remote document.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncTriplet CreateSFPFromRemoteDocument(
            IDocument remoteDocument,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder) 
        {
            SyncTriplet res = new SyncTriplet (IsDocument);

            String remoteRoot = cmisSyncFolder.RemotePath;
            String remoteFull = CmisFileUtil.PathCombine(
                SyncFileUtil.GetApplicablePath (remoteDocument, cmisSyncFolder), 
                CmisFileUtil.GetLocalFileName(remoteDocument, cmisSyncFolder.CmisProfile));
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteDocument);

            // Check database
            res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, res.RemoteStorage.RelativePath, IsDocument, RemoteToLocal);

            // Create local storage 
            res.LocalStorage = null;

            res.Name = remoteRelative;
            return res;
        }

        /// <summary>
        /// Create full synctriplet by remote document. It is useful when creating triplet from changelog
        /// </summary>
        /// <returns>The synctriplet created by remote document.</returns>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="remoteDocument">Remote document.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncTriplet CreateFromRemoteDocument(
            IFolder remoteFolder,
            IDocument remoteDocument,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            SyncTriplet res = CreateSFPFromRemoteDocument (remoteFolder, remoteDocument, cmisSyncFolder);
            // Create local storage 
            res.LocalStorage = (null == res.DBStorage.DBLocalPath) ? null : new LocalStorageItem(cmisSyncFolder.LocalPath, res.DBStorage.DBLocalPath);

            return res;
        }

        public static SyncTriplet CreateFromRemoteDocument(
            IDocument remoteDocumnt,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder) 
        {
            SyncTriplet res = CreateSFPFromRemoteDocument (remoteDocumnt, cmisSyncFolder);
            // Create local storage 
            res.LocalStorage = (null == res.DBStorage.DBLocalPath) ? null : new LocalStorageItem (cmisSyncFolder.LocalPath, res.DBStorage.DBLocalPath);

            return res;
        }


        /// <summary>
        /// Creates the SFP from a remote folder.
        /// </summary>
        /// <returns>The SFP of a remote folder.</returns>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncTriplet CreateSFPFromRemoteFolder (
            IFolder remoteFolder,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {

            SyncTriplet res = new SyncTriplet (IsFolder);

            String remoteRoot = cmisSyncFolder.RemotePath;
            String remoteFull = remoteFolder.Path;
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteFolder);

            res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, res.RemoteStorage.RelativePath, IsFolder, RemoteToLocal);

            res.LocalStorage = null;

            res.Name = remoteRelative + Path.DirectorySeparatorChar;
            return res;
        }


        // Create Full Synctriplet, useful when remote has high prioirty, eg: changelog
        public static SyncTriplet CreateFromRemoteFolder(
            IFolder remoteFolder,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {

            SyncTriplet res = CreateSFPFromRemoteFolder (remoteFolder, cmisSyncFolder);

            res.LocalStorage = (null == res.DBStorage.DBLocalPath) ? null : new LocalStorageItem (cmisSyncFolder.LocalPath, res.DBStorage.DBLocalPath);

            return res;
        }


        /// <summary>
        /// Creates the SFP From the local document. Return in the form:
        ///   &lt;LS, DB, ??&gt;
        /// </summary>
        /// <returns>The SFP From local document.</returns>
        /// <param name="localFullPath">Local full path.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncTriplet CreateSFPFromLocalDocument(
            String localFullPath,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            SyncTriplet res = new SyncTriplet (IsDocument);

            String localRoot = cmisSyncFolder.LocalPath;
            String localRelative = localFullPath.Substring (localRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.LocalStorage = new LocalStorageItem (localRoot, localRelative);

            res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, localRelative, IsDocument, LocalToRemote);

            res.Name = localRelative;
            return res;
        }

        /// <summary>
        /// Create FULL synctriplet from local files using the remote path in DB.
        /// This method is used by local watcher / local change processors that they
        /// do not require remote storage status.
        /// 
        /// The sync direction is local_to_remote
        /// </summary>
        /// <returns>The synctriplet from local document.</returns>
        /// <param name="localFullPath">Local full path.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncTriplet CreateFromLocalDocument(
            String localFullPath,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            SyncTriplet res = CreateSFPFromLocalDocument(localFullPath, cmisSyncFolder);
            res.Direction = DIRECTION.LOCAL2REMOTE;
            return res;
        }
 
        /// <summary>
        /// Creates the SFP from local folders.
        /// </summary>
        /// <returns>The SFP from local folder.</returns>
        /// <param name="localFullPath">Local full path.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncTriplet CreateSFPFromLocalFolder(
            String localFullPath,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            SyncTriplet res = new SyncTriplet (IsFolder);

            String localRoot = cmisSyncFolder.LocalPath;
            String localRelative = localFullPath.Substring (localRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.LocalStorage = new LocalStorageItem (localRoot, localRelative);

            res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, localRelative, IsFolder, LocalToRemote);

            res.Name = localRelative + Path.DirectorySeparatorChar;
            return res;
        }

        /// <summary>
        /// Create FULL synctriplet from local folders using the remote path in DB.
        /// Same to CreateFromLocalFile, this method is used by local watcher/change processors.
        /// 
        /// The sync direction is local_to_remote
        /// </summary>
        /// <returns>The synctriplet from local folder.</returns>
        /// <param name="localFullPath">Local full path.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncTriplet CreateFromLocalFolder (
            String localFullPath,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            SyncTriplet res = CreateSFPFromLocalFolder(localFullPath, cmisSyncFolder);
            res.Direction = DIRECTION.LOCAL2REMOTE;
            return res;
        }

        public static SyncTriplet CreateSFPFromDBFile(
            string localRelativePath,
            string remoteRelativePath,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder
        ) {
            SyncTriplet res = new SyncTriplet (IsDocument);
            res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, localRelativePath, remoteRelativePath, IsDocument);
            res.Name = localRelativePath;
            return res;
        }

        public static SyncTriplet CreateSFPFromDBFolder (
            string localRelativePath,
            string remoteRelativePath,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder
        )
        {
            SyncTriplet res = new SyncTriplet (IsFolder);
            res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, localRelativePath, remoteRelativePath, IsFolder);
            res.Name = localRelativePath + Path.DirectorySeparatorChar;
            return res;
        }


        /// <summary>
        /// Assemble an SFP created by a remote object to an SFP created by a local object.
        /// It will fill the local SFP's RemoteStorageItem by the one from the remote SFP.
        /// </summary>
        /// <param name="remoteSemi">Remote semi.</param>
        /// <param name="localSemi">Local semi.</param>
        public static void AssembleRemoteIntoLocal(SyncTriplet remoteSemi, SyncTriplet localSemi) {
            localSemi.RemoteStorage = new RemoteStorageItem (remoteSemi.RemoteStorage);
        }

        /// <summary>
        /// Assembles a remoteDocument to the SFP created by a local object.
        /// </summary>
        /// <param name="remoteDocument">Remote document.</param>
        /// <param name="localSemi">Local semi.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static void AssembleRemoteIntoLocal(IDocument remoteDocument, SyncTriplet localSemi, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            String remoteRoot = cmisSyncFolder.RemotePath;
            String remoteFull = CmisFileUtil.PathCombine (
                SyncFileUtil.GetApplicablePath (remoteDocument, cmisSyncFolder),
                CmisFileUtil.GetLocalFileName (remoteDocument, cmisSyncFolder.CmisProfile));
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            localSemi.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteDocument);
        }

        /// <summary>
        /// Assemble an SFP created by a local object with the remote document's information.
        /// It will fill the local SFP's RemoteStorageItem by the one created from the remote document.
        /// </summary>
        /// <param name="remoteDocument">Remote document.</param>
        /// <param name="fullPath">Full path.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        /// <param name="localSemi">Local semi.</param>
        public static void AssembleRemoteIntoLocal(IDocument remoteDocument, String fullPath, SyncTriplet localSemi, CmisSyncFolder.CmisSyncFolder cmisSyncFolder) {
            String remoteRoot = cmisSyncFolder.RemotePath;
            String remoteFull = fullPath;
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            localSemi.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteDocument);

        }

        /// <summary>
        /// Assemble an SFP created by a local object with the remote folder's information.
        /// It will fill the local SFP's RemoteStorageItem by the one created from the remote folder.
        /// </summary>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        /// <param name="localSemi">Local semi.</param>
        public static void AssembleRemoteIntoLocal(IFolder remoteFolder, SyncTriplet localSemi, CmisSyncFolder.CmisSyncFolder cmisSyncFolder) {
            // Create remote storage
            String remoteRoot = cmisSyncFolder.RemotePath;
            String remoteFull = remoteFolder.Path;
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            localSemi.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteFolder);

        }

        /// <summary>
        /// Assemble an SFP created by a local object to an SFP created by a remote object.
        /// It will fill the remote SFP's LocalStorageItem by the one from the local SFP.
        /// </summary>
        /// <param name="localSemi">Local semi.</param>
        /// <param name="remoteSemi">Remote semi.</param>
        public static void AssembleLocalIntoRemote(SyncTriplet localSemi, SyncTriplet remoteSemi) {
            remoteSemi.LocalStorage = new LocalStorageItem (localSemi.LocalStorage);
        }

        /// <summary>
        /// Creates the synctriplet for local renamed object. The Name field of the resulting synctriplet 
        /// will be the relative name in DB (which means, the old one). 
        /// 
        /// The direction is local_to_remote
        /// 
        /// Similar with old SynchronizedFolder/WatcherStrategy, this method can return null when
        /// newFullPath does not exist.
        /// </summary>
        /// <returns>The triplet from local renamed object.</returns>
        /// <param name="oldFullPath">Old full path.</param>
        /// <param name="newFullPath">New full path.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncTriplet CreateFromLocalRenamedObject(
            String oldFullPath,
            String newFullPath,
            bool isFolder,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {

            SyncTriplet res = null;
            String localRoot = cmisSyncFolder.LocalPath;
            String localOldRelative = oldFullPath.Substring (localRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            String localNewRelative = newFullPath.Substring (localRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);

            try {
                res = new SyncTriplet (isFolder, DIRECTION.LOCAL2REMOTE);
                res.LocalStorage = new LocalStorageItem (localRoot, localNewRelative);
                res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, localOldRelative, isFolder, LocalToRemote);

            } catch {
                /*
                Boolean isFolder = true;
                if (cmisSyncFolder.Database.IsFile(oldFullPath)) {
                    isFolder = false;
                } else {
                    isFolder = true;
                }
                res = new SyncTriplet (isFolder);
                res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, localOldRelative, isFolder, LocalToRemote);
                res.LocalStorage = null;
                */
            }

            res.Name = isFolder ?
                res.LocalStorage.RelativePath + Path.DirectorySeparatorChar :
                res.LocalStorage.RelativePath;

            return res;
        }
    }
}
