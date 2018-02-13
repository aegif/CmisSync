using System;
using CmisSync.Lib.Sync.SynchronizeTriplet.TripletItem;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync.SyncRepo;
using DotCMIS.Client;
using log4net;

namespace CmisSync.Lib.Sync.SynchronizeTriplet
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
        static ILog logger = LogManager.GetLogger (typeof (SyncTripletFactory));

        /// <summary>
        /// Creates from remote document.
        /// Notice that in SyncTriplet, relative path is /remoteFolder/remoteFilename while
        /// in crawlers we use crawlpath = /remoteRoot/remoteFolder/remoteFilename
        /// </summary>
        /// <returns>The from remote document.</returns>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="remoteDocument">Remote document.</param>
        /// <param name="repoInfo">Repo info.</param>
        /// <param name="database">Database.</param>
        public static SyncTriplet CreateFromRemoteDocument(
            IFolder remoteFolder,
            IDocument remoteDocument,
            RepoInfo repoInfo,
            Database.Database database) 
        {
            SyncTriplet res = new SyncTriplet ();

            res.IsFolder = false;

            // Create remote storage
            String remoteRoot = repoInfo.RemotePath;
            String remoteFull= CmisUtils.PathCombine (remoteFolder.Path, repoInfo.CmisProfile.localFilename (remoteDocument));
            String remoteRelative= remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteDocument.LastModificationDate);

            // Check database
            res.DBStorage = new DBStorageItem (database, res.RemoteStorage.RelativePath, false, true);

            // Create local storage 
            String localRoot = repoInfo.TargetDirectory;
            String localRelative = res.DBStorage.DBLocalPath;
            res.LocalStorage = new LocalStorageItem (localRoot, localRelative);

            // TODO: debug
            System.Console.WriteLine ("Sync triplet from remote folder created! Remote root path: '{0}'\n" +
                            "  Remote relative path: '{1}'\n" +
                            "  Local root path: '{2}\n" +
                            "  Local relative path: '{3}'\n",
                            res.RemoteStorage.RootPath, res.RemoteStorage.RelativePath,
                            res.LocalStorage.RootPath, res.LocalStorage.RelativePath);

            return res;
        }

        /// <summary>
        /// Creates from remote folder.
        /// </summary>
        /// <returns>The from remote folder.</returns>
        /// <param name="remoteFolder">Remote folder.</param>
        /// <param name="repoInfo">Repo info.</param>
        /// <param name="database">Database.</param>
        public static SyncTriplet CreateFromRemoteFolder(
            IFolder remoteFolder,
            RepoInfo repoInfo, 
            Database.Database database)
        {
 
            SyncTriplet res = new SyncTriplet ();

            res.IsFolder = true;

            String remoteRoot = repoInfo.RemotePath;
            String remoteFull = remoteFolder.Path;
            String remoteRelative = remoteFull.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteFolder.LastModificationDate);

            res.DBStorage = new DBStorageItem (database, res.RemoteStorage.RelativePath, true, true);

            String localRoot = repoInfo.TargetDirectory;
            String localRelative = res.DBStorage.DBLocalPath;
            res.LocalStorage = new LocalStorageItem (localRoot, localRelative);

            // TODO: debug
            Console.WriteLine ("Sync triplet from remote folder created! Remote root path: '{0}'\n" +
                               "  Remote relative path: '{1}'\n" +
                               "  Local root path: '{2}\n" +
                               "  Local relative path: '{3}'\n",
                               res.RemoteStorage.RootPath, res.RemoteStorage.RelativePath,
                               res.LocalStorage.RootPath, res.LocalStorage.RelativePath);

            return res;
        }


        /// <summary>
        /// Creates from local document.
        /// Notice that because of CmisSync's logic,
        /// when Create*Local* is called, there is definitely
        /// no relative remote version.
        /// </summary>
        /// <returns>The from local document.</returns>
        /// <param name="localFullPath">Local full path.</param>
        /// <param name="repoInfo">Repo info.</param>
        /// <param name="database">Database.</param>
        public static SyncTriplet CreateFromLocalDocument(
            String localFullPath,
            RepoInfo repoInfo,
            Database.Database database
        )
        {
            SyncTriplet res = new SyncTriplet ();

            String localRoot = repoInfo.TargetDirectory;
            String localRelative = localFullPath.Substring (localRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.LocalStorage = new LocalStorageItem (localRoot, localRelative);

            res.DBStorage = new DBStorageItem (database, localRelative, false, false);

            res.RemoteStorage = new RemoteStorageItem (repoInfo.RemotePath, res.DBStorage.DBRemotePath, null);

            // TODO: debug
            System.Console.WriteLine ("Sync triplet from local document created! Local root path: '{0}'\n" +
                                      "  Local relative path: '{1}'\n" +
                                      res.RemoteStorage.RootPath, res.RemoteStorage.RelativePath);
            return res;
        }

        public static SyncTriplet CreateFromLocalFolder(
            String localFullPath,
            RepoInfo repoInfo,
            Database.Database database
        ) {
            SyncTriplet res = new SyncTriplet ();

            String localRoot = repoInfo.TargetDirectory;
            String localRelative = localFullPath.Substring (localRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            res.LocalStorage = new LocalStorageItem (localRoot, localRelative);

            res.DBStorage = new DBStorageItem (database, localRelative, true, false);

            res.RemoteStorage = new RemoteStorageItem (repoInfo.RemotePath, res.DBStorage.DBRemotePath, null);

            // TODO: debug
            System.Console.WriteLine ("Sync triplet from local folder created! Local root path: '{0}'\n" +
                                      "  Local relative path: '{1}'\n" +
                                      res.RemoteStorage.RootPath, res.RemoteStorage.RelativePath);
            return res;
        }
    }
}
