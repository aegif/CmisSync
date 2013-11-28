using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib.Events
{
    class FileConflictEvent : ISyncEvent
    {
        public FileConflictType Type { get; private set; }

        public string AffectedPath { get; private set; }

        public string CreatedConflictPath { get; private set; }

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

        public override string ToString()
        {
            if(CreatedConflictPath == null )
                return string.Format("FileConflictEvent: \"{0}\" on path \"{1}\"", Type, AffectedPath);
            else
                return string.Format("FileConflictEvent: \"{0}\" on path \"{1}\" solved by creating path \"{2}\"", Type, AffectedPath, CreatedConflictPath);
        }
    }

    public enum FileConflictType {
        DELETED_REMOTE_FILE,
        MOVED_REMOTE_FILE,
        ALREADY_EXISTS_REMOTELY,
        CONTENT_MODIFIED,
        DELETED_REMOTE_PATH
    }
}
