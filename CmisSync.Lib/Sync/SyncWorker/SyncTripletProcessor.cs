using log4net;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncWorker.ProcessWorker;

using DotCMIS.Client;

namespace CmisSync.Lib.Sync.SyncWorker
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
        public BlockingCollection<SyncTriplet.SyncTriplet> FullSyncTriplets = new BlockingCollection<SyncTriplet.SyncTriplet>();

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder = null;

        public SyncTripletProcessor (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session)
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.session = session;
        }

        public void Start() {
            ParallelOptions options = new ParallelOptions ();
            options.MaxDegreeOfParallelism = MaxParallelism;

            Parallel.ForEach (Internal.SingleItemPartitioner.Create (FullSyncTriplets.GetConsumingEnumerable ()), options, 
                              (triplet) => ProcessWorker.ProcessWorker.Process (triplet, session, cmisSyncFolder)
                             );
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
                        FullSyncTriplets.Dispose();
                    }
                    this.disposed = true;
                }
            }
        }
    }
}
