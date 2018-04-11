﻿#pragma warning disable 0414
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
    public class SemiSyncTripletManager : IDisposable
    {

        //private static readonly ILog Logger = LogManager.GetLogger (typeof (SemiSyncTripletManager));

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private ISession session;

        private LocalCrawlWorker localCrawlWorker;

        public SemiSyncTripletManager (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session)
        {
            this.session = session;
            this.cmisSyncFolder = cmisSyncFolder;
        }

        public void Start(
            BlockingCollection<SyncTriplet.SyncTriplet> semi,
            FoldersDependencies fdps)
        {

            this.localCrawlWorker = new LocalCrawlWorker (cmisSyncFolder, semi, fdps);

            localCrawlWorker.Start ();
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
                    if (disposing) {}
                        //this.semiSyncTriplets.Dispose ();
                    this.disposed = true;
                }
            }
        }
    }
}
#pragma warning restore 0414
