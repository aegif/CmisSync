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

        public void addEventHandler(ISyncEventHandler h)
        {
            int pos;
            for( pos = 0; pos < this.handler.Count; pos++) {
                if(h.getPriority() > handler[pos].getPriority())
                {
                    break;
                } else if(h.getPriority() == handler[pos].getPriority() && h==handler[pos])
                {
                    return;
                }
            }
            handler.Insert(pos, h);
        }

        public void handle(ISyncEvent e) {
            foreach ( ISyncEventHandler h in handler)
            {
                if(h.handle(e))
                    return;
            }
        }

        public void removeEventHandler(ISyncEventHandler h)
        {
            handler.Remove(h);
        }
    }
}

