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
        private SynchronizedFolder synchronizedFolder;

        public CmisRepo(RepoInfo repoInfo, ActivityListener activityListener)
            : base(repoInfo)
        {
            synchronizedFolder = new SynchronizedFolder(repoInfo, activityListener, this);
            Logger.Info(synchronizedFolder);
        }

        /// <summary>
        /// Sync for the first time.
        /// This will create a database and download all files.
        /// </summary>
        public void DoFirstSync()
        {
            Logger.Info(String.Format("First sync {0}", this.Name));
            if (synchronizedFolder != null)
                synchronizedFolder.Sync();
        }

        /// <summary>
        /// Synchronize.
        /// The synchronization is performed in the background, so that the UI stays usable.
        /// </summary>
        public override void SyncInBackground()
        {
            if (synchronizedFolder != null) // Because it is sometimes called before the object's constructor has completed.
                synchronizedFolder.SyncInBackground();
        }

        /// <summary>
        /// Size of the repository in bytes.
        /// Obtained by adding the individual sizes of all files.
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
