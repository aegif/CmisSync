using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib.Events
{
    public delegate bool GenericSyncEventDelegate<TSyncEventType>(ISyncEvent e);
    public class GenericSyncEventHandler<TSyncEventType> : SyncEventHandler
    {
        private int priority;
        public override int Priority { get { return priority; } }
        private GenericSyncEventDelegate<TSyncEventType> Handler;

        public GenericSyncEventHandler(int priority, GenericSyncEventDelegate<TSyncEventType> handler)
        {
            this.priority = priority;
            Handler = handler;
        }

        public override bool Handle(ISyncEvent e)
        {
            if (e is TSyncEventType)
                return Handler(e);
            else 
                return false;
        }
    }
}
