using System;
using System.IO;

namespace CmisSync.Lib.Events
{
    public class FSEvent : ISyncEvent
    {
        public WatcherChangeTypes Type { get; private set; }

        public string Path { get; private set; }

        public FSEvent(WatcherChangeTypes type, string path) {
            Type = type;
            Path = path;
        }

        public virtual string ToString() {
            return string.Format("FSEvent with type \"{0}\" on path \"{1}\"", Type, Path);
        }
    }
}

