using System;
using System.Collections.Generic;
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

        public List<string> IgnoredPaths;

        public long MaxDownloadRetries;

        public long MaxUploadRetries;

        public long MaxDeletionRetries;

        public CmisSyncFolder (RepoInfo repoInfo)
        {
            Name = repoInfo.Name;

            RemotePath = repoInfo.RemotePath;
            // Cmis path should not keep the last '/' but user's imput might
            if (RemotePath [RemotePath.Length - 1] == CmisUtils.CMIS_FILE_SEPARATOR) RemotePath = RemotePath.Remove (RemotePath.Length - 1);

            LocalPath = repoInfo.TargetDirectory;
            BIDIRECTIONAL = true;

            PollInterval = repoInfo.PollInterval;
            CmisProfile = new CmisProfileRefactor (repoInfo);
            Database = new Database.Database (repoInfo.CmisDatabase, LocalPath, RemotePath);

            IgnoredPaths = repoInfo.IgnoredPaths;
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
