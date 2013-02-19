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

namespace SparkleLib.Cmis
{

    // Sets up a fetcher that can get remote folders
    public class SparkleFetcher : SparkleFetcherBase
    {
        SparkleRepoCmis CmisRepo;

        //public SparkleFetcher(string server, string required_fingerprint, string remote_path,
        //    string target_folder, bool fetch_prior_history, string canonical_name, string repository, string path,
        //    string user, string password, SparkleConfig config, ActivityListener activityListener)
        //    : base(server, required_fingerprint,
        //        remote_path, target_folder, fetch_prior_history, repository, path, user, password)
        public SparkleFetcher(SparkleRepoInfo repoInfo, ActivityListener activityListener)
            : base(repoInfo)
        {
            SparkleLogger.LogInfo("Fetcher", "Cmis SparkleFetcher constructor");
            TargetFolder = repoInfo.TargetDirectory;
            RemoteUrl = repoInfo.Address;

            String localPath = Path.Combine(SparkleFolder.ROOT_FOLDER, repoInfo.TargetDirectory);
            Directory.CreateDirectory(localPath);

            CmisRepo = new SparkleRepoCmis(localPath, repoInfo, activityListener);

            // CmisDirectory cmis = new CmisDirectory(canonical_name, path, remote_path, server, user, password, repository, activityListener);
            // cmis.Sync();
        }


        public override bool Fetch()
        {
            SparkleLogger.LogInfo("Fetcher", "Cmis SparkleFetcher Fetch");
            //double percentage = 1.0;
            //Regex progress_regex = new Regex(@"([0-9]+)%", RegexOptions.Compiled);
            //DateTime last_change = DateTime.Now;
            //TimeSpan change_interval = new TimeSpan(0, 0, 0, 1);

            CmisRepo.DoFirstSync();
            return true; // TODO
        }


        public override bool IsFetchedRepoEmpty
        {
            get
            {
                SparkleLogger.LogInfo("Fetcher", "Cmis SparkleFetcher IsFetchedRepoEmpty");
                return false; // TODO
            }
        }


        public override void EnableFetchedRepoCrypto(string password)
        {
            SparkleLogger.LogInfo("Fetcher", "Cmis SparkleFetcher EnableFetchedRepoCrypto");
            // TODO
        }


        public override bool IsFetchedRepoPasswordCorrect(string password)
        {
            SparkleLogger.LogInfo("Fetcher", "Cmis SparkleFetcher IsFetchedRepoPasswordCorrect");
            return true; // TODO
        }


        public override void Stop()
        {
            SparkleLogger.LogInfo("Fetcher", "Cmis SparkleFetcher Stop");
        }


        public override void Complete()
        {
            SparkleLogger.LogInfo("Fetcher", "Cmis SparkleFetcher Complete");
            // base.Complete();
        }


        private void InstallConfiguration()
        {
            SparkleLogger.LogInfo("Fetcher","Cmis SparkleFetcher InstallConfiguration");
        }

    }
}
