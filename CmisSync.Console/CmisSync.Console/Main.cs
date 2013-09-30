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

			//System.Console.WriteLine(ConfigManager.CurrentConfigFile);
			MainClass main = new MainClass();
			//main.init();
			main.Test();

			//System.Console.WriteLine(Crypto.Obfuscate("admin"));


		}
		public void Test ()
		{
			// Create session.
			var parameters = new Dictionary<string, string>();
			parameters[SessionParameter.BindingType] = BindingType.AtomPub;
			parameters[SessionParameter.AtomPubUrl] = "http://192.168.0.22:8080/alfresco/cmisatom"; // MODIFY HERE
			parameters[SessionParameter.User] = "admin"; // MODIFY HERE
			parameters[SessionParameter.Password] = "admin"; // MODIFY HERE
			var factory = SessionFactory.NewInstance();
			ISession session = factory.GetRepositories(parameters)[0].CreateSession();
			
			// Update a document twice.
			string remoteFilePath = "/User Homes/test.txt";
			Document doc = (Document)session.GetObjectByPath(remoteFilePath);
		}

/*		private void init ()
		{
			Config config = ConfigManager.CurrentConfig;
			RepoInfo repoInfo = config.GetRepoInfo("documentLibrary");
			
			ConsoleController controller = new ConsoleController ();
			cmisRepo = new CmisRepo (repoInfo, controller);
			
			cmisRepo.Initialize ();

			//main Loop
			cmisRepo.DoFirstSync (); 
			TimerCallback timerDelegate = new TimerCallback(Sync);
			Timer timer = new Timer(timerDelegate, null , 0, 1000);
		}*/

		public  void Sync ( object o)
		{
			cmisRepo.SyncInBackground();
		}

	}

}
