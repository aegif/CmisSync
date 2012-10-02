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
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

using SparkleLib;

namespace SparkleLib.Cmis {

    // Sets up a fetcher that can get remote folders
    public class SparkleFetcher : SparkleFetcherBase {

        public SparkleFetcher (string server, string required_fingerprint, string remote_path,
            string target_folder, bool fetch_prior_history, string repository, string path,
            string user, string password) : base (server, required_fingerprint,
                remote_path, target_folder, fetch_prior_history, repository, path, user, password)
        {
			Console.WriteLine("Cmis SparkleFetcher constructor");
            TargetFolder = target_folder;
            RemoteUrl    = new Uri (server);

            Cmis cmis = new Cmis(path, remote_path, server, user, password, repository);
            cmis.SyncInBackground();
        }


        public override bool Fetch ()
        {
			Console.WriteLine("Cmis SparkleFetcher Fetch");
            return true; // TODO
		}


        public override bool IsFetchedRepoEmpty {
            get {
				Console.WriteLine("Cmis SparkleFetcher IsFetchedRepoEmpty");
                return false; // TODO
            }
        }


        public override void EnableFetchedRepoCrypto (string password)
        {
			Console.WriteLine("Cmis SparkleFetcher EnableFetchedRepoCrypto");
			// TODO
        }


        public override bool IsFetchedRepoPasswordCorrect (string password)
        {
			Console.WriteLine("Cmis SparkleFetcher IsFetchedRepoPasswordCorrect");
			return true; // TODO
        }


        public override void Stop ()
        {
			Console.WriteLine("Cmis SparkleFetcher Stop");
        }


        public override void Complete ()
        {
			Console.WriteLine("Cmis SparkleFetcher Complete");
            base.Complete ();
        }


        private void InstallConfiguration ()
        {
			Console.WriteLine("Cmis SparkleFetcher InstallConfiguration");
        }
    }
}
