using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using log4net;

namespace CmisSync.Lib.Events
{
    /// <summary></summary>
    public class SyncEventQueue : IDisposable {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SyncEventQueue));

        private BlockingCollection<ISyncEvent> queue = new BlockingCollection<ISyncEvent>();

        private SyncEventManager manager;

        private Task consumer;

        private bool alreadyDisposed = false;

        /// <summary>Constructor.</summary>
        public SyncEventQueue(SyncEventManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentException("manager may not be null");
            }
            this.manager = manager;

            // Start to listen in a separate thread.
            this.consumer = new Task(() => Listen(this.queue, this.manager));
            this.consumer.Start();
        }

        private static void Listen(BlockingCollection<ISyncEvent> queue, SyncEventManager manager)
        {
            Logger.Debug("Starting to listen on SyncEventQueue");
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
                    try{
                        manager.Handle(syncEvent);
                    }catch(Exception e) {
                        Logger.Error("Exception in EventHandler");
                        Logger.Error(e);
                    }
                }
            }
            Logger.Debug("Stopping to listen on SyncEventQueue");
        }

        /// <summary></summary>
        /// <param name="newEvent"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddEvent(ISyncEvent newEvent) {
            if(alreadyDisposed) {
                throw new ObjectDisposedException("SyncEventQueue", "Called AddEvent on Disposed object");
            }
            this.queue.Add(newEvent);
        } 

        /// <summary></summary>
        public void StopListener() {
            if(alreadyDisposed) {
                return;
            }
            this.queue.CompleteAdding();
        }            
        
        /// <summary></summary>
        public bool IsStopped {
            get { 
                return this.consumer.IsCompleted; 
            }
        }

        /// <summary></summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary></summary>
        /// <param name="isDisposing"></param>
        protected virtual void Dispose(bool isDisposing) {
            if(alreadyDisposed) {
                return;
            }
            if(!IsStopped){
                Logger.Warn("Disposing a not yet stopped SyncEventQueue");
            }
            if(isDisposing) {
                this.queue.Dispose();
            }
            this.alreadyDisposed = true;
        }
    }
}
