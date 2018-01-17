using System;
using CmisSync.Lib.Sync.SyncTriplet.TripletItem;
using CmisSync.Lib.Cmis;
using DotCMIS.Client;
using log4net;

namespace CmisSync.Lib.Sync.SyncTriplet
{
    public static class SyncTripletFactory
    {
        static ILog logger = LogManager.GetLogger (typeof (SyncTripletFactory));

        public static SyncTriplet CreateFromRemoteDocument(
            IFolder remoteFolder,
            IDocument remoteDocument,
            RepoInfo repoInfo,
            Database.Database database) 
        {
            SyncTriplet res = new SyncTriplet ();

            res.IsFolder = false;

            // String remoteRoot = = CmisUtils.PathCombine (remoteIFolder.Path, repoInfo.CmisProfile.localFilename (remoteDocument));
            String remoteRoot = remoteFolder.Path;
            String remoteRelative = repoInfo.CmisProfile.localFilename (remoteDocument);

            String localRoot = repoInfo.TargetDirectory;
            String localRelative = remoteRelative;


            return res;
        }

        public static SyncTriplet CreateFromRemoteFolder(
            IFolder remoteFolder,
            RepoInfo repoInfo, 
            Database.Database database)
        {
 
            SyncTriplet res = new SyncTriplet ();

            res.IsFolder = true;

            String remoteRoot = repoInfo.RemotePath;
            String remoteRelative = remoteFolder.Path;

            if (remoteRelative.StartsWith (remoteRoot)) {
                remoteRelative = remoteRelative.Substring (remoteRoot.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
            }

            String localRoot = repoInfo.TargetDirectory;
            String localRelative = remoteRelative;

            res.RemoteStorage = new RemoteStorageItem (remoteRoot, remoteRelative, remoteFolder.LastModificationDate);

            res.LocalStorage = new LocalStorageItem (localRoot, localRelative);

            res.DBStorage = new DBStorageItem (database, remoteRelative, localRelative);

            logger.Debug (String.Format("Sync triplet from remote folder created! Remote root path: '{0}', " +
                                        "Remote relative path: '{1}', " +
                                        "Local root path: '{2}', " +
                                        "Local relative path: '{3}'", remoteRoot, remoteRelative, localRoot, localRelative));

            return res;
        }

        public static SyncTriplet CreateFromLocalDocument() {
            return new SyncTriplet ();
        }

        public static SyncTriplet CreateFromLocalFolder() {
            return new SyncTriplet ();
        }
    }
}
