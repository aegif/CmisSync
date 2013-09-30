using System;
using System.IO;

namespace CmisSync.Lib
{
    public interface SyncEvent
    {
        object getSourceEvent();
        SyncEventType getType();
    }

    public enum SyncEventType {
            FILESYSTEM, CONTENTCHANGE, LOCALCRAWL, REMOTECRAWL
    }

    public abstract class FSEvent : SyncEvent
    {
        protected WatcherChangeTypes type;
        protected string path;
        public FSEvent(WatcherChangeTypes type, string path) {
            this.type = type;
            this.path = path;
        }

        public SyncEventType getType() {
            return SyncEventType.FILESYSTEM;
        }

        abstract public object getSourceEvent();
    }
}

