using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;

using log4net;
using System.IO;
using CmisSync.Auth;
using CmisSync.Lib.ActivityListener;
using CmisSync.Lib.Config;
using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync.SyncMachine.Internal;
using CmisSync.Lib.Sync.SyncMachine.ProcessWorker.Internal;

using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace CmisSync.Lib.Sync.SyncMachine.ProcessWorker
{
    public enum SyncResult { SUCCEED = 1, FAILED = 0, UNRESOLVED = 2, CONFLICT = 3 };

    /// <summary>
    /// Process worker is the actual class to perform synchronizing on a synctriplet. It checks the synctriplet's
    /// dependencies syncresult (UNRESOLVED, CONFLICT) and its LS, DB, RS to decides which operation should be applied
    /// to the triplet.
    /// </summary>
    public static class ProcessWorker
    {

        /*
         * MEMO:
         *   Only following operations require dependency checking:
         *   - add files / add folders to remote server
         *   - delete folders        
         */

        // =================== ItemsDependencies Approach ================
        public static SyncResult Process (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                    ItemsDependencies idps)
        {

            if (!idps.IsResolved (triplet.Name)) {
                return SyncResult.UNRESOLVED;
            }

            if (idps.HasFailedDependence(triplet.Name)) {
                return SyncResult.FAILED;
            }

            return Reducer (triplet, session, cmisSyncFolder, idps);
        }

        public static SyncResult Reducer (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                    ItemsDependencies idps)
        {

            /*
             * If there are conflictions in one folder's dependencess but
             * the folder itself is synchronized, keep it synchronized and do
             * not perform SolveConflict method.             
             */            
            if (triplet.LocalEqDB && triplet.RemoteEqDB) {

                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1} is synchronized!",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                return SyncResult.SUCCEED;

            } else if (idps.HasConflictedDependence(triplet.Name)) {

                Console.WriteLine (" # [ WorkerThread: {0} ] There is confliction in the dependencies of syncTriplet {1}!",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                return SolveConflictAndSync (triplet, session, cmisSyncFolder);

            } else if (triplet.LocalEqDB && !triplet.RemoteEqDB) {

                return SyncRemoteToLocal (triplet, session, cmisSyncFolder);

            } else if (!triplet.LocalEqDB && triplet.RemoteEqDB) {

                return SyncLocalToRemote (triplet, session, cmisSyncFolder);

            } else {

                return SolveConflictAndSync (triplet, session, cmisSyncFolder);
            }
        }

        public static SyncResult SyncRemoteToLocal (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            if (!triplet.RemoteExist) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: remote removed! delete local!",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                // Remote deleted, remove local.
                if (triplet.IsFolder) {
                    // One should do deletelocalfolder even if local folder does not exist to
                    // ensure the folder been deleted in DB. Eg. the folder is renamed due to conflict
                    Console.WriteLine (" # [ WorkerThread: {0} ] delete local folder {1}.",
                                       System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                    return WorkerOperations.DeleteLocalFolder (triplet, cmisSyncFolder);
                } else {
                    return WorkerOperations.DeleteLocalFile (triplet, cmisSyncFolder);
                }


            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: download remote to local",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                // Remote new, download
                if (triplet.IsFolder) {
                    if (!triplet.LocalExist) {
                        return WorkerOperations.CreateLocalFolder (triplet, session, cmisSyncFolder);
                    }
                } else {
                    return WorkerOperations.DownloadFile (triplet, session, cmisSyncFolder);
                }
            }
            return SyncResult.SUCCEED;
        }

        public static SyncResult SyncLocalToRemote (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {

            if (cmisSyncFolder.BIDIRECTIONAL) {
                if (triplet.LocalExist) {

                    // remote not exist, create or upload to remote, no matter if the triplet is a RENAME triplet
                    if (!triplet.RemoteExist) {

                        Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: upload local to remote",
                                        System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                        Console.WriteLine (" # [ WorkerThread: {0} ] create {1}.",
                                           System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                        if (triplet.IsFolder)

                            return WorkerOperations.CreateRemoteFolder (triplet, session, cmisSyncFolder);
                        else
                            return WorkerOperations.UploadFile (triplet, session, cmisSyncFolder);

                    // remote exist, update    
                    } else {

                        Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: update remote by local",
                                        System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                        // not rename
                        if (triplet.LocalStorage.RelativePath == triplet.DBStorage.DBLocalPath) {
                            if (triplet.IsFolder) { }// do nothing 
                            else return WorkerOperations.UpdateRemoteFile (triplet, session, cmisSyncFolder);
                        } else {
                            //TODO
                            if (triplet.IsFolder) {
                                //TODO
                                // move remote folder
                            } else {
                                //TODO
                                return WorkerOperations.MoveRemoteFile (triplet, session, cmisSyncFolder);
                            }
                        }
                    }

                } else {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: local deleted! remove remote {2}",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name, (triplet.IsFolder ? "folder" : "file"));

                    // Local removed, delete remote
                    if (triplet.IsFolder) {

                        Console.WriteLine (" # [ WorkerThread: {0} ] Remove remote folder {1}.",
                                           System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);


                        return WorkerOperations.DeleteRemoteFolder (triplet, session, cmisSyncFolder);

                    } else {

                        return WorkerOperations.DeleteRemoteFile (triplet, session, cmisSyncFolder);
                    }
                }

            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: not BIDIRECTIONAL, do not upload local to remote",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                return SyncResult.SUCCEED;
            }
            return SyncResult.SUCCEED;
        }

        public static SyncResult SolveConflictAndSync (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {

            // If LS=ne , DB=e, RS=ne: 
            // both removed, clear DB record only
            if (!triplet.LocalExist && !triplet.RemoteExist) {
                return WorkerOperations.RemoveDbRecord (triplet, cmisSyncFolder);
            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: conflict! rename local file/folder",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                WorkerOperations.SolveConflict (triplet, cmisSyncFolder);

                SyncResult res = SyncRemoteToLocal (triplet, session, cmisSyncFolder);
                return  (res != SyncResult.FAILED) ? SyncResult.CONFLICT : SyncResult.FAILED;
            }
        }
    }
}
