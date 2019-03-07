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
    public enum SyncResult { SUCCEED = 1, FAILED = 0, UNRESOLVED = 2 };

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
                                    ItemsDependencies fdps)
        {

            return Reducer (triplet, session, cmisSyncFolder, fdps);
        }

        public static SyncResult Reducer (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                    ItemsDependencies fdps)
        {

            if (triplet.LocalEqDB && triplet.RemoteEqDB) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1} is syncrhonized!",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                return SyncResult.SUCCEED;
            } else if (triplet.LocalEqDB && !triplet.RemoteEqDB) {
                return SyncRemoteToLocal (triplet, session, cmisSyncFolder, fdps);
            } else if (!triplet.LocalEqDB && triplet.RemoteEqDB) {
                return SyncLocalToRemote (triplet, session, cmisSyncFolder, fdps);
            } else {
                return SolveConflictAndSync (triplet, session, cmisSyncFolder, fdps);
            }
        }

        public static SyncResult SyncRemoteToLocal (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                              ItemsDependencies fdps)
        {
            if (!triplet.RemoteExist) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: remote removed! delete local!",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                // Remote deleted, remove local.
                if (triplet.IsFolder) {
                    // sub folder might be already removed due to parent folder has been removed
                    if (triplet.LocalExist) {

                        // Spin wait until folder's dependencies are all resolved
                        // - local operation is fast therefore spin wait is ok.
                        if (!fdps.IsResolved(triplet.Name)) {
                            Console.WriteLine (" # [ WorkerThread: {0} ] folder {1}'s dependencies are not all resolved.",
                                               System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                            return SyncResult.UNRESOLVED;
                        }

                        Console.WriteLine (" # [ WorkerThread: {0} ] delete local folder {1}.",
                                           System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                        return WorkerOperations.DeleteLocalFolder (triplet, fdps.IsResolved (triplet.Name), cmisSyncFolder) ?
                            SyncResult.SUCCEED : SyncResult.FAILED;
                    }
                } else {
                    return WorkerOperations.DeleteLocalFile (triplet, cmisSyncFolder) ?
                        SyncResult.SUCCEED : SyncResult.FAILED;
                }


            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: download remote to local",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                // Remote new, download
                if (triplet.IsFolder) {
                    if (!triplet.LocalExist) {
                        return WorkerOperations.CreateLocalFolder (triplet, session, cmisSyncFolder) ?
                            SyncResult.SUCCEED : SyncResult.FAILED;
                    }
                } else {
                    return WorkerOperations.DownloadFile (triplet, session, cmisSyncFolder) ?
                        SyncResult.SUCCEED : SyncResult.FAILED;
                }
            }
            return SyncResult.SUCCEED;
        }

        public static SyncResult SyncLocalToRemote (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                              ItemsDependencies fdps)
        {

            if (cmisSyncFolder.BIDIRECTIONAL) {
                if (triplet.LocalExist) {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: upload local to remote",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    // remote not exist, create or upload to remote
                    if (!triplet.RemoteExist) {

                        if (!fdps.IsResolved (triplet.Name)) {
                            Console.WriteLine (" # [ WorkerThread: {0} ] item {1}'s depdencies are not all resolved.",
                                               System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                            return SyncResult.UNRESOLVED;
                        }

                        Console.WriteLine (" # [ WorkerThread: {0} ] create {1}.",
                                           System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                        if (triplet.IsFolder)
                            return WorkerOperations.CreateRemoteFolder (triplet, session, cmisSyncFolder) ?
                                SyncResult.SUCCEED : SyncResult.FAILED;
                        else 
                            return WorkerOperations.UploadFile (triplet, session, cmisSyncFolder) ?
                                SyncResult.SUCCEED : SyncResult.FAILED;

                        // remote exist, update    
                    } else {
                        if (triplet.IsFolder) { }// do nothing 
                        else return WorkerOperations.UpdateRemoteFile (triplet, session, cmisSyncFolder) ?
                                SyncResult.SUCCEED : SyncResult.FAILED;
                    }

                } else {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: local deleted! remove remote file",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    // Local removed, delete remote
                    if (triplet.IsFolder) {

                        if (!fdps.IsResolved(triplet.Name)) {
                            Console.WriteLine (" # [ WorkerThread: {0} ] folder {1}'s depdencies are not all resolved.",
                                System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                            return SyncResult.UNRESOLVED;
                        }

                        Console.WriteLine (" # [ WorkerThread: {0} ] Remove remote folder {1}.",
                                           System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);


                        return WorkerOperations.DeleteRemoteFolder (triplet, session, cmisSyncFolder) ?
                            SyncResult.SUCCEED : SyncResult.FAILED;

                    } else {

                        return WorkerOperations.DeleteRemoteFile (triplet, session, cmisSyncFolder) ?
                            SyncResult.SUCCEED : SyncResult.FAILED;
                    }
                }

            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: not BIDIRECTIONAL, do not upload local to remote",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                return SyncResult.SUCCEED;
            }
            return SyncResult.SUCCEED;
        }

        public static SyncResult SolveConflictAndSync (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                                 ItemsDependencies fdps)
        {

            // If LS=ne , DB=e, RS=ne: 
            // both removed, clear DB record only
            if (!triplet.LocalExist && !triplet.RemoteExist) {
                return WorkerOperations.RemoveDbRecord (triplet, cmisSyncFolder) ?
                    SyncResult.SUCCEED : SyncResult.FAILED;
            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: conflict! rename local file",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                WorkerOperations.SolveConflict (triplet, cmisSyncFolder);

                SyncRemoteToLocal (triplet, session, cmisSyncFolder, fdps);

                // Conflict would always return false
                // TODO: add WorkerStatus to indicate what indeed happened
                return SyncResult.FAILED;
            }
        }


        // ================================== new approach , not used by processor yet ==========================

        public enum SyncAction { Upload, Download, DeleteLocal, DeleteRemote, Conflict, Sync };

        public static SyncAction Reduce (SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {

            if (triplet.LocalEqDB && triplet.RemoteEqDB) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1} is syncrhonized!",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                return SyncAction.Sync;
            } else if (triplet.LocalEqDB && !triplet.RemoteEqDB) {

                return SyncRemoteToLocalReducer (triplet, cmisSyncFolder);

            } else if (!triplet.LocalEqDB && triplet.RemoteEqDB) {

                return SyncLocalToRemoteReducer (triplet, cmisSyncFolder);

            } else {

                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: Conflict!",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                return SyncAction.Conflict;

            }

        }

        public static SyncAction SyncRemoteToLocalReducer (SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            if (!triplet.RemoteExist) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: remote removed! delete local!",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                return SyncAction.DeleteLocal;

            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: download remote to local",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                return SyncAction.Download;
            }
        }

        public static SyncAction SyncLocalToRemoteReducer (SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {

            if (cmisSyncFolder.BIDIRECTIONAL) {
                if (triplet.LocalExist) {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: upload local to remote",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    return SyncAction.Upload;

                } else {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: local deleted! remove remote file",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    return SyncAction.DeleteRemote;

                }

            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: not BIDIRECTIONAL, do not upload local to remote",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                return SyncAction.Sync;
            }
        }

        public static void Process (SyncTriplet.SyncTriplet triplet, SyncAction action, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            switch (action) {
            case SyncAction.Sync:
                return;

            case SyncAction.Upload:

                if (triplet.IsFolder) {
                    if (!triplet.RemoteExist) WorkerOperations.CreateRemoteFolder (triplet, session, cmisSyncFolder);
                } else {
                    if (triplet.RemoteExist) {
                        WorkerOperations.UpdateRemoteFile (triplet, session, cmisSyncFolder);
                    } else {
                        WorkerOperations.UploadFile (triplet, session, cmisSyncFolder);
                    }
                }
                return;

            case SyncAction.Download:
                if (triplet.IsFolder) {
                    if (!triplet.LocalExist) WorkerOperations.CreateLocalFolder (triplet, session, cmisSyncFolder);
                } else {
                    WorkerOperations.DownloadFile (triplet, session, cmisSyncFolder);
                }
                return;

            case SyncAction.DeleteLocal:

                if (triplet.IsFolder) {
                    // sub folder might be already removed due to parent folder has been removed
                    if (triplet.LocalExist) WorkerOperations.DeleteLocalFolder (triplet, cmisSyncFolder);
                } else {
                    WorkerOperations.DeleteLocalFile (triplet, cmisSyncFolder);
                }
                return;

            case SyncAction.DeleteRemote:
                // Local removed, delete remote
                if (triplet.IsFolder) {
                    WorkerOperations.DeleteRemoteFolder (triplet, session, cmisSyncFolder);
                } else {
                    WorkerOperations.DeleteRemoteFile (triplet, session, cmisSyncFolder);
                }
                return;

            case SyncAction.Conflict:
                // If LS=ne , DB=e, RS=ne: 
                // both removed, clear DB record only
                if (!triplet.LocalExist && !triplet.RemoteExist) {

                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: conflict! only db record remained ",
                                       System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                    WorkerOperations.RemoveDbRecord (triplet, cmisSyncFolder);

                } else {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: conflict! rename local file",
                                       System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    WorkerOperations.SolveConflict (triplet, cmisSyncFolder);

                    SyncAction nextAction = Reduce (triplet, cmisSyncFolder);

                    Process (triplet, nextAction, session, cmisSyncFolder);

                }
                return;

            default:
                return;
            }
        }



    }
}
