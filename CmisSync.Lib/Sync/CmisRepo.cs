//   CmisSync, a CMIS synchronization tool.
//   Copyright (C) 2012  Nicolas Raoul <nicolas.raoul@aegif.jp>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.

using log4net;
using System;
using System.IO;
using CmisSync.Lib.Sync.CmisRepoFolder;

namespace CmisSync.Lib.Sync
{
    /// <summary></summary>
    public partial class CmisRepo : RepoBase
    {
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger (typeof (CmisRepo));

        /// <summary>
        /// Remote folder to synchronize.
        /// </summary>
        private SynchronizedFolder synchronizedFolder;

        /// <summary>
        /// Track whether <c>Dispose</c> has been called.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisRepo(RepoInfo repoInfo, IActivityListener activityListener, bool enableWatcher)
            : base(repoInfo, activityListener, enableWatcher)
        {
            this.synchronizedFolder = new SynchronizedFolder(repoInfo, this, activityListener);
            Logger.Info(synchronizedFolder);
        }


        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.synchronizedFolder.Dispose();
                }
                this.disposed = true;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Whether this folder's synchronization is running right now.
        /// </summary>
        public override bool isSyncing()
        {
            return this.synchronizedFolder.isSyncing();
        }

        /// <summary>
        /// Whether this folder's synchronization is suspended right now.
        /// </summary>
        public override bool isSuspended()
        {
            return this.synchronizedFolder.isSuspended();
        }

        /// <summary>
        /// Synchronize.
        /// The synchronization is performed in the background, so that the UI stays usable.
        /// </summary>
        public override void SyncInBackground()
        {
            if (this.synchronizedFolder != null)// Because it is sometimes called before the object's constructor has completed.
            {
                if (this.Enabled)
                {
                    this.synchronizedFolder.SyncInBackground();
                }
                else
                {
                    Logger.Info(String.Format("Repo {0} - Sync skipped. Status={1}", this.Name, this.Enabled));
                }
            }
        }

        /// <summary>
        /// Synchonize.
        /// The synchronization is performed synchronously.
        /// </summary>
        /// <param name="syncFull"></param>
        public bool SyncInForeground()
        {
            if (synchronizedFolder == null)
            {
                return false;
            }
            else
            {
                if (Enabled)
                {
                    return synchronizedFolder.SyncInForeground();
                }
                else
                {
                    Logger.Info(String.Format("Repo {0} - Sync skipped.Status={1}", this.Name, this.Enabled));
                    return true;
                }
            }
        }


        /// <summary>
        /// Update repository settings.
        /// </summary>
        public override void UpdateSettings(string password, int pollInterval, bool syncAtStartup)
        {
            base.UpdateSettings(password, pollInterval, syncAtStartup);
            synchronizedFolder.UpdateSettings(RepoInfo);
            Logger.Info("Updated sync settings. Restarting sync.");
            SyncInBackground();
        }

        /// <summary>
        /// Cancel the currently running sync.  Returns once the current blocking operation completes.
        /// </summary>
        public override void CancelSync()
        {
            synchronizedFolder.CancelSync();
        }

        /// <summary>
        /// Size of the synchronized folder in bytes.
        /// Obtained by adding the individual sizes of all files, recursively.
        /// </summary>
        public override double Size
        {
            get
            {
                return 1234567; // TODO do we really need this size feature?
            }
        }
    }
}
