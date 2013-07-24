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

using System;

namespace CmisSync.Lib.Sync
{

    public partial class CmisRepo : RepoBase
    {
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
        public CmisRepo(RepoInfo repoInfo, IActivityListener activityListener)
            : base(repoInfo)
        {
            this.synchronizedFolder = new SynchronizedFolder(repoInfo, activityListener, this);
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
        /// Sync for the first time.
        /// This will create a database and download all files.
        /// </summary>
        public void DoFirstSync()
        {
            Logger.Info("First sync of " + this.Name);
            if (this.synchronizedFolder != null)
            {
                this.synchronizedFolder.Sync();
            }
        }

        /// <summary>
        /// Synchronize.
        /// The synchronization is performed in the background, so that the UI stays usable.
        /// </summary>
        public override void SyncInBackground()
        {
            if (this.synchronizedFolder != null) // Because it is sometimes called before the object's constructor has completed.
                this.synchronizedFolder.SyncInBackground();
        }

        /// <summary>
        /// Size of the synchronized folder in bytes.
        /// Obtained by adding the individual sizes of all files, recursively.
        /// </summary>
        public override double Size
        {
            get
            {
                return 1234567; // TODO
            }
        }
    }
}
