using System;

using log4net;

namespace CmisSync.Lib.Events
{
    /// <summary>Base class for all Event-Handlers</summary>
    public abstract class SyncEventHandler : IComparable<SyncEventHandler>, IComparable
    {
        /// <summary></summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public abstract bool Handle(ISyncEvent e);

        ///<summary>May not be changed during runtime</summary>
        public abstract int Priority {get;}

        /// <summary></summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(SyncEventHandler other) {
            return Priority.CompareTo(other.Priority);
        }

        // CompareTo is implemented for Sorting EventHandlers
        // Equals is not implemented because EventHandler removal shall work by Object.Equals
        int IComparable.CompareTo(object obj) {
            if(!(obj is SyncEventHandler)){
                throw new ArgumentException("Argument is not a SyncEventHandler", "obj");
            }
            SyncEventHandler other = obj as SyncEventHandler;
            return this.CompareTo(other);
        }

        /// <summary></summary>
        /// <returns></returns>
        public override string ToString() {
            return this.GetType() + " with Priority " + Priority.ToString();
        }
    }
}

