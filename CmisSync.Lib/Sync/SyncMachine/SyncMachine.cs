using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
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
using CmisSync.Lib.Sync.SyncMachine.Internal;
using CmisSync.Lib.Sync.SyncMachine.Exceptions;
using CmisSync.Lib.Utilities.FileUtilities;
using CmisSync.Lib.Cmis;

using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace CmisSync.Lib.Sync.SyncMachine
{
    public class SyncMachine : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger (typeof (SyncMachine));

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private ISession session;

        private BlockingCollection<SyncTriplet.SyncTriplet> fullSyncTriplets = null; 

        private BlockingCollection<SyncTriplet.SyncTriplet> semiSyncTriplets = null;

        private ItemsDependencies itemsDependencies = null;

        private ProcessorCompleteAddingChecker processorCompleteAddingChecker = null;

        private SemiSyncTripletManager semiSyncTripletManager;

        private SyncTripletAssembler syncTripletAssembler;

        private SyncTripletProcessor syncTripletProcessor;

        private Watcher watcher = null;

        private object syncingLock = new object();

        public bool IsWorking = false;

        public SyncMachine (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session)
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.session = session;

            // semisynctriplet manager holds a concurrent semi triplet queue
            this.semiSyncTripletManager = new SemiSyncTripletManager (cmisSyncFolder, session);
            // processor holds a concurrent triplet process queue
            this.syncTripletProcessor = new SyncTripletProcessor (cmisSyncFolder, session);
            // assembler read semi triplet from semi queue, assemble it with remote info, and push assembled triplet to process queue
            this.syncTripletAssembler = new SyncTripletAssembler (cmisSyncFolder, session);
        }

        public bool DoCrawlSync ()
        {
            bool succeed = true;

            lock (syncingLock) {

                System.Console.WriteLine ("Crawl Sync Task Start: ");

                IsWorking = true;

                itemsDependencies = new ItemsDependencies ();
                processorCompleteAddingChecker = new ProcessorCompleteAddingChecker ( itemsDependencies );

                fullSyncTriplets = new BlockingCollection<SyncTriplet.SyncTriplet> ();
                semiSyncTriplets = new BlockingCollection<SyncTriplet.SyncTriplet> ();

                Task tripletProcessTask = Task.Factory.StartNew (() => this.syncTripletProcessor.Start (fullSyncTriplets, itemsDependencies, processorCompleteAddingChecker));
                Task semiManagerTask = Task.Factory.StartNew (() => this.semiSyncTripletManager.Start (semiSyncTriplets, itemsDependencies));
                Task tripletAssemblerTask = Task.Factory.StartNew (() => this.syncTripletAssembler.StartForLocalCrawler (semiSyncTriplets, fullSyncTriplets, itemsDependencies));

                semiManagerTask.Wait ();
                semiSyncTriplets.CompleteAdding ();

                // wait until assembler and processor completed
                tripletAssemblerTask.Wait ();
                // all semi-triplets assembled and pushed to process queue
                processorCompleteAddingChecker.assemblerCompleted = true;

                // a trick:
                // It is possible that semiSyncTriplets assembler ( especially the remote-crawler in it)
                // completes later than all full-triplets are processed but there is no remained remote-semi-triplet
                // to be appended the full triplet processor ( usually means there is no freshly created file/folder remotely ).
                // 
                // In such case, the processorCompletedAddingChecker in the full-triplet-processor's multi-thread processing
                // worker will not work because the the worker would only work when there is a NEW element ( actually they have
                // stopped before the remote-crawler has completed). So, push a Dummy triplet to the processor to enforce it 
                // work once again to check if the full-processor's paralle.foreach multi-thread pipeline should stop.
                SyncTriplet.SyncTriplet dummyTriplet = new SyncTriplet.SyncTriplet (false);
                fullSyncTriplets.TryAdd (dummyTriplet);

                tripletProcessTask.Wait ();

                if (cmisSyncFolder.CmisProfile.CmisProperties.ChangeLogCapability) {
                    try {
                        String token = CmisUtils.GetChangeLogToken (session);
                        System.Console.WriteLine ("Get server's changelog token {0}", token);
                        cmisSyncFolder.Database.SetChangeLogToken (token);
                    } catch (Exception e) {
                        System.Console.WriteLine ("Get server's changelog token error: {0}", e.Message);
                    }
                }

                System.Console.WriteLine ("Triplet Processor completed");

                IsWorking = false;
            }

            Console.WriteLine ("Crawl Sync Task Completed.");

            return succeed;
        }

        public bool DoChangeLogSync() {

            bool succeed = true;
            processorCompleteAddingChecker = new ProcessorCompleteAddingChecker ( itemsDependencies );

            lock (syncingLock) {

                System.Console.WriteLine ("Changelog Sync Task Start: ");

                IsWorking = true;

                itemsDependencies = new ItemsDependencies ();
                fullSyncTriplets = new BlockingCollection<SyncTriplet.SyncTriplet> ();

                Task tripletProcessTask = Task.Factory.StartNew (() => this.syncTripletProcessor.Start (fullSyncTriplets, itemsDependencies, processorCompleteAddingChecker));
                try {

                    Task tripletAssemblerTask = Task.Factory.StartNew (() => syncTripletAssembler.StartForChangeLog (fullSyncTriplets));
                    tripletAssemblerTask.Wait ();

                } catch (AggregateException ae) {
                    foreach (var e in ae.InnerExceptions) {
                        if (e is ChangeLogProcessorBrokenException) {
                            Console.WriteLine ("Changelog Sync Task broke: {0}", e.Message);
                            succeed = false;
                        } else {
                            succeed = false;
                            throw;
                        }
                    }
                }

                tripletProcessTask.Wait ();

                if (succeed) {
                    try {
                        String token = CmisUtils.GetChangeLogToken (session);
                        Console.WriteLine ("Get server's changelog token {0}", token);
                        cmisSyncFolder.Database.SetChangeLogToken (token);
                    } catch (Exception e) {
                        Console.WriteLine ("Get server's changelog token error: {0}", e.Message);
                    }
                }
                
                IsWorking = false;
            }

            Console.WriteLine ("Changelog Sync Task Completed.");
            return succeed;
        }

        ~SyncMachine()
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
                        // this.fullSyncTriplets.Dispose ();
                        this.semiSyncTriplets.Dispose ();
                        this.semiSyncTripletManager.Dispose ();
                        this.syncTripletAssembler.Dispose ();
                        this.syncTripletProcessor.Dispose ();
                    }
                    this.disposed = true;
                }
            }
        }

        public void DoChangeLogTest ()
        {
            Config.CmisSyncConfig.Feature features = null;
            if (ConfigManager.CurrentConfig.GetFolder (cmisSyncFolder.Name) != null)
                features = ConfigManager.CurrentConfig.GetFolder (cmisSyncFolder.Name).SupportedFeatures;

            int maxNumItems = (features != null && features.MaxNumberOfContentChanges != null) ?
                (int)features.MaxNumberOfContentChanges : 50;

            string lastTokenOnClient = cmisSyncFolder.Database.GetChangeLogToken ();
            string lastTokenOnServer = CmisUtils.GetChangeLogToken (session);

            if (lastTokenOnClient == lastTokenOnServer) {
                Console.WriteLine ("  Synchronized ");
                return;
            }
            if (lastTokenOnClient == null) {
                Console.WriteLine ("  Should do full sync! Local token is null");
                return;
            }

            var currentChangeToken = lastTokenOnClient;
            IChangeEvents changes;
            do {
                Console.WriteLine (" Get changes for current token: {0}", currentChangeToken);

                changes = session.GetContentChanges (currentChangeToken, cmisSyncFolder.CmisProfile.CmisProperties.IsPropertyChangesSupported, maxNumItems);
                var changeEvents = changes.ChangeEventList./*Where (p => p != changes.ChangeEventList.FirstOrDefault ()).*/ToList ();
                foreach (IChangeEvent changeEvent in changeEvents) {
                    Console.WriteLine (" Get change : {0}, {1} at [{2}]({3})", changeEvent.ObjectId, changeEvent.ChangeType, changeEvent.ChangeTime.ToString (), ((DateTime)changeEvent.ChangeTime).ToFileTime ());
                }
                currentChangeToken = changes.LatestChangeLogToken;
                if (changes.HasMoreItems == true && (currentChangeToken == null || currentChangeToken.Length == 0)) {
                    break;
                }

            } while (changes.HasMoreItems ?? false);
        }

        public void DoWatcherTest() {

            // TODO: watcher for test
            watcher = new Watcher (cmisSyncFolder.LocalPath);
            watcher.EnableRaisingEvents = true;

            watcher.ChangeEvent += (sender, e) => {
                WatcherEvent we = watcher.GetChangeQueue ().Last ();
                //watcher.Changed += (sender, e) => {
                //if (!e.FullPath.StartsWith (cmisSyncFolder.LocalPath) || e.FullPath.Equals (cmisSyncFolder.LocalPath)) return;
                //if (!SyncFileUtil.WorthSyncing (e.FullPath, cmisSyncFolder)) return;

                Console.WriteLine ("%% Filesystem changed: \n" +
                                   "   Name: {0}\n" +
                                   "   Path: {1}\n" +
                                   "   Type: {2}\n" +
                                   "   Object: {3}", e.Name, e.FullPath, e.ChangeType, e.GetType().ToString());
                Logger.Info (String.Format("FS wathcer: [0}: {1}, [{2}], len: {3}", e.Name, e.FullPath, e.ChangeType, ((Watcher)sender).GetChangeCount()));
            };

            while (true) {
                Thread.Sleep (100);
                /*Thread.Sleep (15000);
                Console.WriteLine ("## watcher count: {0}", watcher.GetChangeCount ()); */
            }

            return;
        }
    }
}
