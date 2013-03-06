//   CmisSync, a collaboration and sharing tool.
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
using CmisSync.Lib;

namespace CmisSync.Lib.Cmis
{

    // Sets up a fetcher that can get remote folders
    public class Fetcher : FetcherBase
    {
        CmisRepo CmisRepo;

        //public Fetcher(string server, string required_fingerprint, string remote_path,
        //    string target_folder, bool fetch_prior_history, string canonical_name, string repository, string path,
        //    string user, string password, Config config, ActivityListener activityListener)
        //    : base(server, required_fingerprint,
        //        remote_path, target_folder, fetch_prior_history, repository, path, user, password)
        public Fetcher(RepoInfo repoInfo, ActivityListener activityListener)
            : base(repoInfo)
        {
            Logger.Info("Fetcher | Cmis Fetcher constructor");
            TargetFolder = repoInfo.TargetDirectory;
            RemoteUrl = repoInfo.Address;

            // String localPath = Path.Combine(Folder.ROOT_FOLDER, repoInfo.TargetDirectory);
            // String localPath = Path.Combine(Config.DefaultConfig.FoldersPath, repoInfo.TargetDirectory);

            if (!Directory.Exists(Config.DefaultConfig.FoldersPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} do not exist", Config.DefaultConfig.FoldersPath));
                throw new DirectoryNotFoundException("Root folder don't exist !");
            }

            if (!Folder.HasWritePermissionOnDir(Config.DefaultConfig.FoldersPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} is not writable", Config.DefaultConfig.FoldersPath));
                throw new UnauthorizedAccessException("Root folder is not writable!");
            }

            if (Directory.Exists(repoInfo.TargetDirectory))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Repository Folder {0} already exist", repoInfo.TargetDirectory));
                throw new UnauthorizedAccessException("Repository folder already exist!");
            }

            Directory.CreateDirectory(repoInfo.TargetDirectory);

            CmisRepo = new CmisRepo(repoInfo, activityListener);

            // CmisDirectory cmis = new CmisDirectory(canonical_name, path, remote_path, server, user, password, repository, activityListener);
            // cmis.Sync();
        }


        public override bool Fetch()
        {
            Logger.Info("Fetcher | Cmis Fetcher Fetch");
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
                Logger.Info("Fetcher | Cmis Fetcher IsFetchedRepoEmpty");
                return false; // TODO
            }
        }


        public override void EnableFetchedRepoCrypto(string password)
        {
            Logger.Info("Fetcher | Cmis Fetcher EnableFetchedRepoCrypto");
            // TODO
        }


        public override bool IsFetchedRepoPasswordCorrect(string password)
        {
            Logger.Info("Fetcher | Cmis Fetcher IsFetchedRepoPasswordCorrect");
            return true; // TODO
        }


        public override void Stop()
        {
            Logger.Info("Fetcher | Cmis Fetcher Stop");
        }


        public override void Complete()
        {
            Logger.Info("Fetcher | Cmis Fetcher Complete");
            // base.Complete();
        }


        private void InstallConfiguration()
        {
            Logger.Info("Fetcher | Cmis Fetcher InstallConfiguration");
        }

    }
}
