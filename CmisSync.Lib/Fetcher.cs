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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using log4net;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync;

namespace CmisSync.Lib
{

    /// <summary>
    /// Creates a CmisSync synchronized folder.
    /// TODO: This class should probably be removed, together with the last step of the folder addition wizard.
    /// </summary>
    public class Fetcher : IDisposable
    {
        /// <summary>
        /// Log.
        /// </summary>
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(Fetcher));


        /// <summary>
        /// Configuration details of the CmisSync synchronized folder.
        /// </summary>
        private CmisRepo cmisRepo;


        /// <summary>
        /// Track whether <c>Dispose</c> has been called.
        /// </summary>
        private bool disposed = false;


        /// <summary>
        /// Indicates that the synchronized folder has been set up.
        /// </summary>
        public Action Finished { get; set; }


        /// <summary>
        /// URL of the CMIS enpoint.
        /// </summary>
        public Uri RemoteUrl { get; protected set; }


        /// <summary>
        /// Local folder for the synchronization.
        /// </summary>
        public string TargetFolder { get; protected set; }


        /// <summary>
        /// Sets up a fetcher that can get remote CMIS folders.
        /// </summary>
        public Fetcher(RepoInfo repoInfo, IActivityListener activityListener)
        {
            string remote_path = repoInfo.RemotePath.Trim("/".ToCharArray());
            string address = repoInfo.Address.ToString();

            TargetFolder = repoInfo.TargetDirectory;

            RemoteUrl = new Uri(address + remote_path);

            Logger.Info("Fetcher | Cmis Fetcher constructor");
            TargetFolder = repoInfo.TargetDirectory;
            RemoteUrl = repoInfo.Address;

            // Check that the CmisSync root folder exists.
            if (!Directory.Exists(ConfigManager.CurrentConfig.FoldersPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} does not exist", ConfigManager.CurrentConfig.FoldersPath));
                throw new DirectoryNotFoundException("Root folder don't exist !");
            }

            // Check that the folder is writable.
            if (!Utils.HasWritePermissionOnDir(ConfigManager.CurrentConfig.FoldersPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} is not writable", ConfigManager.CurrentConfig.FoldersPath));
                throw new UnauthorizedAccessException("Root folder is not writable!");
            }

            // Check that the folder exists.
            if (Directory.Exists(repoInfo.TargetDirectory))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Repository Folder {0} already exist", repoInfo.TargetDirectory));
                throw new UnauthorizedAccessException("Repository folder already exists!");
            }

            // Create the local folder.
            Directory.CreateDirectory(repoInfo.TargetDirectory);

            // Use this folder configuration.
            this.cmisRepo = new CmisRepo(repoInfo, activityListener);
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~Fetcher()
        {
            Dispose(false);
        }


        /// <summary>
        /// Implement IDisposable interface. 
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (this.cmisRepo != null)
                    {
                        this.cmisRepo.Dispose();
                    }
                }
                this.disposed = true;
            }
        }
    }
}
