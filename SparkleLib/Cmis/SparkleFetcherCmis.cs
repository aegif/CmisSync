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

        private string crypto_salt = "e0d592768d7cf99a"; // TODO: Make unique per repo


        public SparkleFetcher (string server, string required_fingerprint, string remote_path,
            string target_folder, bool fetch_prior_history) : base (server, required_fingerprint,
                remote_path, target_folder, fetch_prior_history)
        {
            TargetFolder = target_folder;
            RemoteUrl    = new Uri ("http://localhost:8080/alfresco/service/cmis"); // TODO
        }


        public override bool Fetch ()
        {
            return true; // TODO
		}


        public override bool IsFetchedRepoEmpty {
            get {
                return false; // TODO
            }
        }


        public override void EnableFetchedRepoCrypto (string password)
        {
			// TODO
        }


        public override bool IsFetchedRepoPasswordCorrect (string password)
        {
			return true; // TODO
        }


        public override void Stop ()
        {
        }


        public override void Complete ()
        {
            base.Complete ();
        }


        private void InstallConfiguration ()
        {
        }
    }
}
