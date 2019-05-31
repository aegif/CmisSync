using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestLibrary.External
{
    public class CmisSyncProcess
    {
        // TODO Change this to your CmisSync, either debug or installed version.
        public const string CONSOLE_EXE = @"C:\Users\nico\src\CmisSync\CmisSync.Console\bin\Debug\CmisSync.Console.exe";

        /// <summary>
        /// Default synchronization interval, in milliseconds.
        /// </summary>
        public const int DEFAULT_SYNC_INTERVAL = 5000;

        /// <summary>
        /// Month-long sync interval, effectively infinitely long considering the scope of a test.
        /// </summary>
        public const int MONTH_LONG_SYNC_INTERVAL = 2000000000;

        private string localDataFolder;

        private Process process;

        public CmisSyncProcess(string remoteFolderPath,
            string url, string user, string password, string repositoryId, long pollInterval)
        {
            // Create temporary folder for configuration and data files.
            string tempFolder = Path.Combine(Path.GetTempPath(), "CmisSync_test_" + Path.GetRandomFileName());
            Console.Write("Test folder: " + tempFolder);
            string configurationFolder = Path.Combine(tempFolder, "configuration");
            localDataFolder = Path.Combine(tempFolder, "data");
            Directory.CreateDirectory(tempFolder);
            Directory.CreateDirectory(configurationFolder);
            Directory.CreateDirectory(localDataFolder);

            // Create XML config file.
            string customConfigPath = Path.Combine(configurationFolder, "config.xml");
            string logPath = Path.Combine(configurationFolder, "debug_log.txt");
            string customConfig = File.ReadAllText(@"../../config.xml");
            // Replace variables in template
            customConfig = customConfig.Replace("{LOG}", logPath);
            customConfig = customConfig.Replace("{LOCAL_FOLDER}", localDataFolder);
            customConfig = customConfig.Replace("{REMOTE_FOLDER}", remoteFolderPath);
            customConfig = customConfig.Replace("{URL}", url);
            customConfig = customConfig.Replace("{USER}", user);
            customConfig = customConfig.Replace("{PASSWORD}", password);
            customConfig = customConfig.Replace("{REPOSITORY}", repositoryId);
            customConfig = customConfig.Replace("{POLL_INTERVAL}", "" + pollInterval);
            // Write config file.
            File.WriteAllText(customConfigPath, customConfig);

            // Start CmiSync.
            string arguments = "-p -c " + customConfigPath;
            Console.Write("Executing: " + CONSOLE_EXE + " " + arguments);
            process = Process.Start(CONSOLE_EXE, arguments);
            if (null == process)
                throw new Exception("Could not start process, maybe an existing process has been reused?");
            process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            process.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);
        }

        public CmisSyncProcess(string remoteFolderPath,
            string url, string user, string password, string repositoryId)
            :
            this(remoteFolderPath, url, user, password, repositoryId, DEFAULT_SYNC_INTERVAL)
        {}

        public void Suspend()
        {
            process.Suspend();
        }

        public void Resume()
        {
            process.Resume();
        }

        public void Kill()
        {
            process.Kill();
        }

        public string Folder()
        {
            return localDataFolder;
        }
    }
}
