using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

using log4net;

using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Cmis;

namespace CmisSync.Lib.Sync.SyncWorker.Crawler
{
    /*
     *  LocalCrawer will try to add local-changed triplets before local-non-changed triplets to semiSyncTriplet Queue
     */
    public class LocalCrawlWorker:IDisposable
    {

        private static readonly ILog Logger = LogManager.GetLogger (typeof (LocalCrawlWorker));

        private BlockingCollection<SyncTriplet.SyncTriplet> semiSyncTriplets = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private List<SyncTriplet.SyncTriplet> waitingSemi = new List<SyncTriplet.SyncTriplet> ();

        public LocalCrawlWorker ( CmisSyncFolder.CmisSyncFolder cmisSyncFolder, BlockingCollection<SyncTriplet.SyncTriplet> queue)
        {
            this.semiSyncTriplets = queue;
            this.cmisSyncFolder = cmisSyncFolder;
        }

        public void Start() {

            Console.WriteLine (" Start local file crawling");
            CrawlFolder (cmisSyncFolder.LocalPath);
            Console.WriteLine (" Finish local file crawling");

            Console.WriteLine (" Start DB tranversing");
            // deletes are always non-eq, priority.
            CrawlLocalDeleteds ();
            Console.WriteLine (" Finish DB tranversing");

            // local non-eq first, append local-eq later
            foreach (SyncTriplet.SyncTriplet semi in waitingSemi) {
                semiSyncTriplets.Add (semi);
            }

            // complete adding will stop blockcollection foreach loop in 
            // synctriplet assembler
            semiSyncTriplets.CompleteAdding ();
        }

        private void CrawlFolder(String folder) {

            // filePath and folderPath are all full pathes.
            foreach( String filePath in Directory.GetFiles (folder)) {

                // ignore .sync file
                if (filePath.Contains (".sync")) continue;

                SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromLocalDocument (
                    filePath, this.cmisSyncFolder
                );

                if (triplet.LocalEqDB)
                    waitingSemi.Add (triplet);
                else
                    semiSyncTriplets.Add (triplet);
            }

            foreach (String folderPath in Directory.GetDirectories (folder)) {

                if (folderPath.Contains ("-conflict-version")) continue;
                
                SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromLocalFolder (
                    folderPath, this.cmisSyncFolder
                );

                if (triplet.LocalEqDB)
                    waitingSemi.Add (triplet);
                else
                    semiSyncTriplets.Add (triplet);


                CrawlFolder (folderPath);
            }
        }

        private void CrawlLocalDeleteds() {
            // GetLocalFolders2 return [localpath, remotepath] array
            foreach (string[] folder in cmisSyncFolder.Database.GetLocalFolders2())
            {
                if (!Directory.Exists(Utils.PathCombine(cmisSyncFolder.LocalPath, folder[0])))
                {
                    semiSyncTriplets.Add(
                        SyncTripletFactory.CreateSFGFromDBFolder (folder [0], folder [1], cmisSyncFolder));
                }
            }

            // GetLocalFiles2 return [localpath, remotepath] array
            foreach( string[] file in cmisSyncFolder.Database.GetLocalFiles2())
            {
                if (!File.Exists(Utils.PathCombine(cmisSyncFolder.LocalPath, file[0])))
                {
                    semiSyncTriplets.Add (
                        SyncTripletFactory.CreateSFGFromDBFile(file [0], file [1], cmisSyncFolder));
                }
            }
        }

        ~LocalCrawlWorker ()
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
