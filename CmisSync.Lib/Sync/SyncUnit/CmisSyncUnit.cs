using System;
using log4net;
using CmisSync.Lib;
using CmisSync.Lib.Sync.CmisRepoFolder;
using CmisSync.Lib.ActivityListener;
using CmisSync.Lib.Sync.SyncRepo;

namespace CmisSync.Lib.Sync
{
    public class CmisSyncUnit //: RepoBase
    {
        /*
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger (typeof (CmisSyncUnit));

        /// <summary>
        /// The repo info.
        /// </summary>
        private RepoInfo repoInfo;

        /// <summary>
        /// Track whether <c>Dispose</c> has been called.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisSyncUnit (RepoInfo repoInfo, IActivityListener activityListener, bool enableWatcher)
            : base(repoInfo, activityListener, enableWatcher)
        {
            Logger.Info (synchronizedFolder);
        }


        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        protected override void Dispose (bool disposing)
        {
            if (!this.disposed) {
                if (disposing) {
                    this.synchronizedFolder.Dispose ();
                }
                this.disposed = true;
            }
            base.Dispose (disposing);
        }

        /// <summary>
        /// Whether this folder's synchronization is running right now.
        /// </summary>
        public override bool isSyncing ()
        {
            return this.synchronizedFolder.isSyncing ();
        }

        /// <summary>
        /// Whether this folder's synchronization is suspended right now.
        /// </summary>
        public override bool isSuspended ()
        {
            return this.synchronizedFolder.isSuspended ();
        }

        /// <summary>
        /// Synchronize.
        /// The synchronization is performed in the background, so that the UI stays usable.
        /// </summary>
        public override void SyncInBackground ()
        {
            if (this.synchronizedFolder != null)// Because it is sometimes called before the object's constructor has completed.
            {
                if (this.Enabled) {
                    this.synchronizedFolder.SyncInBackground ();
                } else {
                    Logger.Info (String.Format ("Repo {0} - Sync skipped. Status={1}", this.Name, this.Enabled));
                }
            }
        }

        /// <summary>
        /// Synchonize.
        /// The synchronization is performed synchronously.
        /// </summary>
        /// <param name="syncFull"></param>
        public bool SyncInForeground ()
        {
            if (synchronizedFolder == null) {
                return false;
            } else {
                if (Enabled) {
                    return synchronizedFolder.SyncInForeground ();
                } else {
                    Logger.Info (String.Format ("Repo {0} - Sync skipped.Status={1}", this.Name, this.Enabled));
                    return true;
                }
            }
        }


        /// <summary>
        /// Update repository settings.
        /// </summary>
        public override void UpdateSettings (string password, int pollInterval, bool syncAtStartup)
        {
            base.UpdateSettings (password, pollInterval, syncAtStartup);
            synchronizedFolder.UpdateSettings (RepoInfo);
            Logger.Info ("Updated sync settings. Restarting sync.");
            SyncInBackground ();
        }

        /// <summary>
        /// Cancel the currently running sync.  Returns once the current blocking operation completes.
        /// </summary>
        public override void CancelSync ()
        {
            synchronizedFolder.CancelSync ();
        }

        /// <summary>
        /// Size of the synchronized folder in bytes.
        /// Obtained by adding the individual sizes of all files, recursively.
        /// </summary>
        public override double Size {
            get {
                return 1234567; // TODO do we really need this size feature?
            }
        }
        */
    }
}
