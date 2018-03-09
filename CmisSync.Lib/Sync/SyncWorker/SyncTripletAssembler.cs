using log4net;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;

using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Sync.SyncWorker.Crawler;
using CmisSync.Lib.Utilities.FileUtilities;

using DotCMIS.Client;
using DotCMIS.Exceptions;

namespace CmisSync.Lib.Sync.SyncWorker
{
    public class SyncTripletAssembler : IDisposable
    {

        private static readonly ILog Logger = LogManager.GetLogger (typeof (SyncTripletAssembler));

        private ISession session;

        private BlockingCollection<SyncTriplet.SyncTriplet> fullSyncTriplets = null;

        private BlockingCollection<SyncTriplet.SyncTriplet> semiSyncTriplets = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder = null;

        private RemoteCrawlWorker remoteCrawlWorker = null;

        private ConcurrentDictionary<string, SyncTriplet.SyncTriplet> remoteBuffer = new ConcurrentDictionary<String, SyncTriplet.SyncTriplet> ();

        private Dictionary<String, bool> processedTriplets = new Dictionary<string, bool> ();

        public SyncTripletAssembler (CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                     ISession session,
                                     BlockingCollection<SyncTriplet.SyncTriplet> syncTriplets,
                                     BlockingCollection<SyncTriplet.SyncTriplet> semiTriplets 
                                    )
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.session = session;
            this.fullSyncTriplets = syncTriplets;
            this.semiSyncTriplets = semiTriplets;
            this.remoteCrawlWorker = new RemoteCrawlWorker (cmisSyncFolder, session, remoteBuffer);
        }

        public void Start() {

            // Start remote crawler for assemble 
            Task remoteCrawlTask = Task.Factory.StartNew (() => remoteCrawlWorker.Start () );

            // Assemble semiTriplets generated from local crawler
            foreach(SyncTriplet.SyncTriplet semiTriplet in semiSyncTriplets.GetConsumingEnumerable ()) {

                SyncTriplet.SyncTriplet remoteTriplet = null;

                // if ignore samelowername, use lowerinvariant to lookup in already-crawled-remote-triplet dictionary
                string _key = cmisSyncFolder.CmisProfile.CmisProperties.IgnoreIfSameLowercaseNames ? semiTriplet.Name.ToLowerInvariant () : semiTriplet.Name;

                // if remote info is already crawled
                if (remoteBuffer.TryGetValue (_key, out remoteTriplet)) {
                    SyncTripletFactory.AssembleRemoteIntoLocal (remoteTriplet, semiTriplet);
                } else {

                    // if remote is not crawled yet, lookup db for remote path and query CMIS server
                    if (semiTriplet.DBExist) {
                        string remotePath = CmisFileUtil.PathCombine (cmisSyncFolder.RemotePath, semiTriplet.DBStorage.DBRemotePath);

                        try {
                            ICmisObject remoteObject = session.GetObjectByPath (remotePath, false);
                            if (semiTriplet.IsFolder) {
                                IFolder remoteFolder = (IFolder)remoteObject;
                                SyncTripletFactory.AssembleRemoteIntoLocal (remoteFolder, cmisSyncFolder, semiTriplet);
                            } else {
                                // TODO: multi filing
                                IDocument remoteDocument = (IDocument)remoteObject;
                                SyncTripletFactory.AssembleRemoteIntoLocal (remoteDocument, remoteDocument.Paths [0], cmisSyncFolder, semiTriplet);
                            }
                        } catch (Exception) {
                            Console.WriteLine (" - remote path: {0} Not found", remotePath); 
                        }
                    } else {
                        // TODO:
                        // Local exist , DB not exist, Remote ???
                    }
                }

                if (!fullSyncTriplets.TryAdd (semiTriplet)) {
                    Console.WriteLine (" - assembled triplet: {0} is not appended to full sync triplet list.", semiTriplet.Name);
                } 
 
                processedTriplets.Add (_key, true);
            }

            remoteCrawlTask.Wait ();

            // Assemble semiTriplets generated from remote crawler, except those
            // are already processed in the previous process.
            Console.WriteLine (" - Adding remained remote triplets");
            foreach (string key in remoteBuffer.Keys) {
                if (processedTriplets.ContainsKey (key)) {
                    // Console.WriteLine (" - key: {0}'s assigned remote-semitriplet is already pushed to processor, ignore", key);
                } else {
                    Console.WriteLine (" - key: {0}'s assigned remote-semitriplet is not processed yet, push to full sync trip", key);

                    SyncTriplet.SyncTriplet remoteTriplet = null;
                    if (!remoteBuffer.TryGetValue (key, out remoteTriplet)) {
                        Console.WriteLine (" - assembler can not get remote semi triplet {0} ", key);
                        continue;
                    }
                    if (remoteTriplet == null) {
                        Console.WriteLine (" - assembled triplet: remote {0} is null.", key);
                        continue;
                    }
                    if (!fullSyncTriplets.TryAdd (remoteTriplet)) {
                        Console.WriteLine (" - assembled triplet: {0} is not appended to full sync triplet list.", remoteTriplet.Name);
                    }
                }
            }

            // Info full sync triplets that adding process is completed.
            fullSyncTriplets.CompleteAdding ();

        }

        ~SyncTripletAssembler ()
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
