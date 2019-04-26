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

    /// <summary>
    /// SyncMachine is operated(used) by SyncWorker to do synchronizing work. It contains:
    ///   a semiSyncTripletManager that calls crawler to crawl semi-finished-goods of synctriplet
    ///     and pushes them to semiSyncTriplets queue
    ///   a semiSyncTriplets queue that accepts semi-finished-goods of synctriplet from the crawler
    ///   a syncTripletAssembler that reads semi-finished-goods from semiSyncTriplets and fills 
    ///     them with necessary information, then pushes them to the fullSynTriplets queue
    ///   a fullSyncTriplets queue that accept synctriplets from the assembler
    ///   a synTripletProcessor that processes all synctriplets from fullSyncTriplets queue
    ///   an ItemsDependencies hashmap to record dependence among synctriplets
    /// 
    /// DoCrawlSync is called to do crawling synchronizing. DoChangeLogSync is called to do changelog
    /// synchronizing. 
    /// 
    /// TODO: DoWatcherSync
    /// </summary>
    public class SyncMachine : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger (typeof (SyncMachine));

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private ISession session;

        private BlockingCollection<SyncTriplet.SyncTriplet> fullSyncTriplets = null; 

        private BlockingCollection<SyncTriplet.SyncTriplet> semiSyncTriplets = null;

        private ItemsDependencies itemsDependencies = null;

        private ProcessorCompleteAddingChecker processorCompleteAddingChecker = null;

        private LocalSfpProducer localSfpProducer;

        private SyncTripletAssembler syncTripletAssembler;

        private SyncTripletProcessor syncTripletProcessor;

        private Watcher watcher = null;

        private object syncingLock = new object();

        public bool IsWorking = false;

        public SyncMachine (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session)
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.session = session;

            if (cmisSyncFolder.IsWatcherEnabled) {
                watcher = new Watcher (cmisSyncFolder.LocalPath);
                watcher.EnableRaisingEvents = true;
            }

            // semisynctriplet manager holds a concurrent semi triplet queue
            this.localSfpProducer = new LocalSfpProducer (cmisSyncFolder, session);
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
                Task localSpfProduceTask = Task.Factory.StartNew (() => this.localSfpProducer.StartForLocalCrawler (semiSyncTriplets, itemsDependencies));
                Task tripletAssemblerTask = Task.Factory.StartNew (() => this.syncTripletAssembler.StartForLocalCrawler (semiSyncTriplets, fullSyncTriplets, itemsDependencies));

                localSpfProduceTask.Wait ();
                semiSyncTriplets.CompleteAdding ();

                // wait until assembler and processor completed
                tripletAssemblerTask.Wait ();
                // all semi-triplets assembled and pushed to process queue
                processorCompleteAddingChecker.assemblerCompleted = true;

                /*
                 * a trick:
                 * It is possible that semiSyncTriplets assembler ( especially the remote-crawler in it)
                 * completes later than all full-triplets are processed but there is no remained remote-semi-triplet
                 * to be appended the full triplet processor ( usually means there is no freshly created file/folder remotely ).
                 * 
                 * In such case, the processorCompletedAddingChecker in the full-triplet-processor's multi-thread processing
                 * worker will not work because the the worker would only work when there is a NEW element ( actually they have
                 * stopped before the remote-crawler has completed). So, push a Dummy triplet to the processor to enforce it 
                 * work once again to check if the full-processor's paralle.foreach multi-thread pipeline should stop.
                 */
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

        /// <summary>
        /// Dos the change log sync. 
        /// </summary>
        /// <returns><c>true</c>, if change log sync was done, <c>false</c> otherwise.</returns>
        public bool DoChangeLogSync()
        {
            bool res = true;
            IsWorking = true;
            lock (syncingLock) {
                res = DoNonLockChangeLogSync ();
            }
            IsWorking = false;
            return res;
        }

        /// <summary>
        /// Dos the local watcher sync, followed by local change sync and changelog sync
        /// </summary>
        /// <returns><c>true</c>, if local watcher sync was done, <c>false</c> otherwise.</returns>
        public bool DoLocalWatcherSync()
        {
            bool res = true;
            IsWorking = true;
            lock (syncingLock) {
                res &= DoNonLockLocalWatcherSync ();
                res &= DoNonLockLocalChangeSync ();
                // TODO 
                // res &= DoNonLockChangeLogSync ();
            }
            IsWorking = false;
            return res;

        }

        public bool DoNonLockChangeLogSync ()
        {

            bool succeed = true;

            System.Console.WriteLine ("Changelog Sync Task Start: ");

            itemsDependencies = new ItemsDependencies ();
            processorCompleteAddingChecker = new ProcessorCompleteAddingChecker (itemsDependencies);

            fullSyncTriplets = new BlockingCollection<SyncTriplet.SyncTriplet> ();

            Task tripletProcessTask = Task.Factory.StartNew (() => this.syncTripletProcessor.Start (fullSyncTriplets, itemsDependencies, processorCompleteAddingChecker));
            try {

                Task tripletAssemblerTask = Task.Factory.StartNew (() => syncTripletAssembler.StartForChangeLog (fullSyncTriplets, itemsDependencies));
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

            // assembler completed.
            processorCompleteAddingChecker.assemblerCompleted = true;

            SyncTriplet.SyncTriplet dummyTriplet = new SyncTriplet.SyncTriplet (false);
            fullSyncTriplets.TryAdd (dummyTriplet);

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

            Console.WriteLine ("Changelog Sync Task Completed.");
            return succeed;

        }

        /// <summary>
        /// Do the local watcher sync.
        /// </summary>
        /// <returns><c>true</c>, if local watcher sync was done, <c>false</c> otherwise.</returns>
        public bool DoNonLockLocalWatcherSync ()
        {
            bool succeed = false;

            System.Console.WriteLine ("Local watcher sync start: ");

            itemsDependencies = new ItemsDependencies ();
            processorCompleteAddingChecker = new ProcessorCompleteAddingChecker (itemsDependencies);

            fullSyncTriplets = new BlockingCollection<SyncTriplet.SyncTriplet> ();
            semiSyncTriplets = new BlockingCollection<SyncTriplet.SyncTriplet> ();


            Task watcherTripletProcessTask = Task.Factory.StartNew (() => this.syncTripletProcessor.Start (fullSyncTriplets, itemsDependencies, processorCompleteAddingChecker));
            Task localSfpProducerTask = Task.Factory.StartNew (() => localSfpProducer.StartForLocalWatcher (watcher, semiSyncTriplets, itemsDependencies));
            Task tripletAssemblerTask = Task.Factory.StartNew (() => this.syncTripletAssembler.StartForLocalWatcherAndLocalChange (semiSyncTriplets, fullSyncTriplets));

            localSfpProducerTask.Wait ();
            semiSyncTriplets.CompleteAdding ();
            tripletAssemblerTask.Wait ();

            // all semi-triplets assembled and pushed to process queue
            processorCompleteAddingChecker.assemblerCompleted = true;

            SyncTriplet.SyncTriplet dummyTriplet = new SyncTriplet.SyncTriplet (false);
            fullSyncTriplets.TryAdd (dummyTriplet);

            watcherTripletProcessTask.Wait ();

            return succeed;
        }

        /// <summary>
        /// Dos the local change sync.
        /// </summary>
        /// <returns><c>true</c>, if local change sync was done, <c>false</c> otherwise.</returns>
        public bool DoNonLockLocalChangeSync()
        {
            bool succeed = false;

            System.Console.WriteLine ("Local change crawler start: ");

            itemsDependencies = new ItemsDependencies ();
            processorCompleteAddingChecker = new ProcessorCompleteAddingChecker (itemsDependencies);

            fullSyncTriplets = new BlockingCollection<SyncTriplet.SyncTriplet> ();
            semiSyncTriplets = new BlockingCollection<SyncTriplet.SyncTriplet> ();

            Task localChangeTripletProcessTask = Task.Factory.StartNew (() => this.syncTripletProcessor.Start (fullSyncTriplets, itemsDependencies, processorCompleteAddingChecker));
            Task localSfpProducerTask = Task.Factory.StartNew (() => this.localSfpProducer.StartForLocalChange (semiSyncTriplets, itemsDependencies));
            Task tripletAssemblerTask = Task.Factory.StartNew (() => this.syncTripletAssembler.StartForLocalWatcherAndLocalChange (semiSyncTriplets, fullSyncTriplets));

            localSfpProducerTask.Wait ();
            semiSyncTriplets.CompleteAdding ();
            tripletAssemblerTask.Wait ();

            processorCompleteAddingChecker.assemblerCompleted = true;

            SyncTriplet.SyncTriplet dummyTriplet = new SyncTriplet.SyncTriplet (false);
            fullSyncTriplets.TryAdd (dummyTriplet);

            localChangeTripletProcessTask.Wait ();

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
                        this.localSfpProducer.Dispose ();
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


                    String[] tmp = cmisSyncFolder.Database.GetPathById (changeEvent.ObjectId.Split (CmisUtils.CMIS_FILE_SEPARATOR).Last ());
                    String remotePath = tmp == null ? "NULL" : tmp [0];
                    Console.WriteLine (" Remote path: {0}", remotePath);
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

            while (true) {
                Thread.Sleep (5000);
                Queue<WatcherEvent> changes = watcher.GetChangeQueue ();
                watcher.Clear ();

                foreach (WatcherEvent change in changes) {
                    var e = change.GetFileSystemEventArgs ();
                    if (!File.Exists (e.FullPath) && !Directory.Exists (e.FullPath)) {
                        Console.WriteLine ("%% Local file/folder: {0} not found, might be deleted.", e.FullPath);
                        continue;
                    }

                    if (!(e.ChangeType == WatcherChangeTypes.Deleted)) { //&& SyncFileUtil.WorthSyncing(e.FullPath, cmisSyncFolder)) {
                        Console.WriteLine ("%% Filesystem changed: \n" +
                          "   Name: {0}\n" +
                          "   Path: {1}\n" +
                          "   OldPath: {4}\n" + 
                          "   Change Type: {2}\n" +
                          "   e's Type: {5}\n" +
                          "   IsMove: {3}", e.Name, e.FullPath, e.ChangeType, e is CmisSync.Lib.Watcher.MovedEventArgs,
                          e is RenamedEventArgs ? ((RenamedEventArgs)e).OldFullPath : "",
                          e.GetType());
                        SyncTriplet.SyncTriplet triplet = null;
                        bool isFolder = Utils.IsFolder (e.FullPath);
                        switch (e.ChangeType) {
                        case WatcherChangeTypes.Created:

                            triplet = isFolder ?
                                SyncTriplet.SyncTripletFactory.CreateSFPFromLocalFolder (e.FullPath, cmisSyncFolder) :
                                SyncTriplet.SyncTripletFactory.CreateSFPFromLocalDocument (e.FullPath, cmisSyncFolder);
                            break;
                        case WatcherChangeTypes.Changed:
                            triplet = isFolder ?
                                SyncTriplet.SyncTripletFactory.CreateSFPFromLocalFolder (e.FullPath, cmisSyncFolder) :
                                SyncTriplet.SyncTripletFactory.CreateSFPFromLocalDocument (e.FullPath, cmisSyncFolder);
                            break;
                        case WatcherChangeTypes.Renamed:
                            triplet = SyncTriplet.SyncTripletFactory.CreateFromLocalRenamedObject (((RenamedEventArgs)e).OldFullPath, e.FullPath, isFolder, cmisSyncFolder);
                            break;
                        default:
                            break;
                        }

                        if (triplet != null) {
                            Console.WriteLine ("%% SyncTriplet: \n {0}", triplet.Name);
                        } else {
                            Console.WriteLine("%% Local file/folder: {0} not found, might be deleted.", e.FullPath);
                        }
                    } else {
                        if (e.ChangeType == WatcherChangeTypes.Deleted) {
                            Console.WriteLine ("%% File Deleted: {0}", e.FullPath);
                        }
                    }
                }
            }
        }
    }
}
