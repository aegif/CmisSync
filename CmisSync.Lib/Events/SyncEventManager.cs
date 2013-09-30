using System;
using System.Collections.Generic;

namespace CmisSync.Lib
{
    public class SyncEventManager
    {
        private List<SyncEventHandler> handler;
        public SyncEventManager()
        {
            handler = new List<SyncEventHandler>();
        }

        public void addEventHandler(SyncEventHandler h)
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

        public void handle(SyncEvent e) {
            foreach ( SyncEventHandler h in handler)
            {
                if(h.handle(e))
                    return;
            }
        }

        public void removeEventHandler(SyncEventHandler h)
        {
            handler.Remove(h);
        }
    }
}

