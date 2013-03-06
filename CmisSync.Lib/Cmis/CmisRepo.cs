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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Client;
using DotCMIS.Data.Impl;
using DotCMIS.Data.Extensions;

using CmisSync.Lib;
using System.ComponentModel;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

using System.Data;
using System.Data.SQLite;
using System.Collections;

namespace CmisSync.Lib.Cmis
{

    public partial class CmisRepo : RepoBase
    {
        private CmisDirectory cmis;

        public CmisRepo(RepoInfo repoInfo, ActivityListener activityListener)
            : base(repoInfo)
        {
            cmis = new CmisDirectory(repoInfo, activityListener);
            Logger.LogInfo("Sync", "Cmis:" + cmis);
        }


        // Not used.
        public override List<string> ExcludePaths
        {
            get
            {
                return new List<string>();
            }
        }


        // Not used.
        public override double Size
        {
            get
            {
                return 1234567;
            }
        }


        // Not used.
        public override double HistorySize
        {
            get
            {
                return 1234567;
            }
        }


        // Not used.
        private void UpdateSizes()
        {
            
        }


        public override string[] UnsyncedFilePaths
        {
            // Not used.
            get
            {
                Logger.LogInfo("Sync", String.Format("Cmis Repo [{0}] UnsyncedFilePaths get", this.Name));
                List<string> file_paths = new List<string>();
                //file_paths.Add (path);
                return file_paths.ToArray();
            }
        }


        public override string CurrentRevision
        {
            // Not used.
            get
            {
                Logger.LogInfo("Sync", String.Format("Cmis Repo [{0}] CurrentRevision get", this.Name));
                return null;
            }
        }


        public override bool HasRemoteChanges
        {
            // Not used.
            get
            {
                return false;
            }
        }


        public override bool SyncUp()
        {
  //          Logger.LogInfo("Sync", String.Format("Cmis Repo [{0}] SyncUp", this.Name));
  //          if (cmis != null)
  //              cmis.SyncInBackground();
            return true;
        }


        public override bool SyncDown()
        {
   //         Logger.LogInfo("Sync", String.Format("Cmis Repo [{0}] SyncDown", this.Name));
   //         if (cmis != null)
   //             cmis.SyncInBackground();
            return true;
        }

        public void DoFirstSync()
        {
            Logger.LogInfo("Sync", String.Format("Cmis Repo [{0}] First sync", this.Name));
            if (cmis != null)
                cmis.Sync();
        }

        public override bool HasLocalChanges
        {
            // Not used.
            get
            {
                return false;
            }
        }


        public override bool HasUnsyncedChanges
        {
            get
            {
                Logger.LogInfo("Sync", String.Format("Cmis Repo [{0}] HasUnsyncedChanges get", this.Name));
                if (cmis != null) // Because it is sometimes called before the object's constructor has completed.
                    cmis.SyncInBackground();
                return false; // TODO
            }

            set
            {
                // Not used.
            }
        }


        // Stages the made changes
        private void Add()
        {
            // Not used.
        }


        // Commits the made changes
        private void Commit(string message)
        {
            // Not used.
        }


        // Merges the fetched changes
        private void Rebase()
        {
            // Not used.
        }


        private void ResolveConflict()
        {
            // Not used.
        }


        public override void RevertFile(string path, string revision)
        {
            // Not used.
        }


        public override List<ChangeSet> GetChangeSets(string path, int count)
        {
            // Not used.
            Logger.LogInfo("Sync", String.Format("Cmis Repo [{0}] GetChangeSets", this.Name));
            return new List<ChangeSet>();
        }


        public override List<ChangeSet> GetChangeSets(int count)
        {
            // Not used.
            Logger.LogInfo("Sync", String.Format("Cmis Repo [{0}] GetChangeSets", this.Name));
            return new List<ChangeSet>();
        }

    }
}
