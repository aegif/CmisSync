using DotCMIS.Client;
using DotCMIS.Exceptions;
using System;

namespace CmisSync.Lib.Sync
{
    public enum EventLevel { INFO, WARN, ERROR }

    [Serializable]
    public class SyncronizerEvent
    {
        public DateTime Date { get; internal set; }
        public SyncFolderSyncronizerBase Source { get; internal set; }
        public Config.SyncConfig.SyncFolder SyncFolderInfo { get { return Source.SyncFolderInfo; } }
        public CmisBaseException Exception { get; internal set; }
        public EventLevel Level { get; internal set; }

        public SyncronizerEvent(SyncFolderSyncronizerBase source, CmisBaseException exception, EventLevel level)
            : this(DateTime.Now, source, exception, level) { }
        public SyncronizerEvent(DateTime date, SyncFolderSyncronizerBase source, CmisBaseException exception, EventLevel level)
        {
            this.Date = date;
            this.Source = source;
            this.Exception = exception;
            this.Level = level;
        }

        public override bool Equals(object obj)
        {
            if (this == obj) {
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
                return this.Source.GetHashCode() + 7 * this.Exception.GetHashCode();
            }
        }
    }
}

namespace DotCMIS.Exceptions
{
    [Serializable]
    public abstract class SyncException : CmisBaseException
    {
        public override string Message
        {
            get
            {
                return base.Message + (InnerException != null ? ": " + InnerException.Message : "");
            }
        }

        public SyncException(string message)
            : base(message) { }

        public SyncException(string message, Exception inner)
            : base(message, inner) { }

        public override bool Equals(object obj)
        {
            if (obj == this) { return true; }

            if (obj != null && obj.GetType() == this.GetType())
            {
                if (((SyncException)obj).Message.Equals(this.Message))
                {
                    return true;
                }
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode();
        }
    }

    [Serializable]
    public class UnhandledException : SyncException
    {
        public UnhandledException(Exception e)
            : base("UnhandledException", e) { }
    }

    [Serializable]
    public class NetworkException : SyncException
    {
        protected NetworkException(string message, Exception e) : base(message, e) { }
        public NetworkException(Exception e)
            : this("Network Error", e) { }
    }

    public class ServerNotFoundException : NetworkException
    {
        public Uri BestTryedUrl { get; internal set; }

        public ServerNotFoundException(Exception e) : base("Server not found", e) { }
    }

    [Serializable]
    public class RemoteObjectException : SyncException
    {
        public ICmisObject CmisObject { get; internal set; }

        public RemoteObjectException(ICmisObject cmisObject, Exception e)
            : base("Could not access remote object", e)
        {
            CmisObject = cmisObject;
        }
    }

    [Serializable]
    public abstract class FileException : SyncException
    {
        public String Path { get; internal set; }

        public override string Message
        {
            get
            {
                return base.Message + ": " + Path;
            }
        }

        public FileException(string path, string message)
            : base(message)
        {
            Path = path;
        }
        public FileException(string path, string message, Exception inner)
            : base(message, inner)
        {
            Path = path;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj) && ((FileException)obj).Path == this.Path;
        }

        public override int GetHashCode()
        {
            return CmisSync.Lib.SyncUtils.GetHashCode(base.GetHashCode(), this.Path);
        }
    }

    [Serializable]
    public class RemoteFileException : FileException
    {
        public RemoteFileException(string path, string message) : base(path, message) { }
        public RemoteFileException(string path, string message, Exception inner) : base(path, message, inner) { }
    }

    [Serializable]
    public class LocalFileException : FileException
    {
        public LocalFileException(string path, string message) : base(path, message) { }
        public LocalFileException(string path, string message, Exception inner) : base(path, message, inner) { }
    }

    [Serializable]
    public class MissingRootSyncFolderException : LocalFileException
    {
        public MissingRootSyncFolderException(String path) : base(path, "Local sync folder missing") { }
    }

    [Serializable]
    public class DownloadFileException : RemoteFileException
    {
        public DownloadFileException(string path) : this(path, "File download failed") { }
        protected DownloadFileException(string path, string message) : base(path, message) { }
        protected DownloadFileException(string path, string message, Exception inner) : base(path, message, inner) { }
        public DownloadFileException(string path, Exception inner) : this(path, "Download failed", inner) { }
    }

    [Serializable]
    public class DownloadFolderException : DownloadFileException
    {
        public DownloadFolderException(string path, Exception inner) : base(path, "Folder download failed", inner) { }
    }

    [Serializable]
    public class FetchMetadataFileException : DownloadFileException
    {
        public FetchMetadataFileException(string path, Exception e) : base(path, "Could not fetch metadata", e) { }
    }

    [Serializable]
    public class FileConflictException : DownloadFileException
    {
        public string ConflictingUser { get; internal set; }
        public string ConflictFilename { get; internal set; }

