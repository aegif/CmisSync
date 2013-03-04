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

using SparkleLib;
using System.ComponentModel;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

using System.Data;
using System.Data.SQLite;
using System.Collections;

using SparkleLib.Cmis;

namespace SparkleLib.Cmis
{

    public partial class SparkleRepoCmis : SparkleRepoBase
    {
        private CmisDirectory cmis;

        public SparkleRepoCmis(SparkleRepoInfo repoInfo, ActivityListener activityListener)
            : base(repoInfo)
        {
            cmis = new CmisDirectory(repoInfo, activityListener);
            SparkleLogger.LogInfo("Sync", "Cmis:" + cmis);
        }


        public override List<string> ExcludePaths
        {
            get
            {
                SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] ExcludePaths get", this.Name));
                return new List<string>();
            }
        }


        public override double Size
        {
            get
            {
                SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] Size get", this.Name));
                return 1234567; // TODO
            }
        }


        public override double HistorySize
        {
            get
            {
                SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] HistorySize get", this.Name));
                return 1234567; // TODO
            }
        }


        private void UpdateSizes()
        {
            SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] UpdateSizes", this.Name));
            // TODO
        }


        public override string[] UnsyncedFilePaths
        {
            get
            {
                SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] UnsyncedFilePaths get", this.Name));
                List<string> file_paths = new List<string>();
                //file_paths.Add (path); TODO
                return file_paths.ToArray();
            }
        }


        public override string CurrentRevision
        {
            get
            {
                SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] CurrentRevision get", this.Name));
                return null; // TODO
            }
        }


        public override bool HasRemoteChanges
        {
            // TODO - Yannick - Use ChangeLogToken on the repository to determine if someone is change.
            // ChangeLogToken must be store physicaly (CmisDatabase ?)
            get
            {
                SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] HasRemoteChanges get", this.Name));
                return false; // TODO
            }
        }


        public override bool SyncUp()
        {
            SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] SyncUp", this.Name));
            if (cmis != null)
                cmis.SyncInBackground();
            return true; // TODO
        }


        public override bool SyncDown()
        {
            SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] SyncDown", this.Name));
            if (cmis != null)
                cmis.SyncInBackground();
            return true;
        }

        public void DoFirstSync()
        {
            SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] First sync", this.Name));
            if (cmis != null)
                cmis.Sync();
        }

        public override bool HasLocalChanges
        {
            // TODO - Yannick - Use FileSystemWatcher to determine what is changed.
            get
            {
                SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] HasLocalChanges get", this.Name));
                return false; // TODO
            }
        }


        public override bool HasUnsyncedChanges
        {
            get
            {
                SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] HasUnsyncedChanges get", this.Name));
                if (cmis != null) // Because it is sometimes called before the object's constructor has completed.
                    cmis.SyncInBackground();
                return false; // TODO
            }

            set
            {
                SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] HasUnsyncedChanges set", this.Name));
                // TODO
            }
        }


        // Stages the made changes
        private void Add()
        {
            SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] Add", this.Name));
            // TODO
        }


        // Commits the made changes
        private void Commit(string message)
        {
            SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] Commit", this.Name));
            // TODO
        }


        // Merges the fetched changes
        private void Rebase()
        {
            SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] Rebase", this.Name));
            // TODO
        }


        private void ResolveConflict()
        {
            SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] ResolveConflict", this.Name));
            // TODO
        }


        public override void RevertFile(string path, string revision)
        {
            SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] RevertFile", this.Name));
            // TODO
        }


        public override List<SparkleChangeSet> GetChangeSets(string path, int count)
        {
            SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] GetChangeSets", this.Name));
            return new List<SparkleChangeSet>(); // TODO
        }


        public override List<SparkleChangeSet> GetChangeSets(int count)
        {
            SparkleLogger.LogInfo("Sync", String.Format("Cmis SparkleRepo [{0}] GetChangeSets", this.Name));
            return new List<SparkleChangeSet>(); // TODO
        }

    }
}
