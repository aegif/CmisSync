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
    public static class ProcessWorker
    {


        // =================== FoldersDependencies Approach ================
        public static bool Process (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                    FoldersDependencies fdps)

        {

            return Reducer (triplet, session, cmisSyncFolder, fdps);
        }

        public static bool Reducer (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                    FoldersDependencies fdps)
        {

            Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: Processed!\n",
                               System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

            if (triplet.LocalEqDB && triplet.RemoteEqDB) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1} is syncrhonized!\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                return true;
            } else if (triplet.LocalEqDB && !triplet.RemoteEqDB) {
                return SyncRemoteToLocal (triplet, session, cmisSyncFolder, fdps);
            } else if (!triplet.LocalEqDB && triplet.RemoteEqDB) {
                return SyncLocalToRemote (triplet, session, cmisSyncFolder, fdps);
            } else {
                return SolveConflictAndSync (triplet, session, cmisSyncFolder, fdps);
            }
        }

        public static bool SyncRemoteToLocal (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                              FoldersDependencies fdps)
        {
            if (!triplet.RemoteExist) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: remote removed! delete local!\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                // Remote deleted, remove local.
                if (triplet.IsFolder) {
                    // sub folder might be already removed due to parent folder has been removed
                    if (triplet.LocalExist) {

                        Console.WriteLine (" # [ WorkerThread: {0} ] wait spin for {1}\n",
                                           System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                        // TODO : add fdps resolve
                        var sw = new SpinWait ();
                        while (fdps.GetFolderDependenceCount(triplet.Name) != 0) {
                            sw.SpinOnce ();
                        }

                        Console.WriteLine (" # [ WorkerThread: {0} ] delete local {1} after wait spin\n",
                                           System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                        return WorkerOperations.DeleteLocalFolder (triplet, fdps.IsClear(triplet.Name), cmisSyncFolder);
                    }
                } else {
                    return WorkerOperations.DeleteLocalFile (triplet, cmisSyncFolder);
                }


            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: download remote to local\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                // Remote new, download
                if (triplet.IsFolder) {
                    if (!triplet.LocalExist) return WorkerOperations.CreateLocalFolder (triplet, session, cmisSyncFolder);
                } else {
                    return WorkerOperations.DownloadFile (triplet, session, cmisSyncFolder);
                }
            }
            return true;
        }

        public static bool SyncLocalToRemote (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                              FoldersDependencies fdps)
        {

            if (cmisSyncFolder.BIDIRECTIONAL) {
                if (triplet.LocalExist) {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: upload local to remote\n",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    // Local Exist, upload to remote
                    if (triplet.IsFolder) {
                        if (!triplet.RemoteExist) return WorkerOperations.CreateRemoteFolder (triplet, session, cmisSyncFolder);
                    } else {
                        if (triplet.RemoteExist) {
                            return WorkerOperations.UpdateRemoteFile (triplet, session, cmisSyncFolder);
                        } else {
                            return WorkerOperations.UploadFile (triplet, session, cmisSyncFolder);
                        }
                    }

                } else {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: local deleted! remove remote file\n",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    // Local removed, delete remote
                    if (triplet.IsFolder) {

                        // TODO: add fdps resolve
                        return WorkerOperations.DeleteRemoteFolder (triplet, session, cmisSyncFolder);

                    } else {

                        return WorkerOperations.DeleteRemoteFile (triplet, session, cmisSyncFolder);
                    }
                }

            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: not BIDIRECTIONAL, do not upload local to remote\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                return true;
            }
            return true;
        }

        public static bool SolveConflictAndSync (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                                 FoldersDependencies fdps)
        {

            // If LS=ne , DB=e, RS=ne: 
            // both removed, clear DB record only
            if (!triplet.LocalExist && !triplet.RemoteExist) {
                return WorkerOperations.RemoveDbRecord (triplet, cmisSyncFolder);
            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: conflict! rename local file\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                WorkerOperations.SolveConflict (triplet, cmisSyncFolder);

                return SyncRemoteToLocal (triplet, session, cmisSyncFolder, fdps);
            }
        }

// ======================================== old approaches ==================================================================
        public static bool Process(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder, 
                                   ConcurrentQueue<SyncTriplet.SyncTriplet> delayedFolderDeletions ) {

            return Reducer (triplet, session, cmisSyncFolder, delayedFolderDeletions);
        }

        public static bool Reducer(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder, 
                                   ConcurrentQueue<SyncTriplet.SyncTriplet> delayedFolderDeletions){

            Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: Processed!\n",
                               System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

            if (triplet.LocalEqDB && triplet.RemoteEqDB) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1} is syncrhonized!\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                return true;
            } else if (triplet.LocalEqDB && !triplet.RemoteEqDB) {
                return SyncRemoteToLocal (triplet, session, cmisSyncFolder, delayedFolderDeletions);
            } else if (!triplet.LocalEqDB && triplet.RemoteEqDB) {
                return SyncLocalToRemote (triplet, session, cmisSyncFolder, delayedFolderDeletions);
            } else {
                return SolveConflictAndSync (triplet, session, cmisSyncFolder, delayedFolderDeletions);
            }
        }

        public static bool SyncRemoteToLocal(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                             ConcurrentQueue<SyncTriplet.SyncTriplet> delayedFolderDeletions) {
            if (!triplet.RemoteExist) {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: remote removed! delete local!\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                // Remote deleted, remove local.
                if (triplet.IsFolder) {
                    // sub folder might be already removed due to parent folder has been removed
                    if (triplet.LocalExist) {
                        /* it is not clear whether this operation is thread safe
                        if (Directory.EnumerateFileSystemEntries(triplet.LocalStorage.FullPath).Count() == 0) {
                            WorkerOperations.DeleteLocalFolder (triplet, cmisSyncFolder); 
                        }  else  {
                            delayedFolderDeletions.Enqueue (triplet); 
                        }
                        */
                        if (!triplet.Delayed) return WorkerOperations.DeleteLocalFolder (triplet, cmisSyncFolder);
                        else {
                            /*bool contains = false;
                            foreach (SyncTriplet.SyncTriplet trip in delayedFolderDeletions) {
                                if (trip.Name == triplet.Name) {
                                    contains = true;
                                    break;
                                }
                            }
                            if (!contains)*/ delayedFolderDeletions.Enqueue (triplet);
                            return true;
                        }
                    } 
                } else {
                    return WorkerOperations.DeleteLocalFile (triplet, cmisSyncFolder);
                }


            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: download remote to local\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                // Remote new, download
                if (triplet.IsFolder) {
                    if (!triplet.LocalExist) return WorkerOperations.CreateLocalFolder (triplet, session, cmisSyncFolder);
                } else {
                    return WorkerOperations.DownloadFile (triplet, session, cmisSyncFolder);
                }
            }
            return true;
        }

        public static bool SyncLocalToRemote(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                             ConcurrentQueue<SyncTriplet.SyncTriplet> delayedFolderDeletions) {

            if (cmisSyncFolder.BIDIRECTIONAL) {
                if (triplet.LocalExist) {
                    Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: upload local to remote\n",
                                    System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                    // Local Exist, upload to remote
                    if (triplet.IsFolder) {
                        if (!triplet.RemoteExist) return WorkerOperations.CreateRemoteFolder (triplet, session, cmisSyncFolder);
                    } else {
                        if (triplet.RemoteExist) {
                            return WorkerOperations.UpdateRemoteFile (triplet, session, cmisSyncFolder);
                        } else {
                            return WorkerOperations.UploadFile (triplet, session, cmisSyncFolder);
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
                            return true;
                        } else {
                            return WorkerOperations.DeleteRemoteFolder (triplet, session, cmisSyncFolder);
                        }
                    } else {
                        return WorkerOperations.DeleteRemoteFile (triplet, session, cmisSyncFolder);
                    }
                }
                    
            }  else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: not BIDIRECTIONAL, do not upload local to remote\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);
                return true;
            }
            return true;
        }

        public static bool SolveConflictAndSync(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                                ConcurrentQueue<SyncTriplet.SyncTriplet> delayedFolderDeletions) {

            // If LS=ne , DB=e, RS=ne: 
            // both removed, clear DB record only
            if (!triplet.LocalExist && !triplet.RemoteExist) {
                return WorkerOperations.RemoveDbRecord (triplet, cmisSyncFolder);
            } else {
                Console.WriteLine (" # [ WorkerThread: {0} ] SyncTriplet {1}: conflict! rename local file\n",
                                   System.Threading.Thread.CurrentThread.ManagedThreadId, triplet.Name);

                WorkerOperations.SolveConflict (triplet, cmisSyncFolder);

                return SyncRemoteToLocal (triplet, session, cmisSyncFolder, delayedFolderDeletions);
            }
        }


        // ================================== new approach , not used by processor yet ==========================

        public enum SyncAction { Upload, Download, DeleteLocal, DeleteRemote, Conflict, Sync };

        public static SyncAction Reduce (SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
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



    }
}
