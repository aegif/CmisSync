using DotCMIS.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CmisSync.Lib.Sync
{
    public enum EventLevel { INFO, WARN, ERROR }

    [Serializable]
    public abstract class SyncronizerEvent
    {
        public DateTime Date { get; internal set; }
        public SyncFolderSyncronizerBase Source { get; internal set; }
        public Config.SyncConfig.SyncFolder SyncFolderInfo { get { return Source.SyncFolderInfo; } }
        private String _message;
        public CmisBaseException Exception { get; internal set; }
        public EventLevel Level { get; internal set; }

        public SyncronizerEvent(SyncFolderSyncronizerBase source, string message, EventLevel level)
            : this(source, (CmisBaseException)null, level)
        {
            _message = message;
        }
        public SyncronizerEvent(SyncFolderSyncronizerBase source, CmisBaseException exception, EventLevel level)
            : this(DateTime.Now, source, exception, level) { }
        public SyncronizerEvent(DateTime date, SyncFolderSyncronizerBase source, CmisBaseException exception, EventLevel level)
        {
            this.Date = date;
            this.Source = source;
            this.Exception = exception;
            this.Level = level;
        }

        public virtual String Message
        {
            get
            {
                string message = _message;
                if (Exception != null && !string.IsNullOrEmpty(Exception.Message))
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        message += ": ";
                    }
                    else
                    {
                        message = "";
                    }
                    message += Exception.Message;
                }
                return message;
            }
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }

            if (obj != null && obj.GetType() == this.GetType())
            {
                SyncronizerEvent ex = obj as SyncronizerEvent;
                return Object.Equals(this.Source, ex.Source) && Object.Equals(this.Exception, ex.Exception);
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return this.Source.GetHashCode() + 7 * Objects.GetHashCode(this.Exception);
            }
        }
    }

    [Serializable]
    public class SyncronizationStarted : SyncronizerEvent
    {
        public SyncronizationStarted(SyncFolderSyncronizerBase source)
            : base(source, "Syncronization started", EventLevel.INFO)
        { }
    }

    [Serializable]
    public class SyncronizationCancelledByUser : SyncronizerEvent
    {
        public SyncronizationCancelledByUser(SyncFolderSyncronizerBase source)
            : base(source, "Syncronization cancelled by the user", EventLevel.INFO)
        { }
    }

    //[Serializable]
    //public class FileUploadedEvent : SyncronizerEvent{
    //    public string LocalFilePath { get; internal set; }
    //    public string RemoteFilePath { get; internal set; }

    //    public override string Message
    //    {
    //        get
    //        {
    //            return base.Message + ": " + LocalFilePath + " to " + RemoteFilePath ;
    //        }
    //    }

    //    public FileUploadedEvent(SyncFolderSyncronizerBase source, string localFilePath, string remoteFilePath)
    //        : base(source, "File Uploaded", EventLevel.INFO)
    //    {
    //        LocalFilePath = localFilePath;
    //        RemoteFilePath = remoteFilePath;
    //    }
    //}

    //[Serializable]
    //public class FileDownloadedEvent : SyncronizerEvent
    //{        
    //    public string RemoteFilePath { get; internal set; }
    //    public string LocalFilePath { get; internal set; }

    //    public override string Message
    //    {
    //        get
    //        {
    //            return base.Message + ": " + RemoteFilePath + " to " + LocalFilePath;
    //        }
    //    }

    //    public FileDownloadedEvent(SyncFolderSyncronizerBase source, string remoteFilePath, string localFilePath)
    //        : base(source, "File Downloaded", EventLevel.INFO)
    //    {
        
    //        RemoteFilePath = remoteFilePath;
    //        LocalFilePath = localFilePath;
    //    }
    //}

    [Serializable]
    public class SyncronizationException : SyncronizerEvent
    {
        public SyncronizationException(SyncFolderSyncronizerBase source, CmisBaseException exception, EventLevel level)
            : base(source, exception, level)
        { }
    }

    [Serializable]
    public class SyncronizationComleted : SyncronizerEvent
    {
        public SyncronizationComleted(SyncFolderSyncronizerBase source)
            : base(source, "Syncronization completed", EventLevel.INFO)
        { }
    }
}
