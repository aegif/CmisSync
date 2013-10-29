using System;
using System.Collections.Generic;

namespace CmisSync.Lib.Events
{
    public class SyncEventManager
    {
        private List<SyncEventHandler> handler = new List<SyncEventHandler>();
        public SyncEventManager()
        {
        }

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

        public virtual void Handle(ISyncEvent e) {
            foreach ( SyncEventHandler h in handler)
            {
                if(h.Handle(e))
                    return;
            }
        }

        public void RemoveEventHandler(SyncEventHandler h)
        {
            handler.Remove(h);
        }
    }
}

