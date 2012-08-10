//   SparkleShare, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
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

using SparkleLib;

namespace SparkleLib.Cmis {

    public class SparkleRepo : SparkleRepoBase {

		private bool user_is_set;
        private bool remote_url_is_set;
        private bool use_git_bin;


        public SparkleRepo (string path, SparkleConfig config) : base (path, config)
        {
            // TODO
        }


        public override List<string> ExcludePaths {
            get {
                List<string> rules = new List<string> ();
                rules.Add (".CmisSync"); // Contains the configuration for this checkout
                return rules;
            }
        }


        public override double Size {
            get {
                return 1234567; // TODO
            }
        }


        public override double HistorySize {
            get {
                return 1234567; // TODO
            }
        }


        private void UpdateSizes ()
        {
			// TODO
        }
        

        public override string [] UnsyncedFilePaths {
            get {
                List<string> file_paths = new List<string> ();
                //file_paths.Add (path); TODO
                return file_paths.ToArray ();
            }
        }


        public override string CurrentRevision {
            get {
                return null; // TODO
            }
        }


        public override bool HasRemoteChanges {
            get {
                    return false; // TODO
            }
        }


        public override bool SyncUp ()
        {
			return true; // TODO
        }


        public override bool SyncDown ()
        {
			return true; // TODO
        }


        public override bool HasLocalChanges {
            get {
				return false; // TODO
            }
        }


        public override bool HasUnsyncedChanges {
            get {
				return false; // TODO
            }

            set {
				// TODO
            }
        }


        // Stages the made changes
        private void Add ()
        {
			// TODO
        }


        // Commits the made changes
        private void Commit (string message)
		{
			// TODO
        }


        // Merges the fetched changes
        private void Rebase ()
        {
			// TODO
        }


        private void ResolveConflict ()
        {
			// TODO
        }


        public override void RevertFile (string path, string revision)
        {
			// TODO
        }


        public override List<SparkleChangeSet> GetChangeSets (string path, int count)
        {
            return new List <SparkleChangeSet> (); // TODO
        }   


        public override List<SparkleChangeSet> GetChangeSets (int count)
        {
            return new List <SparkleChangeSet> (); // TODO
        }
    }
}
