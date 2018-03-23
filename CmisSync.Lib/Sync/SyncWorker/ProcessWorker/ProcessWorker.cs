using System;
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
using CmisSync.Lib.Sync.SyncWorker.ProcessWorker.Internal;

using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace CmisSync.Lib.Sync.SyncWorker.ProcessWorker
{
    public static class ProcessWorker
    {

// ================================== new approach , not used by processor yet ==========================

        public enum SyncAction { Upload, Download, DeleteLocal, DeleteRemote, Conflict, Sync };

        public static SyncAction Reduce(SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder) 
        {
            
            if (triplet.LocalEqDB && triplet.RemoteEqDB) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1} is syncrhonized!\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                return SyncAction.Sync;
            } else if (triplet.LocalEqDB && !triplet.RemoteEqDB) {

                return SyncRemoteToLocalReducer (triplet, cmisSyncFolder);

            } else if (!triplet.LocalEqDB && triplet.RemoteEqDB) {

                return SyncLocalToRemoteReducer (triplet, cmisSyncFolder);

            } else {

                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: Conflict!\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                return SyncAction.Conflict;

            }

        }

        public static SyncAction SyncRemoteToLocalReducer (SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            if (!triplet.RemoteExist) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: remote removed! delete local!\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                return SyncAction.DeleteLocal;

            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: download remote to local\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                return SyncAction.Download;
            }
        }

        public static SyncAction SyncLocalToRemoteReducer (SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {

            if (cmisSyncFolder.BIDIRECTIONAL) {
                if (triplet.LocalExist) {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: upload local to remote\n",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    return SyncAction.Upload;

                } else {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: local deleted! remove remote file\n",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    return SyncAction.DeleteRemote;

                }

            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: not BIDIRECTIONAL, do not upload local to remote\n",
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

                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: conflict! only db record remained \n",
                                       System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                    WorkerOperations.RemoveDbRecord (triplet, cmisSyncFolder);

                } else {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: conflict! rename local file\n",
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


// ======================================== old approaches ==================================================================
        public static void Process(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder, 
                                   ConcurrentQueue<SyncTriplet.SyncTriplet> delayedFolderDeletions ) {

            Reducer (triplet, session, cmisSyncFolder, delayedFolderDeletions);
        }

        public static void Reducer(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder, 
                                   ConcurrentQueue<SyncTriplet.SyncTriplet> delayedFolderDeletions){

            if (triplet.LocalEqDB && triplet.RemoteEqDB) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1} is syncrhonized!\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
            } else if (triplet.LocalEqDB && !triplet.RemoteEqDB) {
                SyncRemoteToLocal (triplet, session, cmisSyncFolder, delayedFolderDeletions);
            } else if (!triplet.LocalEqDB && triplet.RemoteEqDB) {
                SyncLocalToRemote (triplet, session, cmisSyncFolder, delayedFolderDeletions);
            } else {
                SolveConflictAndSync (triplet, session, cmisSyncFolder, delayedFolderDeletions);
            }
        }

        public static void SyncRemoteToLocal(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                             ConcurrentQueue<SyncTriplet.SyncTriplet> delayedFolderDeletions) {
            if (!triplet.RemoteExist) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: remote removed! delete local!\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                // Remote deleted, remove local.
                if (triplet.IsFolder) {
                    // sub folder might be already removed due to parent folder has been removed
                    if (triplet.LocalExist && !triplet.Delayed) WorkerOperations.DeleteLocalFolder (triplet, cmisSyncFolder);
                    // use delayed property. if delayed, do not process it but move to delayed queue.
                    if (triplet.LocalExist && triplet.Delayed) delayedFolderDeletions.Enqueue(triplet);
                } else {
                    WorkerOperations.DeleteLocalFile (triplet, cmisSyncFolder);
                }


            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: download remote to local\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                // Remote new, download
                if (triplet.IsFolder) {
                    if (!triplet.LocalExist) WorkerOperations.CreateLocalFolder (triplet, session, cmisSyncFolder);
                } else {
                    WorkerOperations.DownloadFile (triplet, session, cmisSyncFolder);
                }
            }
        }

        public static void SyncLocalToRemote(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                             ConcurrentQueue<SyncTriplet.SyncTriplet> delayedFolderDeletions) {

            if (cmisSyncFolder.BIDIRECTIONAL) {
                if (triplet.LocalExist) {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: upload local to remote\n",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    // Local Exist, upload to remote
                    if (triplet.IsFolder) {
                        if (!triplet.RemoteExist) WorkerOperations.CreateRemoteFolder (triplet, session, cmisSyncFolder);
                    } else {
                        if (triplet.RemoteExist) {
                            WorkerOperations.UpdateRemoteFile (triplet, session, cmisSyncFolder);
                        } else {
                            WorkerOperations.UploadFile (triplet, session, cmisSyncFolder);
                        }
                    }

                } else {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: local deleted! remove remote file\n",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    // Local removed, delete remote
                    if (triplet.IsFolder) {

                        // add delayed property 
                        if (triplet.Delayed) {
                            delayedFolderDeletions.Enqueue(triplet);
                        } else {
                            WorkerOperations.DeleteRemoteFolder (triplet, session, cmisSyncFolder);
                        }
                    } else {
                        WorkerOperations.DeleteRemoteFile (triplet, session, cmisSyncFolder);
                    }
                }
                    
            }  else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: not BIDIRECTIONAL, do not upload local to remote\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
            }
        }

        public static void SolveConflictAndSync(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                                ConcurrentQueue<SyncTriplet.SyncTriplet> delayedFolderDeletions) {

            // If LS=ne , DB=e, RS=ne: 
            // both removed, clear DB record only
            if (!triplet.LocalExist && !triplet.RemoteExist) {
                WorkerOperations.RemoveDbRecord (triplet, cmisSyncFolder);
            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: conflict! rename local file\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                WorkerOperations.SolveConflict (triplet, cmisSyncFolder);

                SyncRemoteToLocal (triplet, session, cmisSyncFolder, delayedFolderDeletions);
            }
        }
    }
}
