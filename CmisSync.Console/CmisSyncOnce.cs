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
				#if __COCOA__
				System.Console.WriteLine("Usage: cmissync_once mysyncedfolder");
				System.Console.WriteLine("Example: cmissync_once \"192.168.0.22\\Main Repository\"");
				System.Console.WriteLine("See your folders names in /Users/you/.config/cmissync/config.xml or similar");
				#else
				System.Console.WriteLine("Usage: CmisSyncOnce.exe mysyncedfolder");
				System.Console.WriteLine("Example: CmisSyncOnce.exe \"192.168.0.22\\Main Repository\"");
				System.Console.WriteLine("See your folders names in C:\\Users\\you\\AppData\\Roaming\\cmissync\\config.xml or similar");
				#endif
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
            CmisSync.Lib.Config.SyncConfig.Folder folder = config.GetFolder(folderName);
            if (folder == null)
            {
                System.Console.WriteLine("No folder found with this name: " + folderName);
                return;
            }
			RepoInfo repoInfo = folder.GetRepoInfo();

			ConsoleController controller = new ConsoleController ();
			cmisRepo = new CmisRepo (repoInfo, controller);
		}

        /// <summary>
        /// Synchronize folder.
        /// </summary>
		private void Sync ()
		{
            cmisRepo.SyncInNotBackground();
		}
	}
}
