using log4net;

using System;
using System.Collections.Generic;

namespace CmisSync.Lib.Events
{
    /// <summary></summary>
    public class SyncEventManager
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(SyncEventManager));

        private List<SyncEventHandler> handler = new List<SyncEventHandler>();
        
        /// <summary></summary>
        public SyncEventManager()
        {
        }

        /// <summary></summary>
        /// <param name="h"></param>
        public void AddEventHandler(SyncEventHandler h)
        {
            //The zero-based index of item in the sorted List<T>, 
            //if item is found; otherwise, a negative number that 
            //is the bitwise complement of the index of the next 
            //element that is larger than item or.
            int pos = handler.BinarySearch(h);
            if(pos < 0){
                pos = ~pos;
            }
            handler.Insert(pos, h);
        }

        /// <summary></summary>
        /// <param name="e"></param>
        public virtual void Handle(ISyncEvent e) {
            for(int i = handler.Count-1; i >= 0; i--)
            {
                var h = handler[i];
                logger.Debug("Forwarding to Handler " + h);
                if(handler[i].Handle(e)){
                    return;
                }
            }
        }

        /// <summary></summary>
        /// <param name="h"></param>
        public void RemoveEventHandler(SyncEventHandler h)
        {
            handler.Remove(h);
        }
    }
}

