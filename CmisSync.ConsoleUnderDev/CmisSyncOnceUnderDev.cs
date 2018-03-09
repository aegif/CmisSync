using System;
using System.IO;
using System.Threading;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Config;
using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncRepo;
using CmisSync.Lib.Sync.CmisSyncFolder;
using CmisSync.Lib.Sync.SyncWorker;
using log4net;
using System.Net;

using System.Collections.Generic;
using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Client;

namespace CmisSync.ConsoleUnderDev
{
    class CmisSyncOnceUnderDev
    {

        private static readonly ILog Logger = LogManager.GetLogger(typeof(CmisSyncOnceUnderDev));

        private List<RepoInfo> Repos = new List<RepoInfo>();

        /// <summary>
        /// The entry point of the program, where the program control starts and ends.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        public static void Main(string[] args)
        {

            Utils.ConfigureLogging();
            Logger.Info("Starting. Version: " + CmisSync.Lib.Backend.Version);

            CmisSyncOnceUnderDev once = new CmisSyncOnceUnderDev();

            CmisSyncConfig config = ConfigManager.CurrentConfig;
            foreach (CmisSync.Lib.Config.CmisSyncConfig.SyncConfig.Folder folder in config.Folders)
            {
                RepoInfo repoInfo = folder.GetRepoInfo();
                once.Repos.Add(repoInfo);
            }

            once.Sync();
        }

        private bool Sync () 
        {
            bool success = true;

            foreach (RepoInfo repoInfo in Repos) {
                CmisSyncFolder cmisSyncFolder = new CmisSyncFolder(repoInfo);
                System.Console.WriteLine("Create CmisSyncFolder:\n" +
                                         "  Name: {0}\n" +
                                         "  Remote Uri: {1}\n" +
                                         "  RemotePath: {2}\n" +
                                         "  LocalPath: {3}\n",
                                         cmisSyncFolder.Name, cmisSyncFolder.CmisProfile.RemoteUri + "( id: "+ cmisSyncFolder.CmisProfile.RepoID+" )",
                                         cmisSyncFolder.RemotePath, cmisSyncFolder.LocalPath);

                SyncWorker syncWorker = new SyncWorker(cmisSyncFolder);
                syncWorker.Initialize();
                syncWorker.Start();
            }

            return success;
        }

        private void TestFolder( String rootfolder ) {

            foreach (String file in Directory.GetFiles(rootfolder))
            {
                System.Console.WriteLine("  {0}", file);
            }

            foreach (String folder in Directory.GetDirectories(rootfolder))
            {
                System.Console.WriteLine("  {0}", folder);
                TestFolder(folder);
            }

        }
    }
}
