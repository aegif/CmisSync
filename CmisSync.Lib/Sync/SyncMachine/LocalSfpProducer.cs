#pragma warning disable 0414
using log4net;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Sync.SyncMachine.Crawler;
using CmisSync.Lib.Sync.SyncMachine.Internal;

using DotCMIS.Client;

namespace CmisSync.Lib.Sync.SyncMachine
{
    /*
     *  In this very first version. We do noet consider about
     *  concurrent sync triplet construction from local and remote:
     *     local first, then remote
     */
    public class LocalSfpProducer : IDisposable
    {

        //private static readonly ILog Logger = LogManager.GetLogger (typeof (SemiSyncTripletManager));

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private ISession session;

        public LocalSfpProducer (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session)
        {
            this.session = session;
            this.cmisSyncFolder = cmisSyncFolder;
        }

        public void StartForLocalCrawler(
            BlockingCollection<SyncTriplet.SyncTriplet> semi,
            ItemsDependencies fdps)
        {

            LocalCrawlWorker localCrawlWorker = new LocalCrawlWorker (cmisSyncFolder, semi, fdps);
            localCrawlWorker.Start ();
        }

        public void StartForLocalWatcher(
            Watcher watcher,
            BlockingCollection<SyncTriplet.SyncTriplet> full,
            ItemsDependencies fdps)
        {
            LocalWatcherProcessor localWatcherProcessor = new LocalWatcherProcessor (cmisSyncFolder, watcher, full, fdps);
            localWatcherProcessor.Start ();
        }

        public void StartForLocalChange(
            BlockingCollection<SyncTriplet.SyncTriplet> full,
            ItemsDependencies fdps)
        {
            LocalChangeCrawlWorker localChangeCrawlWorker = new LocalChangeCrawlWorker (cmisSyncFolder, full, fdps);
            localChangeCrawlWorker.Start ();
        }

        ~LocalSfpProducer ()
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
                    if (disposing) {}
                        //this.semiSyncTriplets.Dispose ();
                    this.disposed = true;
                }
            }
        }
    }
}
#pragma warning restore 0414
