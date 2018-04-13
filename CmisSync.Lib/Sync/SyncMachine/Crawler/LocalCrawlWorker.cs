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
     *     3.1. Always add the folder triplet to the semi-production queue BEFORE their contents' triplets if it is freshly created.
     *     3.2. BEAWARE WorkerOperion.UploadFile depends on this logic: if cmissync wants to upload a file but the folder is 
     *          not fould, it must be being created. 
     *     3.3. On the other hand, the folder triplets are pushed to the semi-production queue AFTER their content's triplets 
     *          if it is not newly created.
     *     3.4. (3.2) and (3.3) ensure we can use spin/sleep policy when doing creations/delections to wait the object dependencies
     *          to be satisfied.
     *  4. Object dependencies policy
     *     4.1. If obj == new && parent == new : dep(obj) add parent. means creation of obj depends on creation of its parent
     *     4.2. If obj == new && parnet != new : dep(parent add obj. means creation of obj does not dependes on its parents, 
     *          but possible deletion of parent depends on obj.
     *     4.3. If obj != new && parent != new : dep(parent) add obj. means possible deletion of parent depends on obj.
     *     4.4. Conclusion: if parent == new , add parent to dep(obj), else add obj to dep(parent).
     *     4.5. With this dependences construction, when creating a new object on the remote server, just check if its all deps 
     *          are properly satisfied. If its all satisified but we can not find its parent folder, its parent folder must be
     *          an 'Existed-but-Removed-Remotely' folder ( refer to 4.2, dep(obj) is empty because we didn't add its parent to it ).
     *          Thus we can not upload it and we info its parent as false == there is confliction if you sync delete it.
     *     4.6. This policy works only locally yet.
     */
    public class LocalCrawlWorker : IDisposable
    {

        private static readonly ILog Logger = LogManager.GetLogger (typeof (LocalCrawlWorker));

        private BlockingCollection<SyncTriplet.SyncTriplet> outputQueue = null;

        private ItemsDependencies itemsDeps = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private List<SyncTriplet.SyncTriplet> waitingSemi = null;

        public LocalCrawlWorker ( 
                                 CmisSyncFolder.CmisSyncFolder cmisSyncFolder, 
                                 BlockingCollection<SyncTriplet.SyncTriplet> queue,
                                 ItemsDependencies itemsDeps)
        {
            this.outputQueue = queue;
            this.cmisSyncFolder = cmisSyncFolder;
            this.itemsDeps = itemsDeps;
        }

        public void Start() {

            waitingSemi = new List<SyncTriplet.SyncTriplet> ();
            
            Console.WriteLine (" Start local file crawling from {0}", cmisSyncFolder.LocalPath);

            CrawlFolder (cmisSyncFolder.LocalPath);

            // deletes are always non-eq, priority.
            CrawlLocalDeleteds ();

            // local non-eq first, append local-eq later
            foreach (SyncTriplet.SyncTriplet semi in waitingSemi) {
                outputQueue.Add (semi);
            }

            Console.WriteLine (" Local crawling completed.");

            waitingSemi.Clear ();
        }

        public void StartFrom(String path) {

            waitingSemi = new List<SyncTriplet.SyncTriplet> ();

            Console.WriteLine (" Start local file crawling from {0}", path);

            CrawlFolder (path);
            foreach (SyncTriplet.SyncTriplet semi in waitingSemi) {
                outputQueue.Add (semi);
            }
            // the folder is not root, push it to process queue after all containing items are pushed.
            SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromLocalFolder (
                path, this.cmisSyncFolder
            );
            outputQueue.Add (triplet);

            Console.WriteLine (" Finish local file crawling from {0}", path);
            waitingSemi.Clear ();

        }

        private void CrawlFolder(String folder) {

            SyncTriplet.SyncTriplet dummyTriplet = SyncTripletFactory.CreateFromLocalFolder (folder, cmisSyncFolder);
            string parentName = dummyTriplet.Name;
            bool parentDBExist = folder.Equals(cmisSyncFolder.LocalPath) ? true : dummyTriplet.DBExist;

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
                if (parentDBExist) itemsDeps.AddItemDependence (parentName, triplet);
                else itemsDeps.AddItemDependence (triplet.Name, parentName);

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


                if (parentDBExist) itemsDeps.AddItemDependence (parentName, triplet);
                else itemsDeps.AddItemDependence (triplet.Name, parentName);

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
                    string parentFolderName = triplet.Name.Remove (triplet.Name.LastIndexOf ('/') + 1);

                    if (parentFolderName.Length > 0) {

                        itemsDeps.AddItemDependence (parentFolderName, triplet);
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
                        itemsDeps.AddItemDependence (parentFolderName, triplet);

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
