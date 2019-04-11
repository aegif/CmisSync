using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

using log4net;

using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Sync.SyncMachine.Internal;
using CmisSync.Lib.Utilities.FileUtilities;
using CmisSync.Lib.Cmis;

namespace CmisSync.Lib.Sync.SyncMachine.Crawler
{
    public class LocalChangeCrawlWorker : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger (typeof (LocalCrawlWorker));

        private BlockingCollection<SyncTriplet.SyncTriplet> outputQueue = null;

        private ItemsDependencies itemsDeps = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        public LocalChangeCrawlWorker (
            CmisSyncFolder.CmisSyncFolder folder,
            BlockingCollection<SyncTriplet.SyncTriplet> queue,
            ItemsDependencies idps
        )
        {
            this.cmisSyncFolder = folder;
            this.outputQueue = queue;
            this.itemsDeps = idps;
        }

        public void Start()
        {
            BlockingCollection<SyncTriplet.SyncTriplet> localCrawlerQueue = new BlockingCollection<SyncTriplet.SyncTriplet> ();
            LocalCrawlWorker localCrawler = new LocalCrawlWorker (cmisSyncFolder, localCrawlerQueue, new ItemsDependencies());
            localCrawler.Start ();
            localCrawlerQueue.CompleteAdding ();

            foreach (SyncTriplet.SyncTriplet triplet in localCrawlerQueue) {
                if (!triplet.LocalEqDB) {
                    //outputQueue.TryAdd (triplet);
                } else {
                    //itemsDeps.RemoveItemDependence (triplet.Name, ProcessWorker.SyncResult.SUCCEED);
                }
            }
        }

        ~LocalChangeCrawlWorker ()
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
                    }
                    this.disposed = true;
                }
            }
        }
    }
}
