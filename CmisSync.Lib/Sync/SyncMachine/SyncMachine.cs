using System;
using System.Threading.Tasks;
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
using CmisSync.Lib.Sync.SyncMachine.Exceptions;
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

        private SemiSyncTripletManager semiSyncTripletManager;

        private SyncTripletAssembler syncTripletAssembler;

        private SyncTripletProcessor syncTripletProcessor;

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
            this.syncTripletAssembler = new SyncTripletAssembler (cmisSyncFolder, session, syncTripletProcessor.FullSyncTriplets, semiSyncTripletManager.semiSyncTriplets);

        }


        public bool DoCrawlSync ()
        {
            bool succeed = true;

            lock (syncingLock) {

                System.Console.WriteLine ("Start task: ");

                IsWorking = true;

                Task tripletProcessTask = Task.Factory.StartNew (() => this.syncTripletProcessor.Start ());
                Task semiManagerTask = Task.Factory.StartNew (() => this.semiSyncTripletManager.Start ());
                Task tripletAssemblerTask = Task.Factory.StartNew (() => this.syncTripletAssembler.StartForLocalCrawler ());

                semiManagerTask.Wait ();
                tripletAssemblerTask.Wait ();
                tripletProcessTask.Wait ();


                if (cmisSyncFolder.CmisProfile.CmisProperties.ChangeLogCapability) {
                    try {
                        String token = CmisUtils.GetChangeLogToken (session);
                        System.Console.WriteLine ("Get server's changelot token {0}", token);
                        cmisSyncFolder.Database.SetChangeLogToken (token);
                    } catch (Exception e) {
                        System.Console.WriteLine ("Get server's changelot token error: {0}", e.Message);
                    }
                }

                System.Console.WriteLine ("Triplet Processor completed");

                IsWorking = false;
            }

            return succeed;
        }

        public bool DoChangeLogSync() {

            bool succeed = true;

            lock (syncingLock) {
                IsWorking = true;
                Task tripletProcessTask = Task.Factory.StartNew (() => this.syncTripletProcessor.Start ());
                try {
                    Task tripletAssemblerTask = Task.Factory.StartNew (() => syncTripletAssembler.StartForChangeLog ());

                    tripletAssemblerTask.Wait ();
                } catch (ChangeLogProcessorBreakException ex) {
                    succeed = false; 
                }
                tripletProcessTask.Wait ();
                IsWorking = false;
            }

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
                        this.semiSyncTripletManager.Dispose ();
                        this.syncTripletAssembler.Dispose ();
                        this.syncTripletProcessor.Dispose ();
                    }
                    this.disposed = true;
                }
            }
        }
    }
}
