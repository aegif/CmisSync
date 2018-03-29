using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

using log4net;

using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Utilities.FileUtilities;
using CmisSync.Lib.Cmis;

namespace CmisSync.Lib.Sync.SyncMachine.Crawler
{
    /*
     *  1. LocalCrawler will try to add local-changed triplets before local-non-changed triplets to semiSyncTriplet Queue
     *  2. LocalCrawler has tow modes:
     *     2.1. queue = semiSyncTriplet, it is for full crawl sync. Triplets created fron local files are sent to the Assembler
     *          to get remote information, then to processor.
     *     2.2. queue = fullSyncTriplet, it is for change log deletion sync. Triplets are directly sent to fullSyncTriplet with
     *          null remote storage ( = deleted remotely ).
     */
    public class LocalCrawlWorker:IDisposable
    {

        private static readonly ILog Logger = LogManager.GetLogger (typeof (LocalCrawlWorker));

        private BlockingCollection<SyncTriplet.SyncTriplet> outputQueue = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private List<SyncTriplet.SyncTriplet> waitingSemi = new List<SyncTriplet.SyncTriplet> ();

        public LocalCrawlWorker ( CmisSyncFolder.CmisSyncFolder cmisSyncFolder, BlockingCollection<SyncTriplet.SyncTriplet> queue)
        {
            this.outputQueue = queue;
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
                outputQueue.Add (semi);
            }

            waitingSemi.Clear ();
        }

        public void StartFrom(String path) {

            CrawlFolder (path);

            foreach (SyncTriplet.SyncTriplet semi in waitingSemi) {
                outputQueue.Add (semi);
            }

            // the folder is not root, push it to process queue after all containing items are pushed.
            SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromLocalFolder (
                path, this.cmisSyncFolder
            );
            outputQueue.Add (triplet);

            waitingSemi.Clear ();
        }

        private void CrawlFolder(String folder) {

            // filePath and folderPath are all full pathes.
            foreach( String filePath in Directory.GetFiles (folder)) {

                if (!SyncFileUtil.WorthSyncing (filePath, cmisSyncFolder)) {
                    Console.WriteLine (" - {0} is ignored: ", filePath);
                    continue;
                }

                SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromLocalDocument (
                    filePath, this.cmisSyncFolder
                );

                if (triplet.LocalEqDB)
                    waitingSemi.Add (triplet);
                else
                    outputQueue.Add (triplet);
            }

            foreach (String folderPath in Directory.GetDirectories (folder)) {

                //if (folderPath.Contains ("-conflict-version")) continue;
                if (!SyncFileUtil.WorthSyncing (folderPath, cmisSyncFolder)) {
                    Console.WriteLine (" - {0} is ignored: ", folderPath);
                    continue;
                }

                
                SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromLocalFolder (
                    folderPath, this.cmisSyncFolder
                );

                if (triplet.LocalEqDB)
                    waitingSemi.Add (triplet);
                else
                    outputQueue.Add (triplet);


                CrawlFolder (folderPath);
            }
        }

        private void CrawlLocalDeleteds() {
            // GetLocalFolders2 return [localpath, remotepath] array
            foreach (string[] folder in cmisSyncFolder.Database.GetLocalFolders2())
            {
                if (!Directory.Exists(Utils.PathCombine(cmisSyncFolder.LocalPath, folder[0])))
                {
                    Console.WriteLine (" - {0} is deleted: ", folder[0]);
                    outputQueue.Add(
                        SyncTripletFactory.CreateSFGFromDBFolder (folder [0], folder [1], cmisSyncFolder));
                }
            }

            // GetLocalFiles2 return [localpath, remotepath] array
            foreach( string[] file in cmisSyncFolder.Database.GetLocalFiles2())
            {
                if (!File.Exists(Utils.PathCombine(cmisSyncFolder.LocalPath, file[0])))
                {
                    Console.WriteLine (" - {0} is deleted: ", file [0]);
                    outputQueue.Add (
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
