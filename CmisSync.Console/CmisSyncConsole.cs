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
	class CmisSyncConsole
	{
        /// <summary>
        /// Logging.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CmisSyncConsole));

        /// <summary>
        /// Configured synchronized folder on which the synchronization must be performed.
        /// </summary>
		private List<CmisRepo> repos = new List<CmisRepo>();

        /// <summary>
        /// Whether the tool should synchronize again and again forever (like the desktop CmisSync, using a poll interval/etc),
        /// or just synchronize once and exit.
        /// </summary>
        private bool perpetual;

        /// <summary>
        /// Controller for the console UI.
        /// </summary>
        private static ConsoleController controller = new ConsoleController();

        public CmisSyncConsole(bool perpetual)
        {
            this.perpetual = perpetual;
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
            CmisRepo cmisRepo = new CmisRepo(repoInfo, controller, false, perpetual);
            repos.Add(cmisRepo);
        }

        /// <summary>
        /// Synchronize folder.
        /// </summary>
		private bool Run()
        {
            bool success = true;

            if (perpetual)
            {
                // No need to do anything, as the periodicSynchronizationTimer objects in RepoBase have been configured to run synchronizations at regular intervals.

                // Wait until our processus gets terminated. All work is done in other threads.
                Thread.Sleep(Timeout.Infinite);

                // This line will never be reached, but needed for compilation.
                return true;
            }
            else
            {
                foreach (CmisRepo cmisRepo in repos)
                {
                    success &= cmisRepo.SyncInForeground();
                }

                return success;
            }
        }

        /// <summary>
        /// Main method, pass folder name as argument.
        /// </summary>
		public static int Main (string[] args)
		{
            System.Console.WriteLine("Started CmisSyncOnce");
            Utils.ConfigureLogging();
            Logger.Info("Starting. Version: " + CmisSync.Lib.Backend.Version);

            // Uncomment this line to disable SSL checking (for self-signed certificates)
            // ServicePointManager.CertificatePolicy = new YesCertPolicyHandler();

            PathRepresentationConverter.SetConverter(new WindowsPathRepresentationConverter());

            bool perpetual = false;

            CmisSyncConsole instance = null;

            // -p means perpetual.
            if (args.Length > 0 && "-p".Equals(args[0]))
            {
                perpetual = true;
            }

            instance = new CmisSyncConsole(perpetual);

            // Load the specified synchronized folders, or all if none is specified.
            if (args.Length > 1 || (!perpetual && args.Length > 0 ))
            {
                int i = 0;

                // Skip the -p argument if present.
                if ("-p".Equals(args[0]))
                {
                    i++;
                }

                for (; i < args.Length; i++)
                {
                    instance.AddSynchronizedFolder(args[i]);
                }
            }
            else
            {
                // No specific folders given, so load all folders.

                Config config = ConfigManager.CurrentConfig;
                foreach (CmisSync.Lib.Config.SyncConfig.Folder folder in config.Folders)
                {
                    RepoInfo repoInfo = folder.GetRepoInfo();
                    CmisRepo cmisRepo = new CmisRepo(repoInfo, controller, false, perpetual);
                    instance.repos.Add(cmisRepo);
                }
            }

            // Synchronize all
            bool success = instance.Run();

            System.Console.WriteLine("Press enter to close...");
            System.Console.ReadLine();

            // Exit code 0 if synchronization was successful or not needed,
            // 1 if synchronization failed, or could not run.
            return success ? 0 : 1;
		}
	}
}
