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
        private void AddSynchronizedFolder(string folderName, bool enableWatcher)
        {
            Config config = ConfigManager.CurrentConfig;
            CmisSync.Lib.Config.SyncConfig.Folder folder = config.GetFolder(folderName);
            if (folder == null)
            {
                System.Console.WriteLine("No folder found with this name: " + folderName);
                return;
            }
            RepoInfo repoInfo = folder.GetRepoInfo();
            CmisRepo cmisRepo = new CmisRepo(repoInfo, controller, enableWatcher, perpetual);
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

                // Some repos are configured to be synchronized at startup, so sync them (done in background).
                foreach (CmisRepo cmisRepo in repos)
                {
                    cmisRepo.SyncAtStartupIfConfiguredToDoSo(); // Done in background, so no success boolean returned.
                }

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
		public static int Main (string[] argumentsArray)
		{
            System.Console.WriteLine("Started CmisSyncOnce");

            // Uncomment this line to disable SSL checking (for self-signed certificates)
            // ServicePointManager.CertificatePolicy = new YesCertPolicyHandler();

            PathRepresentationConverter.SetConverter(new WindowsPathRepresentationConverter());

            var argumentsList = new List<string>(argumentsArray);
            bool perpetual = false;

            CmisSyncConsole instance = null;

            // -p means perpetual.
            if (argumentsList.Count >= 1 && "-p".Equals(argumentsList[0]))
            {
                perpetual = true;
                argumentsList.RemoveAt(0);
            }

            // Get optional config file from command line argument -c
            if (argumentsList.Count >= 2 && "-c".Equals(argumentsList[0]))
            {
                System.Console.WriteLine("argument -c");
                // Set the config file to use.
                ConfigManager.CurrentConfigFile = argumentsList[1];

                argumentsList.RemoveAt(0); // Remove -c
                argumentsList.RemoveAt(0); // Remove the path
            }
            System.Console.WriteLine("config: " + ConfigManager.CurrentConfigFile);

            // Now that we have the config, we can start logging (the log file location is in the config).
            Utils.ConfigureLogging();
            Logger.Info("Starting. Version: " + CmisSync.Lib.Backend.Version);

            instance = new CmisSyncConsole(perpetual);

            // Load the specified synchronized folders, or all if none is specified.
            bool enableWatcher = perpetual; // We consider that the watcher is only desirable for perpetual synchronization.
            if (argumentsList.Count >= 1)
            {
                foreach(var argument in argumentsList) {
                    instance.AddSynchronizedFolder(argument, enableWatcher);
                }
            }
            else
            {
                // No specific folders given, so load all folders.

                foreach (CmisSync.Lib.Config.SyncConfig.Folder folder in ConfigManager.CurrentConfig.Folders)
                {
                    RepoInfo repoInfo = folder.GetRepoInfo();
                    CmisRepo cmisRepo = new CmisRepo(repoInfo, controller, enableWatcher, perpetual);
                    instance.repos.Add(cmisRepo);
                }
            }

            // Synchronize all
            bool success = instance.Run();

            // Only for testing in an IDE, to see what happened in the console window before it gets closed by the IDE.
            //System.Console.WriteLine("Press enter to close...");
            //System.Console.ReadLine();

            // Exit code 0 if synchronization was successful or not needed,
            // 1 if synchronization failed, or could not run.
            return success ? 0 : 1;
		}
	}
}
