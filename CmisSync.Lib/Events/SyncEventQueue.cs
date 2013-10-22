using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CmisSync.Lib.Events
{
    public class SyncEventQueue {

        private BlockingCollection<ISyncEvent> queue = new BlockingCollection<ISyncEvent>();
        private SyncEventManager manager;
        private Task consumer;
        
        private static void Listen(BlockingCollection<ISyncEvent> queue, SyncEventManager manager){
            while (!queue.IsCompleted)
            {

                ISyncEvent syncEvent = null;
                // Blocks if number.Count == 0 
                // IOE means that Take() was called on a completed collection. 
                // Some other thread can call CompleteAdding after we pass the 
                // IsCompleted check but before we call Take.  
                // In this example, we can simply catch the exception since the  
                // loop will break on the next iteration. 
                try
                {
                    syncEvent = queue.Take();
                }
                catch (InvalidOperationException) { }

                if (syncEvent != null)
                {
                    manager.handle(syncEvent);
                }
            }
        }

        public SyncEventQueue(SyncEventManager manager) {
            this.manager = manager;
        }

        public void StartListener() {
            this.consumer = new Task(() => Listen(this.queue, this.manager));
        }

        public void StopListener() {
            this.queue.CompleteAdding();
        }            
        
        public bool IsStopped {
            get { return this.consumer == null || this.consumer.IsCompleted; }
        }
    }
}
