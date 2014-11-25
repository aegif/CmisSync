using System;
using System.IO;

namespace CmisSync.Lib.Events
{
    /// <summary></summary>
    public class FSEvent : ISyncEvent
    {
        /// <summary>
        /// Type of change.
        /// For instance: Created, Deleted, Changed, Renamed, All.
        /// </summary>
        public WatcherChangeTypes Type { get; private set; }

        /// <summary>
        /// Path of the changed file/folder.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// A file system event.
        /// For instance, a file was renamed.
        /// </summary>
        public FSEvent(WatcherChangeTypes type, string path) {
            if(path == null) {
                throw new ArgumentNullException("Argument null in FSEvent Constructor","path");
            }
            Type = type;
            Path = path;
        }

        /// <summary></summary>
        /// <returns></returns>
        public override string ToString() {
            return string.Format("FSEvent with type \"{0}\" on path \"{1}\"", Type, Path);
        }
    }
}

