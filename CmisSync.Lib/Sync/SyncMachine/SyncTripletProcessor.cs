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
         * Buzy wait with sleep?
         * 
         */
        private BlockingCollection<SyncTriplet.SyncTriplet> fullSyncTriplets = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder = null;

        private ItemsDependencies itemsDeps = null;

        private ProcessorCompleteAddingChecker completeChecker = null;

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

            ParallelOptions options = new ParallelOptions ();
            options.MaxDegreeOfParallelism = MaxParallelism;


            // Process non-folder-deletion operations
            Parallel.ForEach (Internal.SingleItemPartitioner.Create (fullSyncTriplets.GetConsumingEnumerable ()), options,
                (triplet) => {
                    if (triplet.Name != "") {
                        SyncResult syncRes = ProcessWorker.ProcessWorker.Process (triplet, session, cmisSyncFolder, itemsDeps);
                        Console.WriteLine (" P [ WorkerThread: {0} ] processing triplet [ {1} ]'s result = {2}",
                                         Thread.CurrentThread.ManagedThreadId, triplet.Name, syncRes);

                        if (syncRes == SyncResult.UNRESOLVED) {
                            Console.WriteLine (" P [ WorkerThread: {0} ] push triplet [ {1} ] back to fullSyncTriplets queue.",
                                Thread.CurrentThread.ManagedThreadId, triplet.Name);

                            fullSyncTriplets.TryAdd (triplet);
                        } else {
                            Console.WriteLine (" P [WorkerThread {0} ] triplet [ {1} ] is processed successfully. ",
                                Thread.CurrentThread.ManagedThreadId, triplet.Name);
                            itemsDeps.RemoveItemDependence (triplet.Name, syncRes == SyncResult.SUCCEED);
                        }
                    } else {
                        Console.WriteLine (" P [ WorkerThread: {0} ] has get a dummy triplet. ", Thread.CurrentThread.ManagedThreadId);
                    }

                    if (checker.processorCompleteAdding ()) {

                        Console.WriteLine (" P [ WorkerThread: {0} ] all full-sync-triplets are processed. Set fullSyncTriplets queue CompleteAdding().",
                            Thread.CurrentThread.ManagedThreadId);

                        fullSyncTriplets.CompleteAdding ();
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
