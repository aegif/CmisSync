using System;
using System.IO;

namespace CmisSync.Lib.Events
{
    public interface ISyncEvent
    {
        SyncEventType getType();
    }

    public enum SyncEventType {
            FileSystem, ContentChange, LocalCrawl, RemoteCrawl
    };

    public abstract class FSEvent : ISyncEvent
    {
        protected WatcherChangeTypes type;
        protected string path;
        public FSEvent(WatcherChangeTypes type, string path) {
            this.type = type;
            this.path = path;
        }

        public SyncEventType getType() {
            return SyncEventType.FileSystem;
        }

    }
}

