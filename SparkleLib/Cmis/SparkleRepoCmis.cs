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

namespace SparkleLib.Cmis {

    public class SparkleRepo : SparkleRepoBase {

        private SparkleLib.Cmis.CmisDirectory cmis;

        public SparkleRepo(string path, SparkleConfig config, ActivityListener activityListener)
            : base(path, config)
        {
            cmis = new CmisDirectory(path, config, activityListener);
            SparkleLogger.LogInfo("Sync", "Cmis:" + cmis);
        }


        public override List<string> ExcludePaths {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo ExcludePaths get");
                List<string> rules = new List<string> ();
                rules.Add (".CmisSync"); // Contains the configuration for this checkout.
                return rules;
            }
        }


        public override double Size {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo Size get");
                return 1234567; // TODO
            }
        }


        public override double HistorySize {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo HistorySize get");
                return 1234567; // TODO
            }
        }


        private void UpdateSizes ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo UpdateSizes");
			// TODO
        }
        

        public override string [] UnsyncedFilePaths {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo UnsyncedFilePaths get");
                List<string> file_paths = new List<string> ();
                //file_paths.Add (path); TODO
                return file_paths.ToArray ();
            }
        }


        public override string CurrentRevision {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo CurrentRevision get");
                return null; // TODO
            }
        }


        public override bool HasRemoteChanges {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo HasRemoteChanges get");
                return false; // TODO
            }
        }


        public override bool SyncUp ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo SyncUp");
			return true; // TODO
        }


        public override bool SyncDown ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo SyncDown");
			return true; // TODO
        }


        public override bool HasLocalChanges {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo HasLocalChanges get");
				return false; // TODO
            }
        }


        public override bool HasUnsyncedChanges {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo HasUnsyncedChanges get");
                if (cmis != null) // Because it is sometimes called before the object's constructor has completed.
                    cmis.SyncInBackground();
				return false; // TODO
            }

            set {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo HasUnsyncedChanges set");
				// TODO
            }
        }


        // Stages the made changes
        private void Add ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo Add");
			// TODO
        }


        // Commits the made changes
        private void Commit (string message)
		{
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo Commit");
			// TODO
        }


        // Merges the fetched changes
        private void Rebase ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo Rebase");
			// TODO
        }


        private void ResolveConflict ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo ResolveConflict");
			// TODO
        }


        public override void RevertFile (string path, string revision)
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo RevertFile");
			// TODO
        }


        public override List<SparkleChangeSet> GetChangeSets (string path, int count)
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo GetChangeSets");
            return new List <SparkleChangeSet> (); // TODO
        }   


        public override List<SparkleChangeSet> GetChangeSets (int count)
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo GetChangeSets");
            return new List <SparkleChangeSet> (); // TODO
        }
    }
}
