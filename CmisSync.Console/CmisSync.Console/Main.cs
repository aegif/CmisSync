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
	class MainClass
	{

		private CmisRepo cmisRepo;

		public static void Main (string[] args)
		{
            if (args.Length < 1)
            {
                System.Console.WriteLine("Usage: CmisSyncOnce.exe mysyncedfolder");
                System.Console.WriteLine("Example: CmisSyncOnce.exe \"192.168.0.22\\Main Repository\"");
                System.Console.WriteLine("See your folders names in C:\\Users\\you\\AppData\\Roaming\\cmissync\\config.xml or similar");
                return;
            }

			MainClass main = new MainClass();
			main.Init(args[0]);
            main.Sync();
		}

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

		private void Sync ()
		{
			cmisRepo.SyncInBackground();
		}

	}

}