        public override String Message
        {
            get
            {
                return String.Format("User {0} added a file named {1} at the same time as you.", ConflictingUser, Path) + "\n" + 
                    "Your version has been renamed '" + ConflictFilename + "', please merge your important changes from it and then delete it.";
            }
        }

        public FileConflictException(string path, string user, string conflictFilename)
            : base(path)
        {
            ConflictingUser = user;
            ConflictFilename = conflictFilename;
        }
    }

    [Serializable]
    public class ConflictFileStillPresentException : LocalFileException
    { 
        public ConflictFileStillPresentException(string filename) : base(filename, "Conflict file still present. You should merge and delete it")
        { }
    }

    [Serializable]
    public class DirectoryCollisionFileException : DownloadFileException
    {
        public DirectoryCollisionFileException(string path) : base(path, "A Directory with the same file name exists") { }
    }

    [Serializable]
    public class DeleteLocalFolderException : DownloadFileException
    {
        public DeleteLocalFolderException(string path, Exception e) : base(path, "Could not delete local folder (tree)", e) { }
    }

    [Serializable]
    public class DeleteRemoteFolderException : DownloadFileException
    {
        public override string Message
        {
            get
            {
                if (InnerException is CmisPermissionDeniedException)
                {
                    return "You don't have the necessary permissions to delete folder " + Path
                          + "\nIf you feel you should be able to delete it, please contact your server administrator";
                }
                else
                {
                    return base.Message;
                }
            }
        }

        public DeleteRemoteFolderException(string path, Exception e) : base(path, "Could not delete remote folder", e) { }
    }

    [Serializable]
    public class UploadFileException : LocalFileException
    {
        public UploadFileException(string path) : this(path, "Could not upload file") { }
        protected UploadFileException(string path, string message) : base(path, message) { }
        protected UploadFileException(string path, string message, Exception inner) : base(path, message, inner) { }
        public UploadFileException(string path, Exception inner) : base(path, "Could not upload file", inner) { }
    }

    [Serializable]
    public class CreateRemoteDirectory : UploadFileException
    {
        protected CreateRemoteDirectory(string path, string message, Exception e) : base(path, message, e) { }
        public CreateRemoteDirectory(string path, Exception e) : this(path, "Could not create remote directory", e) { }
    }

    [Serializable]
    public class DirectoryCreationRemoteFileCollisionException : CreateRemoteDirectory
    {
        public DirectoryCreationRemoteFileCollisionException(string path, Exception e) : base(path, "Remote file conflict while creating remote folder", e) { }
    }

    [Serializable]
    public class UploadFolderException : UploadFileException
    {
        public UploadFolderException(string path, Exception e) : base(path, "Could not upload folder", e) { }
    }

    [Serializable]
    public class CheckOutFileException : UploadFileException
    {
        public string CheckinComment { get; internal set; }

        public CheckOutFileException(string path, string checkinComment)
            : base(path)
        {
            CheckinComment = checkinComment;
        }
    }

    [Serializable]
    public class MoveRemoteFileException : UploadFileException
    {
        public string NewPathname { get; internal set; }

        public override string Message
        {
            get
            {
                return String.Format(base.Message + ": {0} -> {1}", Path, NewPathname);
            }
        }

        protected MoveRemoteFileException(string oldPathname, string newPathname, string message, Exception e)
            : base(oldPathname, message, e)
        {
            NewPathname = newPathname;
        }

        public MoveRemoteFileException(string oldPathname, string newPathname, Exception e)
            : this(oldPathname, newPathname, "Unable to move the file", e) { }

        public override bool Equals(object obj)
        {
            return base.Equals(obj) && ((MoveRemoteFileException)obj).NewPathname == this.NewPathname;
        }

        public override int GetHashCode()
        {
            return CmisSync.Lib.SyncUtils.GetHashCode(base.GetHashCode(), this.NewPathname);
        }

    }

    [Serializable]
    public class MoveRemoteFolderException : MoveRemoteFileException
    {
        public MoveRemoteFolderException(string oldPathname, string newPathname, Exception e)
            : base(oldPathname, newPathname, "Unable to move the folder", e) { }
    }

    [Serializable]
    public class RenameRemoteFileException : MoveRemoteFileException
    {
        protected RenameRemoteFileException(string oldPathname, string newPathname, string message, Exception e)
            : base(oldPathname, newPathname, message, e)
        { }

        public RenameRemoteFileException(string oldPathname, string newPathname, Exception e)
            : this(oldPathname, newPathname, "Unable to rename the file", e)
        { }
    }

    [Serializable]
    public class RenameRemoteFolderException : MoveRemoteFileException
    {
        public RenameRemoteFolderException(string oldPathname, string newPathname, Exception e)
            : base(oldPathname, newPathname, "Unable to rename the folder", e)
        { }
    }
}
