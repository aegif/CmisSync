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
        /// Creates the SFGF rom remote document. Given remote folder where the document is.
        /// Therefore it is not necessary to check the document's folder.
        /// </summary>
        /// <returns>The SFGF rom remote document.</returns>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="remoteDocument">Remote document.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncTriplet CreateSFGFromRemoteDocument (
            IFolder remoteFolder,
            IDocument remoteDocument,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            SyncTriplet res = new SyncTriplet (IsDocument);

            // Create remote storage
            String remoteRoot = cmisSyncFolder.RemotePath;
            String remoteFull = CmisFileUtil.PathCombine (remoteFolder.Path, CmisFileUtil.GetLocalFileName (remoteDocument, cmisSyncFolder.CmisProfile));
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteDocument.LastModificationDate);

            // Check database
            res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, res.RemoteStorage.RelativePath, IsDocument, RemoteToLocal);

            // Create local storage 
            res.LocalStorage = null;

            res.Name = remoteRelative;
            return res;
        }

        /// <summary>
        /// Creates the SFGF rom remote document, given document only.
        /// Folder path should be checked among all possible paths to select the one
        /// start with CmisSyncFolder.RemotePath by SyncFileUtil.GetApplicablePath
        /// </summary>
        /// <returns>The SFGF rom remote document.</returns>
        /// <param name="remoteDocument">Remote document.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncTriplet CreateSFGFromRemoteDocument(
            IDocument remoteDocument,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder) 
        {
            SyncTriplet res = new SyncTriplet (IsDocument);

            String remoteRoot = cmisSyncFolder.RemotePath;
            String remoteFull = CmisFileUtil.PathCombine(
                SyncFileUtil.GetApplicablePath (remoteDocument, cmisSyncFolder), 
                CmisFileUtil.GetLocalFileName(remoteDocument, cmisSyncFolder.CmisProfile));
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteDocument.LastModificationDate);

            // Check database
            res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, res.RemoteStorage.RelativePath, IsDocument, RemoteToLocal);

            // Create local storage 
            res.LocalStorage = null;

            res.Name = remoteRelative;
            return res;
        }

        // Create Full Synctriplet, useful when remote has high prioirty, eg: changelog
        public static SyncTriplet CreateFromRemoteDocument(
            IFolder remoteFolder,
            IDocument remoteDocument,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            SyncTriplet res = CreateSFGFromRemoteDocument (remoteFolder, remoteDocument, cmisSyncFolder);
            // Create local storage 
            res.LocalStorage = (null == res.DBStorage.DBLocalPath) ? null : new LocalStorageItem(cmisSyncFolder.LocalPath, res.DBStorage.DBLocalPath);

            return res;
        }

        public static SyncTriplet CreateFromRemoteDocument(
            IDocument remoteDocumnt,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder) 
        {
            SyncTriplet res = CreateSFGFromRemoteDocument (remoteDocumnt, cmisSyncFolder);
            // Create local storage 
            res.LocalStorage = (null == res.DBStorage.DBLocalPath) ? null : new LocalStorageItem (cmisSyncFolder.LocalPath, res.DBStorage.DBLocalPath);

            return res;
        }


        public static SyncTriplet CreateSFGFromRemoteFolder (
            IFolder remoteFolder,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {

            SyncTriplet res = new SyncTriplet (IsFolder);

            String remoteRoot = cmisSyncFolder.RemotePath;
            String remoteFull = remoteFolder.Path;
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteFolder.LastModificationDate);

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
 
            SyncTriplet res = CreateSFGFromRemoteFolder (remoteFolder, cmisSyncFolder);

            res.LocalStorage = (null == res.DBStorage.DBLocalPath) ? null : new LocalStorageItem (cmisSyncFolder.LocalPath, res.DBStorage.DBLocalPath);

            return res;
        }


        // Create SFG of synctriplet from local:
        // LS, DB, ??
        public static SyncTriplet CreateSFGFromLocalDocument(
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

        // Create Full synctriplet
        // This method is almost useless.
        public static SyncTriplet CreateFromLocalDocument(
            String localFullPath,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            SyncTriplet res = CreateSFGFromLocalDocument(localFullPath, cmisSyncFolder);
            res.RemoteStorage = new RemoteStorageItem (cmisSyncFolder.RemotePath, res.DBStorage.DBRemotePath, null);
            return res;
        }
 
        public static SyncTriplet CreateSFGFromLocalFolder(
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

        public static SyncTriplet CreateFromLocalFolder (
            String localFullPath,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            SyncTriplet res = CreateSFGFromLocalFolder(localFullPath, cmisSyncFolder);

            res.RemoteStorage = new RemoteStorageItem (cmisSyncFolder.RemotePath, res.DBStorage.DBRemotePath, null);

            return res;
        }

        public static SyncTriplet CreateSFGFromDBFile(
            string localRelativePath,
            string remoteRelativePath,
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder
        ) {
            SyncTriplet res = new SyncTriplet (IsDocument);
            res.DBStorage = new DBStorageItem (cmisSyncFolder.Database, localRelativePath, remoteRelativePath, IsDocument);
            res.Name = localRelativePath;
            return res;
        }

        public static SyncTriplet CreateSFGFromDBFolder (
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


        public static void AssembleRemoteIntoLocal(SyncTriplet remoteSemi, SyncTriplet localSemi) {
            localSemi.RemoteStorage = new RemoteStorageItem (remoteSemi.RemoteStorage);
        }

        public static void AssembleRemoteIntoLocal(IDocument remoteDocument, String fullPath, CmisSyncFolder.CmisSyncFolder cmisSyncFolder, SyncTriplet localSemi) {
            String remoteRoot = cmisSyncFolder.RemotePath;
            String remoteFull = fullPath;
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            localSemi.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteDocument.LastModificationDate);

        }

        public static void AssembleRemoteIntoLocal(IFolder remoteFolder, CmisSyncFolder.CmisSyncFolder cmisSyncFolder, SyncTriplet localSemi) {
            // Create remote storage
            String remoteRoot = cmisSyncFolder.RemotePath;
            String remoteFull = remoteFolder.Path;
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            localSemi.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteFolder.LastModificationDate);

        }

        public static void AssembleLocalIntoRemote(SyncTriplet localSemi, SyncTriplet remoteSemi) {
            remoteSemi.LocalStorage = new LocalStorageItem (localSemi.LocalStorage);
        }
    }
}
