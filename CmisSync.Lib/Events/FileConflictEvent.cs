using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib.Events
{
    class FileConflictEvent : ISyncEvent
    {
        /// <summary></summary>
        public FileConflictType Type { get; private set; }

        /// <summary></summary>
        public string AffectedPath { get; private set; }

        /// <summary></summary>
        public string CreatedConflictPath { get; private set; }

        /// <summary></summary>
        /// <param name="type"></param>
        /// <param name="affectedPath"></param>
        /// <param name="createdConflictPath"></param>
        public FileConflictEvent(FileConflictType type, string affectedPath, string createdConflictPath = null)
        {
            if (affectedPath == null)
            {
                throw new ArgumentNullException("Argument null in FileConflictEvent Constructor", "path");
            }
            Type = type;
            AffectedPath = affectedPath;
            CreatedConflictPath = createdConflictPath;
        }

        /// <summary></summary>
        /// <returns></returns>
        public override string ToString()
        {
            if(CreatedConflictPath == null )
                return string.Format("FileConflictEvent: \"{0}\" on path \"{1}\"", Type, AffectedPath);
            else
                return string.Format("FileConflictEvent: \"{0}\" on path \"{1}\" solved by creating path \"{2}\"", Type, AffectedPath, CreatedConflictPath);
        }
    }

    /// <summary></summary>
    public enum FileConflictType {
        /// <summary></summary>
        DELETED_REMOTE_FILE,
        /// <summary></summary>
        MOVED_REMOTE_FILE,
        /// <summary></summary>
        ALREADY_EXISTS_REMOTELY,
        /// <summary></summary>
        CONTENT_MODIFIED,
        /// <summary></summary>
        DELETED_REMOTE_PATH
    }
}
