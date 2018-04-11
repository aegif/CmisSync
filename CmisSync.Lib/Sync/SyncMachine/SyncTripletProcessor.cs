using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncMachine.ProcessWorker;
using CmisSync.Lib.Sync.SyncMachine.Internal;

using DotCMIS.Client;

namespace CmisSync.Lib.Sync.SyncMachine
{
    public class SyncTripletProcessor
    {
        private static int MaxParallelism = 4;

        private static readonly ILog Logger = LogManager.GetLogger (typeof (SyncTripletProcessor));

        private ISession session;

        /*
         * Problem:
         *     if set max upper bound in FullSyncTriplet
         * ,its TryAdd will not be blocked but directly return false.
         * 
         * Buzy wait with sleep?
         * 
         */
        private BlockingCollection<SyncTriplet.SyncTriplet> fullSyncTriplets = null; 

        private ConcurrentQueue<SyncTriplet.SyncTriplet> delayedFolerDeletions = new ConcurrentQueue<SyncTriplet.SyncTriplet> ();

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder = null;

        private FoldersDependencies foldersDeps = null;

        public SyncTripletProcessor (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session)
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.session = session;
        }

        public void Start (
            BlockingCollection<SyncTriplet.SyncTriplet> full,
            FoldersDependencies fdps
        ) {

            fullSyncTriplets = full;

            foldersDeps = fdps;

            delayedFolerDeletions = new ConcurrentQueue<SyncTriplet.SyncTriplet> ();

            ParallelOptions options = new ParallelOptions ();
            options.MaxDegreeOfParallelism = MaxParallelism;


            // Process non-folder-deletion operations
            Parallel.ForEach (Internal.SingleItemPartitioner.Create (fullSyncTriplets.GetConsumingEnumerable ()), options,
                              (triplet) => {
                                  // ============== new approaches ==============
                                  // Use folders dependencies class to record all folders' dependencies 
                                  // one folder should be processed only after all its dependencies are processed.
                                  // hint:
                                  // because all folder deletions are enqueued after its conttents, 
                                  // we can use spin() or sleep() to wait for that, just as create remote folder
                                  bool succeed = ProcessWorker.ProcessWorker.Process (triplet, session, cmisSyncFolder, foldersDeps);
                                  foldersDeps.RemoveFolderDependence (triplet.Name, succeed);

                              });

            //Console.WriteLine (" - Processing folder deletions");

            // Process folder-deletion operations 
            // One-by-One non-parallel approach yet

            //List<SyncTriplet.SyncTriplet> folderDeletionList = delayedFolerDeletions.ToList();

            // Lexicographical order
            // Therefore if there are files remained in the repository, eg. local removed but remote modified, vise versa
            // the corresponding folder will be remained. 

            //folderDeletionList.Sort (new ReverseLexicoGraphicalComparer<SyncTriplet.SyncTriplet> ());
            //DoParallelFolderDeletions (folderDeletionList);

            // reset delayed folder deletion buffer

            //delayedFolerDeletions = null;
        }
       
        private void DoParallelFolderDeletions(List<SyncTriplet.SyncTriplet> folders) {

            Console.WriteLine (" - Do parallel folder deletions: ");

            while (folders.Count > 0) {

                // everytime initialize a new full sync triplets;
                fullSyncTriplets = new BlockingCollection<SyncTriplet.SyncTriplet> ();
                Task parallelDeletionTask = Task.Factory.StartNew (() => ParallelFolderDeletionTask() );

                // reset Delayed property.
                folders[0].Delayed = false;

                if (!fullSyncTriplets.TryAdd (folders[0])) {
                    Console.WriteLine ("  - failed add : " + folders[0].Name);
                } else {
                    Console.WriteLine ("  - start parallel delete: " + folders[0].Name);
                }

                string lastFolderName = folders[0].Name;

                folders.RemoveAt (0);
                int i = 0; 
                while (i < folders.Count) {
                    if (lastFolderName.StartsWith(folders[i].Name)) {
                        // This folder can not be processed concurrently with 
                        // the last folder name, increase index
                        i++;
                    } else {
                        // This foler can be processed, remove from list and push 
                        // to fullsynctriplet
                        lastFolderName = folders [i].Name;

                        // Set delayed to false
                        folders [i].Delayed = false;

                        if (!fullSyncTriplets.TryAdd (folders [i])) {
                            Console.WriteLine ("  - failed add : " + folders [i].Name);
                        } else {
                            Console.WriteLine ("  - start parallel delete: " + folders [i].Name);
                        }

                        folders.RemoveAt (i);
                    }
                }

                fullSyncTriplets.CompleteAdding ();

                parallelDeletionTask.Wait ();

                Console.WriteLine ("  - one loop of parallel folder deletions completed. start next loop");
            }
        }

        private void ParallelFolderDeletionTask() {

            ParallelOptions options = new ParallelOptions ();
            options.MaxDegreeOfParallelism = MaxParallelism;

            // Process parallel folder deletion
            Parallel.ForEach (Internal.SingleItemPartitioner.Create (fullSyncTriplets.GetConsumingEnumerable ()), options,
                              (triplet) => ProcessWorker.ProcessWorker.Process (triplet, session, cmisSyncFolder, new ConcurrentQueue<SyncTriplet.SyncTriplet>() )
                             );

        }

        ~SyncTripletProcessor ()
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
                        //FullSyncTriplets.Dispose();
                    }
                    this.disposed = true;
                }
            }
        }
    }
}
