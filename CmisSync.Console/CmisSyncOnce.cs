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
		private CmisRepo cmisRepo;

        /// <summary>
        /// Main method, pass folder name as argument.
        /// </summary>
		public static void Main (string[] args)
		{
            // Check arguments.
            if (args.Length < 1)
            {
                System.Console.WriteLine("Usage: Oris4SyncOnce.exe mysyncedfolder");
                System.Console.WriteLine("Example: Oris4SyncOnce.exe \"192.168.0.22\\Main Repository\"");
                System.Console.WriteLine("See your folders names in C:\\Users\\you\\AppData\\Roaming\\oris4sync\\config.xml or similar");
                return;
            }

            // Load and synchronize.
			CmisSyncOnce once = new CmisSyncOnce();
			once.Init(args[0]);
            once.Sync();
		}

        /// <summary>
        /// Load folder configuration.
        /// </summary>
		private void Init (string folderName)
		{
			Config config = ConfigManager.CurrentConfig;
            CmisSync.Lib.Config.SyncConfig.Folder folder = config.getFolder(folderName);
            if (folder == null)
            {
                System.Console.WriteLine("No folder found with this name: " + folderName);
                return;
            }
			RepoInfo repoInfo = folder.GetRepoInfo();
			
			ConsoleController controller = new ConsoleController ();
			cmisRepo = new CmisRepo (repoInfo, controller);
			
			cmisRepo.Initialize ();
		}

        /// <summary>
        /// Synchronize folder.
        /// </summary>
		private void Sync ()
		{
            cmisRepo.SyncInBackground(); // TODO should not be put in background.
		}
	}
}
