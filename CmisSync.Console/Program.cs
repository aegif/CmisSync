using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using log4net;
using log4net.Config;
using CmisSync.Lib;
using CmisSync.Lib.Sync;


namespace CmisSync.Console
{
    class ActivityListener : IActivityListener
    {
        public void ActivityStarted()
        {
        }

        public void ActivityStopped()
        {
        }
    }

    class Program
    {
        /// <summary>
        /// Mutex checking whether CmisSync is already running or not.
        /// </summary>
        private static Mutex program_mutex = new Mutex(false, "DataSpaceSync");

        /// <summary>
        /// Logging.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// Logging also to the command line if true, default is false
        /// </summary>
        private static bool verbose = false;

        static void Main(string[] args)
        {

            // Only allow one instance of DataSpace Sync (on Windows)
            if (!program_mutex.WaitOne(0, false))
            {
                System.Console.WriteLine("DataSpaceSync is already running.");
                Environment.Exit(-1);
            }
            if (File.Exists(ConfigManager.CurrentConfigFile))
                ConfigMigration.Migrate();

            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());

            if (args.Length != 0)
            {
                foreach(string arg in args) {
                    // Check, if the user would like to read console logs
                    if (arg.Equals("-v") || arg.Equals("--verbose"))
                        verbose = true;
                }
            }
            // Add Console Logging if user wants to
            if (verbose)
                BasicConfigurator.Configure();

            Logger.Info("Starting.");

            List<RepoBase> repositories = new List<RepoBase>();

            foreach (Config.SyncConfig.Folder folder in ConfigManager.CurrentConfig.Folder)
            {
                string path = folder.LocalPath;
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                RepoBase repo = new CmisSync.Lib.Sync.CmisRepo(folder.GetRepoInfo(),new ActivityListener());
                repositories.Add(repo);
                repo.Initialize();
            }

            while(true)
            {
                System.Threading.Thread.Sleep(100);
            }
        }
    }
}
