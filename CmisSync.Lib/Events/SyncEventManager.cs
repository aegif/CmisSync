using System;
using System.Collections.Generic;

namespace CmisSync.Lib.Events
{
    public class SyncEventManager
    {
        private List<ISyncEventHandler> handler;
        public SyncEventManager()
        {
            handler = new List<ISyncEventHandler>();
        }

        public void AddEventHandler(ISyncEventHandler h)
        {
            int pos;
            for( pos = 0; pos < this.handler.Count; pos++) {
                if(h.GetPriority() > handler[pos].GetPriority())
                {
                    break;
                } else if(h.GetPriority() == handler[pos].GetPriority() && h==handler[pos])
                {
                    return;
                }
            }
            handler.Insert(pos, h);
        }

        public void Handle(ISyncEvent e) {
            foreach ( ISyncEventHandler h in handler)
            {
                if(h.Handle(e))
                    return;
            }
        }

        public void RemoveEventHandler(ISyncEventHandler h)
        {
            handler.Remove(h);
        }
    }
}

