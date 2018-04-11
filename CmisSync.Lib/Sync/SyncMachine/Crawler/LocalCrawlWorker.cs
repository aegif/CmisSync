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
    /*
     *  1. LocalCrawler will try to add local-changed triplets before local-non-changed triplets to semiSyncTriplet Queue
     *  2. LocalCrawler has tow modes:
     *     2.1. queue = semiSyncTriplet, it is for full crawl sync. Triplets created fron local files are sent to the Assembler
     *          to get remote information, then to processor.
     *     2.2. queue = fullSyncTriplet, it is for change log deletion sync. Triplets are directly sent to fullSyncTriplet with
     *          null remote storage ( = deleted remotely ).
     *  3. LocalCrawler 
     *     3.1. Always add the folder triplet to the semi-production queue BEFORE their contents' triplets if it is newly created.
     *     3.2. BEAWARE WorkerOperion.UploadFile depends on this logic: if cmissync wants to upload a file but the folder is 
     *          not fould, it must be being created.
     *     3.3. On the other hand, the folder triplets are pushed to the semi-production queue AFTER their content's triplets 
     *          if it is not newly creted.
     */
    public class LocalCrawlWorker : IDisposable
    {

        private static readonly ILog Logger = LogManager.GetLogger (typeof (LocalCrawlWorker));

        private BlockingCollection<SyncTriplet.SyncTriplet> outputQueue = null;

        private FoldersDependencies foldersDeps = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private List<SyncTriplet.SyncTriplet> waitingSemi = null;

        public LocalCrawlWorker ( 
                                 CmisSyncFolder.CmisSyncFolder cmisSyncFolder, 
                                 BlockingCollection<SyncTriplet.SyncTriplet> queue,
                                 FoldersDependencies foldersDeps)
        {
            this.outputQueue = queue;
            this.cmisSyncFolder = cmisSyncFolder;
            this.foldersDeps = foldersDeps;
        }

        public void Start() {

            waitingSemi = new List<SyncTriplet.SyncTriplet> ();
            
            Console.WriteLine (" Start local file crawling from {0}", cmisSyncFolder.LocalPath);
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

            waitingSemi = new List<SyncTriplet.SyncTriplet> ();

            Console.WriteLine (" Start local file crawling from {0}", path);
            CrawlFolder (path);
            Console.WriteLine (" Finish local file crawling");

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

            string folderName = SyncTripletFactory.CreateFromLocalFolder (folder, cmisSyncFolder).Name;

            // filePath and folderPath are all full pathes.
            foreach( String filePath in Directory.GetFiles (folder)) {

                if (!SyncFileUtil.WorthSyncing (filePath, cmisSyncFolder)) {
                    Console.WriteLine (" - {0} is ignored: ", filePath);
                    continue;
                }

                SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromLocalDocument (
                    filePath, this.cmisSyncFolder
                );

                // folder deps should be added before put to queue
                foldersDeps.AddFolderDependence (folderName, triplet);

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


                foldersDeps.AddFolderDependence (folderName, triplet);

                // LocalCrawler always add the folder triplet to the semi - production queue BEFORE their contents' triplets if it is newly created.
                // On the other hand, the folder triplets are pushed to the semi-production queue AFTER their content's triplets 
                if (!triplet.DBExist) {

                    outputQueue.Add (triplet);
                    CrawlFolder (folderPath);

                } else {

                    CrawlFolder (folderPath);

                    if (triplet.LocalEqDB) {
                        waitingSemi.Add (triplet);
                    } else {
                        outputQueue.Add (triplet);
                    }
                }
            }

        }

        // files first, followed by folders
        private void CrawlLocalDeleteds() {

            // GetLocalFiles2 return [localpath, remotepath] array
            foreach( string[] file in cmisSyncFolder.Database.GetLocalFiles2())
            {
                if (!File.Exists(Utils.PathCombine(cmisSyncFolder.LocalPath, file[0])))
                {
                    Console.WriteLine (" - File {0} is deleted: ", file [0]);
                    SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromDBFile(file [0], file [1], cmisSyncFolder);

                    // get files's parent's name
                    string folderName = triplet.Name.Remove (triplet.Name.LastIndexOf ('/') + 1);

                    if (folderName.Length > 0) {

                        foldersDeps.AddFolderDependence (folderName, triplet);
                   }

                    outputQueue.Add (triplet);
                }
            }

            List<SyncTriplet.SyncTriplet> tmp = new List<SyncTriplet.SyncTriplet> ();

            // GetLocalFolders2 return [localpath, remotepath] array
            foreach (string [] folder in cmisSyncFolder.Database.GetLocalFolders2 ()) {
                if (!Directory.Exists (Utils.PathCombine (cmisSyncFolder.LocalPath, folder [0]))) 
                {

                    Console.WriteLine (" - Folder {0} is deleted: ", folder [0]);
                    SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromDBFolder (folder [0], folder [1], cmisSyncFolder);

                    // get folder's parent name
                    string parentFolderName = triplet.Name.Remove ((triplet.Name.Remove (triplet.Name.LastIndexOf ('/'))).LastIndexOf ('/') + 1);
                    // if pareentFolder is / , it would be "" because the Name does not start with / : /a/b/c/ .Name = a/b/c/
                    // root folder should be ignored in dependencies 
                    if (parentFolderName.Length > 0) {

                        // if not do so, the hashset is not thread-safety
                        foldersDeps.AddFolderDependence (parentFolderName, triplet);

                    }

                    tmp.Add (triplet);

                }
            }
            // ReversedLexicographic Order
            tmp.Sort (new ReverseLexicoGraphicalComparer<SyncTriplet.SyncTriplet> ());
            foreach (SyncTriplet.SyncTriplet triplet in tmp) outputQueue.Add (triplet);

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
