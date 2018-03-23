#pragma warning disable 0414
using log4net;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Sync.SyncWorker.Crawler;

using DotCMIS.Client;

namespace CmisSync.Lib.Sync.SyncWorker
{
    /*
     *  In this very first version. We do noet consider about
     *  concurrent sync triplet construction from local and remote:
     *     local first, then remote
     */
    public class SemiSyncTripletManager : IDisposable
    {

        //private static readonly ILog Logger = LogManager.GetLogger (typeof (SemiSyncTripletManager));

        public BlockingCollection<SyncTriplet.SyncTriplet> semiSyncTriplets = new BlockingCollection<SyncTriplet.SyncTriplet> ();

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private ISession session;

        private LocalCrawlWorker localCrawlWorker;

        private ChangeLogProcessor changeLogProcessor;

        public SemiSyncTripletManager (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session)
        {
            this.session = session;
            this.cmisSyncFolder = cmisSyncFolder;
            this.localCrawlWorker = new LocalCrawlWorker (cmisSyncFolder, semiSyncTriplets);
            this.changeLogProcessor = new ChangeLogProcessor (cmisSyncFolder, session, semiSyncTriplets);
        }

        public void StartTest() {
            changeLogProcessor.Start ();
        }

        public void Start() {
            localCrawlWorker.Start ();
            // complete adding will stop blockcollection foreach loop in 
            // synctriplet assembler
            semiSyncTriplets.CompleteAdding ();
        }

        ~SemiSyncTripletManager ()
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
                    if (disposing)
                        this.semiSyncTriplets.Dispose ();
                    this.disposed = true;
                }
            }
        }
    }
}
#pragma warning restore 0414
