using System;
using CmisSync.Lib;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Database;
using CmisSync.Lib.Sync;

using DotCMIS.Client;

namespace CmisSync.Lib.Sync.CmisSyncFolder
{
    public class CmisSyncFolder : IDisposable
    {
        public string Name;

        public string RemotePath;

        public IFolder RemoteRootFolder = null;

        public string LocalPath;

        public bool BIDIRECTIONAL;

        public CmisProfileRefactor CmisProfile;

        public Database.Database Database;

        public bool IsSyncing;

        public bool IsSuspended;

        public double PollInterval;

        public long MaxDownloadRetries;

        public long MaxUploadRetries;

        public long MaxDeletionRetries;

        public CmisSyncFolder (RepoInfo repoInfo)
        {
            Name = repoInfo.Name;
            RemotePath = repoInfo.RemotePath;
            LocalPath = repoInfo.TargetDirectory;
            BIDIRECTIONAL = true;
            PollInterval = repoInfo.PollInterval;
            CmisProfile = new CmisProfileRefactor (repoInfo);
            Database = new Database.Database (repoInfo.CmisDatabase, LocalPath, RemotePath);
            MaxDownloadRetries = repoInfo.MaxDownloadRetries;
            MaxUploadRetries = repoInfo.MaxUploadRetries;
            MaxDeletionRetries = repoInfo.MaxDeletionRetries;
        }

        ~CmisSyncFolder()
        {
            Dispose (false);
        }

        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        private Object disposeLock = new object ();
        private bool disposed = false;
        protected virtual void Dispose (bool disposing)
        {
            lock (disposeLock) {
                if (!this.disposed) {
                    if (disposing) {
                        this.Database.Dispose ();
                    }
                    this.disposed = true;
                }
            }
        }
    }
}
