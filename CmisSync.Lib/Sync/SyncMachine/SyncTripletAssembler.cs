using log4net;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;

using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Sync.SyncMachine.Crawler;
using CmisSync.Lib.Sync.SyncMachine.Exceptions;
using CmisSync.Lib.Sync.SyncMachine.Internal;
using CmisSync.Lib.Utilities.FileUtilities;

using DotCMIS.Client;
using DotCMIS.Exceptions;

namespace CmisSync.Lib.Sync.SyncMachine
{
    public class SyncTripletAssembler : IDisposable
    {

        private static readonly ILog Logger = LogManager.GetLogger (typeof (SyncTripletAssembler));

        private ISession session;

        private BlockingCollection<SyncTriplet.SyncTriplet> fullSyncTriplets = null;

        private BlockingCollection<SyncTriplet.SyncTriplet> semiSyncTriplets = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder = null;

        private ChangeLogProcessor changeLogProcessor = null;

        private ConcurrentDictionary<string, SyncTriplet.SyncTriplet> remoteBuffer = new ConcurrentDictionary<String, SyncTriplet.SyncTriplet> ();

        private OrderedDictionary orderedRemoteBuffer = new OrderedDictionary ();
        private object orbLock = new object ();

        public SyncTripletAssembler (CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
                                     ISession session
                                    )
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.session = session;
        }

        public void StartForChangeLog(
            BlockingCollection<SyncTriplet.SyncTriplet> full 
        ) {
            this.fullSyncTriplets = full;
            this.changeLogProcessor = new ChangeLogProcessor (cmisSyncFolder, session, fullSyncTriplets);

            changeLogProcessor.Start ();

        }

        /*
         * There should be tow fdps(es): 
         *   one main for local crawler
         *   one sub for remote crawler
         * Else for one file /xxxx/yyyy/zzz, it is first added to fdps by local crawler
         * and is soon pushed to full processor queue and been processed therefore the 
         * fdp is delleted from fdps.  then there comes slow remote crawler add to fdps once again. 
         * 
         * It is not desired because we use empty fdps to judge if a folder should be deleted
         * 
         * So use local fdps for main, after procesing all local files, add remote fdps
         * to local fdps before push remote triplet to processor queue
         */
        public void StartForLocalCrawler (
            BlockingCollection<SyncTriplet.SyncTriplet> semi,
            BlockingCollection<SyncTriplet.SyncTriplet> full,
            FoldersDependencies fdps
        ) {

            // Foreach operation on BlockingCollectio is sequentially executed
            // so a common HashSet rather than ConcurrentDictionary should be enough.
            HashSet<string> processedTriplets = new HashSet<string> ();

            this.semiSyncTriplets = semi;
            this.fullSyncTriplets = full;

            FoldersDependencies r_fdps = new FoldersDependencies ();

            //this.remoteCrawlWorker = new RemoteCrawlWorker (cmisSyncFolder, session, remoteBuffer);
            RemoteCrawlWorker orderedRemoteCrawlWorker = new RemoteCrawlWorker (cmisSyncFolder, session, orderedRemoteBuffer, orbLock, r_fdps);

            // Start remote crawler for assemble 
            //Task remoteCrawlTask = Task.Factory.StartNew (() => remoteCrawlWorker.Start () );
            Task remoteCrawlTask = Task.Factory.StartNew (() => orderedRemoteCrawlWorker.Start ());

            // Assemble semiTriplets generated from local crawler
            foreach(SyncTriplet.SyncTriplet semiTriplet in semiSyncTriplets.GetConsumingEnumerable ()) {

                SyncTriplet.SyncTriplet remoteTriplet = null;

                // If ignore samelowername, use lowerinvariant to lookup in already-crawled-remote-triplet dictionary.
                // One note: IgnoreIfSameLowercaseName is applied only on remote server. it seems that if local fs is 
                // case sensitive while remote is not, remote will regard two distinct files in local as duplicated files
                // and rename one of them while upload.
                string _key = cmisSyncFolder.CmisProfile.CmisProperties.IgnoreIfSameLowercaseNames ? semiTriplet.Name.ToLowerInvariant () : semiTriplet.Name;

                // if remote info is already crawled
                lock (orbLock) {
                    if (orderedRemoteBuffer.Contains(_key)) {

                        remoteTriplet = (SyncTriplet.SyncTriplet)orderedRemoteBuffer [_key];
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
                                    IDocument remoteDocument = (IDocument)remoteObject;

                                    SyncTripletFactory.AssembleRemoteIntoLocal (remoteDocument, remotePath, cmisSyncFolder, semiTriplet);
                                }
                            } catch (Exception) {
                                Console.WriteLine (" - remote path: {0} Not found", remotePath);
                            }
                        } else {
                            // TODO:
                            // Local exist , DB not exist, Remote ???
                        }
                    }
                }

                if (!fullSyncTriplets.TryAdd (semiTriplet)) {
                    Console.WriteLine (" - assembled triplet: {0} is not appended to full sync triplet list.", semiTriplet.Name);
                } 
 
                processedTriplets.Add (_key);
            }

            remoteCrawlTask.Wait ();

            // Assemble semiTriplets generated from remote crawler, except those
            // are already processed in the previous process.
            Console.WriteLine (" - Adding remained remote triplets");
            foreach (string key in orderedRemoteBuffer.Keys) {
                // if the triplet is already processed in local crawler, ignore
                if (processedTriplets.Contains (key)) {
                    //Console.WriteLine (" - key: {0}'s assigned remote-semitriplet is already pushed to processor, ignore. Check whether the server is case insensitive.", key);
                    continue;
                } else {
                    Console.WriteLine (" - key: {0}'s assigned remote-semitriplet is not processed yet, push to full sync trip", key);

                    SyncTriplet.SyncTriplet remoteTriplet = (SyncTriplet.SyncTriplet)orderedRemoteBuffer [key];

                    if (remoteTriplet == null) {
                        Console.WriteLine (" - assembled triplet: remote {0} is null.", key);
                        continue;
                    }

                    if (remoteTriplet.IsFolder) {
                        foreach (string dep in r_fdps.GetFolderDependences (remoteTriplet.Name)) fdps.AddFolderDependence (remoteTriplet.Name, dep);
                    }

                    if (!fullSyncTriplets.TryAdd (remoteTriplet)) {
                        Console.WriteLine (" - assembled triplet: {0} is not appended to full sync triplet list.", remoteTriplet.Name);
                    }
                }
            }

            // Clear the remote buffer after all objects are pushed to 
            // full synctriplet queue for the next syncing.
            remoteBuffer.Clear ();
            orderedRemoteBuffer.Clear ();
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
