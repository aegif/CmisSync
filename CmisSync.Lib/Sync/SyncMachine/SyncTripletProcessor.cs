using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncMachine.ProcessWorker;
using CmisSync.Lib.Sync.SyncMachine.Internal;

using DotCMIS.Client;

namespace CmisSync.Lib.Sync.SyncMachine
{
    public class SyncTripletProcessor
    {
        private static int MaxParallelism = 4;

        private static readonly ILog Logger = LogManager.GetLogger (typeof (SyncTripletProcessor));

        private ISession session;

        /*
         * Problem:
         *     if set max upper bound in FullSyncTriplet
         * ,its TryAdd will not be blocked but directly return false.
         * 
         * Busy wait with sleep?: problem: one thread might set complete while another is still sleeping
         * 
         */
        private BlockingCollection<SyncTriplet.SyncTriplet> fullSyncTriplets = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder = null;

        private ItemsDependencies itemsDeps = null;

        private ProcessorCompleteAddingChecker completeChecker = null;

        private ConcurrentDictionary<String, int> retryCounter = null;

        public SyncTripletProcessor (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session)
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.session = session;
        }

        public void Start (
            BlockingCollection<SyncTriplet.SyncTriplet> full,
            ItemsDependencies fdps,
            ProcessorCompleteAddingChecker checker
        )
        {

            fullSyncTriplets = full;

            itemsDeps = fdps;

            completeChecker = checker;

            retryCounter = new ConcurrentDictionary<string, int> ();
             
            ParallelOptions options = new ParallelOptions ();
            options.MaxDegreeOfParallelism = MaxParallelism;


            // Process non-folder-deletion operations
            Parallel.ForEach (Internal.SingleItemPartitioner.Create (fullSyncTriplets.GetConsumingEnumerable ()), options,
                (triplet) => {
                    if (triplet.Name != "") {
                        SyncResult syncRes = ProcessWorker.ProcessWorker.Process (triplet, session, cmisSyncFolder, itemsDeps);


                        if (syncRes == SyncResult.UNRESOLVED) {
                            if (!retryCounter.ContainsKey (triplet.Name)) {
                                Console.WriteLine (" P [ WorkerThread: {0} ] processing triplet [ {1} ]'s result = {2}",
                                                     Thread.CurrentThread.ManagedThreadId, triplet.Name, syncRes);
                                retryCounter [triplet.Name] = 1;
                            } else {
                                retryCounter [triplet.Name]++;
                                if (retryCounter [triplet.Name] > 5) Thread.Sleep (100);
                            }

                            fullSyncTriplets.TryAdd (triplet);
                        } else {
                            Console.WriteLine (" P [ WorkerThread: {0} ] processing triplet [ {1} ]'s result = {2}",
                                             Thread.CurrentThread.ManagedThreadId, triplet.Name, syncRes);
                            itemsDeps.RemoveItemDependence (triplet.Name, syncRes);
                        }
                    } else {
                        Console.WriteLine (" P [ WorkerThread: {0} ] has get a dummy triplet. ", Thread.CurrentThread.ManagedThreadId);
                    }

                    if (checker.processorCompleteAdding ()) {

                        if (!fullSyncTriplets.IsAddingCompleted) {
                            Console.WriteLine (" P [ WorkerThread: {0} ] all full-sync-triplets are processed. Set fullSyncTriplets queue CompleteAdding().",
                                Thread.CurrentThread.ManagedThreadId);
                            fullSyncTriplets.CompleteAdding ();
                        } else {
                            Console.WriteLine (" P [ WorkerThread: {0} ] FullSyncTriplets queue is already IsAddingCompleted.",
                                Thread.CurrentThread.ManagedThreadId);
                        }
                    }
                });
        }

        ~SyncTripletProcessor ()
        {
            Dispose (false);
        }

        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        private Object disposeLock = new object ();
        private bool disposed = false;
        protected virtual void Dispose (bool disposing)
        {
            lock (disposeLock) {
                if (!this.disposed) {
                    if (disposing) {
                        //FullSyncTriplets.Dispose();
                    }
                    this.disposed = true;
                }
            }
        }
    }
}
