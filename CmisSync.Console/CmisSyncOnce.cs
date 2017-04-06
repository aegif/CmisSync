using System;
using System.IO;
using System.Threading;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync;
using log4net;
using System.Net;

using System.Collections.Generic;
using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Client;

namespace CmisSync.Console
{
    /// <summary>
    /// Utility that performs a single synchronization.
    /// Useful for scripts, cron or command-line usage.
    /// </summary>
	class CmisSyncOnce
	{
        /// <summary>
        /// Configured synchronized folder on which the synchronization must be performed.
        /// </summary>
		private List<RepoInfo> repos = new List<RepoInfo>();

        /// <summary>
        /// Main method, pass folder name as argument.
        /// </summary>
		public static int Main (string[] args)
		{
            Utils.ConfigureLogging();
            PathRepresentationConverter.SetConverter(new WindowsPathRepresentationConverter());

            CmisSyncOnce once = new CmisSyncOnce();

            // Load the specified synchronized folders, or all if none is specified.
            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    once.AddSynchronizedFolder(args[i]);
                }
            }
            else
            {
                Config config = ConfigManager.CurrentConfig;
                foreach (CmisSync.Lib.Config.SyncConfig.Folder folder in config.Folders)
                {
                    RepoInfo repoInfo = folder.GetRepoInfo();
                    once.repos.Add(repoInfo);
                }
            }

            // Synchronize all
            bool success = once.Sync();

            // Exit code 0 if synchronization was successful or not needed,
            // 1 if synchronization or could not run for unforeseen reasons.
            return success ? 0 : 1;
		}

        /// <summary>
        /// Load folder configuration.
        /// </summary>
        private void AddSynchronizedFolder(string folderName)
		{
			Config config = ConfigManager.CurrentConfig;
            CmisSync.Lib.Config.SyncConfig.Folder folder = config.GetFolder(folderName);
            if (folder == null)
            {
                System.Console.WriteLine("No folder found with this name: " + folderName);
                return;
            }
			RepoInfo repoInfo = folder.GetRepoInfo();
            repos.Add(repoInfo);
		}

        /// <summary>
        /// Synchronize folder.
        /// </summary>
		private bool Sync ()
		{
            bool success = true;
            ConsoleController controller = new ConsoleController();

            foreach (RepoInfo repoInfo in repos)
            {
                CmisRepo cmisRepo = new CmisRepo (repoInfo, controller, false);
                success &= cmisRepo.SyncNotInBackground();
            }

            return success;
		}
	}
}
