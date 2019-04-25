using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncMachine.ProcessWorker;
using CmisSync.Lib.Sync.SyncMachine.Internal;

using DotCMIS.Client;

namespace CmisSync.Lib.Sync.SyncMachine
{

    /// <summary>
    /// The sync triplet processor parallelly pops synctriplet from fullSynTriplets queue and processes
    /// them in a parallel pattern.
    /// </summary>
    public class SyncTripletProcessor
    {
        private static int MaxParallelism = 4;

        private static readonly ILog Logger = LogManager.GetLogger (typeof (SyncTripletProcessor));

        private ISession session;

        private BlockingCollection<SyncTriplet.SyncTriplet> fullSyncTriplets = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder = null;

        private ItemsDependencies itemsDeps = null;

        private ProcessorCompleteAddingChecker completeChecker = null;

        private ConcurrentDictionary<String, int> retryCounter = null;

        public SyncTripletProcessor (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session)
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.session = session;
        }

        /// <summary>
        /// Given synctriplets queue and their dependencies, do synchronizing work.
        /// </summary>
        /// <param name="full">Full synctriplets queue.</param>
        /// <param name="idps">Items dependencies of synctriplets in the queue.</param>
        /// <param name="checker">Shared processor complete adding checker with crawlers. </param>
        public void Start (
            BlockingCollection<SyncTriplet.SyncTriplet> full,
            ItemsDependencies idps,
            ProcessorCompleteAddingChecker checker
        )
        {

            fullSyncTriplets = full;

            itemsDeps = idps;

            completeChecker = checker;

            retryCounter = new ConcurrentDictionary<string, int> ();
             
            ParallelOptions options = new ParallelOptions ();
            options.MaxDegreeOfParallelism = MaxParallelism;


            /*
             * Parallel.ForEach on BlockingCollection requires a partitioner which can be found in Microsoft's document.
             * Furthermore, ForEach operation must be applied on BlockingCollection's GetConsumingEnumerable() method's
             * returns. If ForEach is directly applied to Partitioner.Create(fullSyncTriplets), the ForEach loop can not
             * handle any items pushed to the fullSyncTriplets after the Parallel.ForEach line is called.            
             */
            Parallel.ForEach (Internal.SingleItemPartitioner.Create (fullSyncTriplets.GetConsumingEnumerable ()), options,
                (triplet) => {

                    /*
                     * There exist a dummy triplet whose Name is empty for triggering ForEach loop. Ignore it.
                     */
                    if (triplet.Name != "") {

                        // Do the synchronizing job and get its return
                        SyncResult syncRes = ProcessWorker.ProcessWorker.Process (triplet, session, cmisSyncFolder, itemsDeps);


                        /*
                         * If the synchronizing reuslt is UNRESOLVED, one should return the triplet back to full-synctriplets queue.
                         *   However, especially when there are very few triplets remained in the queue, there is a problem that if a 
                         * triplet is UNRESOLVED, it will be pushed back to the queue for many(says, thousands) times while waiting 
                         * for its dependencies to be solved(usually creating remote folder or deleting remote files), which is 
                         * relatively slow. 
                         *   For example, when only folder1(delete), folder1/file1(delete), folder2/file2(delete) are
                         * remained but the option.MaxDegreeOfParallelism is 4, it is possible that:
                         *  - thread1: delete folder1, UNRESOLVED, push back to queue
                         *  - thread2: delete folder1/file1, it's slow and will take a few milliseconds
                         *  - thread3: delete folder1/file2, it's slow and will take a few milliseconds
                         * You can see that during deleting folder1/file1 and folder1/file2, folder1 is alwasy UNRESOLVED and will 
                         * continuously been popped from / pushed back to the queue, resulting in a busy-waiting policy.
                         *                         
                         * Therefore, inspired by SpinWait, we increase retryCounter by 1 everytime the folder1 is pushed back to the queue. 
                         * If the retryCounter exceeds some threshold, do a sleep wait to release resources.
                         * 
                         * If the synchronizing result is NOT UNRESOLVED, remove it from each object's dependence list in idps where 
                         * idps[x] includes this triplet.
                         */
                        if (syncRes == SyncResult.UNRESOLVED) {
                            if (!retryCounter.ContainsKey (triplet.Name)) {
                                Console.WriteLine (" P [ WorkerThread: {0} ] processing triplet [ {1} ]'s result = {2}",
                                                     Thread.CurrentThread.ManagedThreadId, triplet.Name, syncRes);
                                retryCounter [triplet.Name] = 1;
                            } else {
                                retryCounter [triplet.Name]++;
                                if (retryCounter [triplet.Name] > 5) Thread.Sleep (100);
                            }

                            // If UNRESOLVED, push the synctriplet back to queue.
                            fullSyncTriplets.TryAdd (triplet);
                        } else {
                            Console.WriteLine (" P [ WorkerThread: {0} ] processing triplet [ {1} ]'s result = {2}",
                                             Thread.CurrentThread.ManagedThreadId, triplet.Name, syncRes);
                            itemsDeps.RemoveItemDependence (triplet.Name, syncRes);
                        }

                    } else {
                        // Console.WriteLine (" P [ WorkerThread: {0} ] has get a dummy triplet. ", Thread.CurrentThread.ManagedThreadId);
                    }

                    if (checker.processorCompleteAdding ()) {

                        /*
                         * In every processing thread, one should check if the idps is empty and the 
                         * crawlers have stopped. If yes, the thread can tell the Parallel.ForEach that 
                         * there is no more incoming triplet to the full-synctriplet queue. The ForEach 
                         * loop will exist after all current threads have finished.                        
                         */                        
                        if (!fullSyncTriplets.IsAddingCompleted) {
                             Console.WriteLine (" P [ WorkerThread: {0} ] all full-sync-triplets are processed. Set fullSyncTriplets queue CompleteAdding().",
                                Thread.CurrentThread.ManagedThreadId);
                            fullSyncTriplets.CompleteAdding ();
                        } else {
                            // Console.WriteLine (" P [ WorkerThread: {0} ] FullSyncTriplets queue is already IsAddingCompleted.",
                            //    Thread.CurrentThread.ManagedThreadId);
                        }
                    }
                });
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
