using System;
using System.Collections.Generic;
using System.IO;

using CmisSync.Lib;


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
        static void Main(string[] args)
        {
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
                System.Threading.Thread.Sleep(1);
            }
        }
    }
}
