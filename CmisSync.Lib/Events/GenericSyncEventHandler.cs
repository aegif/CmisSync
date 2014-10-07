using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib.Events
{
    /// <summary></summary>
    /// <typeparam name="TSyncEventType"></typeparam>
    /// <param name="e"></param>
    /// <returns></returns>
    public delegate bool GenericSyncEventDelegate<TSyncEventType>(ISyncEvent e);

    /// <summary></summary>
    /// <typeparam name="TSyncEventType"></typeparam>
    public class GenericSyncEventHandler<TSyncEventType> : SyncEventHandler
    {
        private int priority;
        
        /// <summary></summary>
        public override int Priority { get { return priority; } }

        private GenericSyncEventDelegate<TSyncEventType> Handler;

        /// <summary></summary>
        /// <param name="priority"></param>
        /// <param name="handler"></param>
        public GenericSyncEventHandler(int priority, GenericSyncEventDelegate<TSyncEventType> handler)
        {
            this.priority = priority;
            Handler = handler;
        }

        /// <summary></summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public override bool Handle(ISyncEvent e)
        {
            if (e is TSyncEventType)
                return Handler(e);
            else 
                return false;
        }
    }
}
