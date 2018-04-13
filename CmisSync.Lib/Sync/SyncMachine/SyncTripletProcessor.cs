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

        public SyncTripletProcessor (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session)
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.session = session;
        }

        public void Start (
            BlockingCollection<SyncTriplet.SyncTriplet> full,
            ItemsDependencies fdps
        ) {

            fullSyncTriplets = full;

            itemsDeps = fdps;

            ParallelOptions options = new ParallelOptions ();
            options.MaxDegreeOfParallelism = MaxParallelism;


            // Process non-folder-deletion operations
            Parallel.ForEach (Internal.SingleItemPartitioner.Create (fullSyncTriplets.GetConsumingEnumerable ()), options,
                              (triplet) => {
                                  // ============== new approaches ==============
                                  // Use folders dependencies class to record all folders' dependencies 
                                  // one folder should be processed only after all its dependencies are processed.
                                  // hint:
                                  // because all folder deletions are enqueued after its conttents, 
                                  // we can use spin() or sleep() to wait for that, just as create remote folder
                                  bool succeed = ProcessWorker.ProcessWorker.Process (triplet, session, cmisSyncFolder, itemsDeps);
                                  Console.WriteLine (" P [ WorkerThread: {0} ] processing triplet [ {1} ]'s result: succeed={2}",
                                                     Thread.CurrentThread.ManagedThreadId, triplet.Name, succeed);
                                  itemsDeps.RemoveItemDependence (triplet.Name, succeed);
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
