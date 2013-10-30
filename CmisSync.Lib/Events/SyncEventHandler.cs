using System;

using log4net;

namespace CmisSync.Lib.Events
{
    public abstract class SyncEventHandler : IComparable<SyncEventHandler>, IComparable
    {
        public abstract bool Handle(ISyncEvent e);
        public abstract int Priority {get;}

        public int CompareTo(SyncEventHandler other) {
            return Priority.CompareTo(other.Priority);
        }

        int IComparable.CompareTo(object obj) {
            if(!(obj is SyncEventHandler)){
                throw new ArgumentException("Argument is not a SyncEventHandler", "obj");
            }
            SyncEventHandler other = obj as SyncEventHandler;
            return this.CompareTo(other);
        }

        public override string ToString() {
            return this.GetType() + " with Priority " + Priority.ToString();
        }
    }
}

