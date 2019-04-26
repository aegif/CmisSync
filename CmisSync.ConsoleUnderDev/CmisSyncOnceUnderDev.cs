using System;
using System.IO;
using System.Threading;
using System.Net;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Config;
using CmisSync.Lib.Sync;
using CmisSync.Lib.Utilities.PathConverter;
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

            /*
             * https://social.msdn.microsoft.com/Forums/en-US/2ab06a91-c8fd-474c-81ea-61b78c8fbb56/httpwebrequestgetrequeststream-hangs-bug-in-systemnet-stack-?forum=ncl
             * 
             * The default max connection limit of a service point (which might be used by httpWebRequest) is 2.
             * So if more than 1 file is updated to the remote ( using http PUT method ) server, httpWebRequest will hang.
             * 
             * TODO: set this value by 'MaxParallism' field in cmissync's config file
             */
            ServicePointManager.DefaultConnectionLimit = 10;

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
                syncWorker.DoSync();
                syncWorker.Disconnect();
            }

            return success;
        }

    }
}
