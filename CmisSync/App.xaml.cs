using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using CmisSync.Lib;
using log4net;
using log4net.Config;
using CmisSync.Lib.Sync;
using System.Net;

namespace CmisSync
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(App));

        /// <summary>
        /// Mutex checking whether CmisSync is already running or not.
        /// </summary>
        private static Mutex program_mutex = new Mutex(false, "CmisSync");

        /// <summary>
        /// A folder lock for the base directory.
        /// </summary>
        private FolderLock folderLock;

        private Controller controller;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool firstRun = !File.Exists(ConfigManager.CurrentConfigFile);
            // Migrate config.xml from past versions, if necessary.
            if (!firstRun)
                ConfigMigration.Migrate();

            CmisSync.Lib.SyncUtils.ConfigureLogging();

            Logger.Info("Starting. Version: " + CmisSync.Lib.Backend.Version);

            checkStartupParameters(e.Args);

            checkSingleInstance();

            //FIXME: Increase the number of concurrent requests to each server,
            // as an unsatisfying workaround until this DotCMIS bug 632 is solved.
            // See https://github.com/aegif/CmisSync/issues/140
            ServicePointManager.DefaultConnectionLimit = 1000;

            // Create the CmisSync folder and add it to the bookmarks
            createCmisSyncFolderAndAddToBookmarks();

            //set Notifications flag only on first run (dont' override it if has been changed)
            if (firstRun)
            {
                ConfigManager.CurrentConfig.Notifications = true;
            }

            // -------------------------------------------------

            
            controller = new Controller();
            controller.startBackgroundWork();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            controller.Dispose();
        }

        /// <summary>
        /// Create the user's CmisSync settings folder.
        /// This folder contains databases, the settings file and the log file.
        /// </summary>
        /// <returns></returns>
        private void createCmisSyncFolderAndAddToBookmarks()
        {
            String syncFolderPath = ConfigManager.CurrentConfig.DefaultSyncFolderRootFolderPath;
            if (Directory.Exists(syncFolderPath))
            {
                //the folder alredy exist, no need to create it
                return;
            }

            Directory.CreateDirectory(syncFolderPath);
            File.SetAttributes(syncFolderPath, File.GetAttributes(syncFolderPath) | FileAttributes.System);

            Logger.Info("Config | Created '" + syncFolderPath + "'");

            string app_path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string icon_file_path = Path.Combine(app_path, "Resources", "cmissync-folder.ico");
            if (!File.Exists(icon_file_path))
            {
                icon_file_path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            }

            string ini_file_path = Path.Combine(syncFolderPath, "desktop.ini");

            string ini_file = "[.ShellClassInfo]\r\n" +
                    "IconFile=" + icon_file_path + "\r\n" +
                    "IconIndex=0\r\n" +
                    "InfoTip=CmisSync\r\n" +
                    "IconResource=" + icon_file_path + ",0\r\n" +
                    "[ViewState]\r\n" +
                    "Mode=\r\n" +
                    "Vid=\r\n" +
                    "FolderType=Generic\r\n";

            try
            {
                File.WriteAllText(ini_file_path, ini_file);

                    File.SetAttributes(ini_file_path,
                        File.GetAttributes(ini_file_path) | FileAttributes.Hidden | FileAttributes.System);


                folderLock = new FolderLock(syncFolderPath);
            }
            catch (IOException e)
            {
                Logger.Info("Config | Failed setting icon for '" + syncFolderPath + "': " + e.Message);
            }
        }

        private void checkStartupParameters(string[] args)
        {
            if (args.Length != 0 && !args[0].Equals("start") &&
                Backend.Platform != PlatformID.MacOSX &&
                Backend.Platform != PlatformID.Win32NT)
            {
                string n = Environment.NewLine;

                Console.WriteLine(n +
                    "CmisSync is a collaboration and sharing tool that is" + n +
                    "designed to keep things simple and to stay out of your way." + n +
                    n +
                    "Version: " + CmisSync.Lib.Backend.Version + n +
                    "Copyright (C) 2014 Aegif" + n +
                    "This program comes with ABSOLUTELY NO WARRANTY." + n +
                    n +
                    "This is free software, and you are welcome to redistribute it" + n +
                    "under certain conditions. Please read the GNU GPLv3 for details." + n +
                    n +
                    "Usage: CmisSync [start|stop|restart]");
                Environment.Exit(-1);
            }
        }

        private void checkSingleInstance()
        {
            // Only allow one instance of CmisSync (on Windows)
            if (!program_mutex.WaitOne(0, false))
            {
                Logger.Error("CmisSync is already running.");
                Environment.Exit(-1);
            }
        }
    }

    
}

