using System;
using System.IO;

namespace CmisSync.Lib.Events
{
    public interface ISyncEvent
    {
        SyncEventType GetType();
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

        public SyncEventType GetType() {
            return SyncEventType.FileSystem;
        }

    }
}

