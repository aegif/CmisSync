//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;
using log4net;

namespace CmisSync
{
    /// <summary>
    /// Platform-independant part of the main CmisSync controller.
    /// </summary>
    public abstract class ControllerBase : ActivityListener
    {
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(ControllerBase));

        /// <summary>
        /// Whether it is the first time that CmisSync is being run.
        /// </summary>
        private bool firstRun;

        private RepoInfo repoInfo;

        /// <summary>
        /// Whether the reporsitories have finished loading.
        /// </summary>
        public bool RepositoriesLoaded { get; private set; }

        private List<RepoBase> repositories = new List<RepoBase>();
        public string FoldersPath { get; private set; }

        public double ProgressPercentage = 0.0;
        public string ProgressSpeed = "";

        public event ShowSetupWindowEventHandler ShowSetupWindowEvent = delegate { };
        public delegate void ShowSetupWindowEventHandler(PageType page_type);

        public event Action ShowAboutWindowEvent = delegate { };

        public event FolderFetchedEventHandler FolderFetched = delegate { };
        public delegate void FolderFetchedEventHandler(string remote_url);

        public event FolderFetchErrorHandler FolderFetchError = delegate { };
        public delegate void FolderFetchErrorHandler(string remote_url, string[] errors);

        public event FolderFetchingHandler FolderFetching = delegate { };
        public delegate void FolderFetchingHandler(double percentage);

        public event Action FolderListChanged = delegate { };


        public event Action OnIdle = delegate { };
        public event Action OnSyncing = delegate { };
        public event Action OnError = delegate { };


        public event NotificationRaisedEventHandler NotificationRaised = delegate { };
        public delegate void NotificationRaisedEventHandler(ChangeSet change_set);

        public event AlertNotificationRaisedEventHandler AlertNotificationRaised = delegate { };
        public delegate void AlertNotificationRaisedEventHandler(string title, string message);


        /// <summary>
        /// Get the repositories configured in CmisSync.
        /// </summary>
        public RepoBase[] Repositories
        {
            get
            {
                lock (this.repo_lock)
                    return this.repositories.GetRange(0, this.repositories.Count).ToArray();
            }
        }

        /// <summary>
        /// Whether it is the first time that CmisSync is being run.
        /// </summary>
        public bool FirstRun
        {
            get
            {
                return firstRun;
            }
        }

        /// <summary>
        /// The list of synchronized folders.
        /// </summary>
        public List<string> Folders
        {
            get
            {
                List<string> folders = new List<string>(ConfigManager.CurrentConfig.Folders);
                folders.Sort();

                return folders;
            }
        }

        public List<string> UnsyncedFolders
        {
            get
            {
                List<string> unsynced_folders = new List<string>();

                foreach (RepoBase repo in Repositories)
                {
                    repo.SyncInBackground();
                }

                return unsynced_folders;
            }
        }

        /// <summary>
        /// Add CmisSync to the list of programs to be started up when the user logs into Windows.
        /// </summary>
        public abstract void CreateStartupItem();

        /// <summary>
        /// Add CmisSync to the user's Windows Explorer bookmarks.
        /// </summary>
        public abstract void AddToBookmarks();


        /// <summary>
        /// Creates the CmisSync folder in the user's home folder.
        /// </summary>
        public abstract bool CreateCmisSyncFolder();


        /// <summary>
        /// Keeps track of whether a download or upload is going on, for display of the task bar animation.
        /// </summary>
        private ActivityListener activityListenerAggregator;

        /// <summary>
        /// Component to create new CmisSync synchronized folders.
        /// </summary>
        private Fetcher fetcher;

        /// <summary>
        /// Watches the local filesystem for modifications.
        /// </summary>
        private FileSystemWatcher watcher;

        /// <summary>
        /// Concurrency locks.
        /// </summary>
        private Object repo_lock = new Object();
        private Object check_repos_lock = new Object();


        /// <summary>
        /// Constructor.
        /// </summary>
        public ControllerBase()
        {
            activityListenerAggregator = new ActivityListenerAggregator(this);
            FoldersPath = ConfigManager.CurrentConfig.FoldersPath;
        }


        /// <summary>
        /// Initialize the controller.
        /// </summary>
        /// <param name="firstRun">Whether it is the first time that CmisSync is being run.</param>
        public virtual void Initialize(Boolean firstRun)
        {
            this.firstRun = firstRun;

            // Create the CmisSync folder and add it to the bookmarks
            if (CreateCmisSyncFolder())
                AddToBookmarks();

            if (firstRun)
            {
                ConfigManager.CurrentConfig.SetConfigOption("notifications", bool.TrueString);
            }

            // Watch the CmisSync folder
            this.watcher = new FileSystemWatcher()
            {
                Filter = "*",
                IncludeSubdirectories = false,
                Path = FoldersPath
            };

            watcher.Deleted += OnFolderActivity;
            watcher.Created += OnFolderActivity;
            watcher.Renamed += OnFolderActivity;

            watcher.EnableRaisingEvents = true;
        }


        /// <summary>
        /// Once the UI has loaded, show setup window if it is the first run, or check the repositories.
        /// </summary>
        public void UIHasLoaded()
        {
            if (firstRun)
            {
                ShowSetupWindow(PageType.Setup);

            }
            else
            {
                new Thread(() =>
                {
                    CheckRepositories();
                    RepositoriesLoaded = true;
                    FolderListChanged();

                }).Start();
            }
        }


        /// <summary>
        /// Initialize (in the UI and syncing mechanism) an existing CmisSync synchronized folder.
        /// </summary>
        /// <param name="folderPath">Synchronized folder path</param>
        private void AddRepository(string folderPath)
        {
            RepoBase repo = null;
            string folder_name = Path.GetFileName(folderPath);

            RepoInfo repositoryInfo = ConfigManager.CurrentConfig.GetRepoInfo(folder_name);
            repo = new CmisSync.Lib.Sync.CmisRepo(repositoryInfo, activityListenerAggregator);

            repo.ChangesDetected += delegate
            {
                UpdateState();
            };

            repo.SyncStatusChanged += delegate(SyncStatus status)
            {
                if (status == SyncStatus.Idle)
                {
                    ProgressPercentage = 0.0;
                    ProgressSpeed = "";
                }

                UpdateState();
            };

            repo.ProgressChanged += delegate(double percentage, string speed)
            {
                ProgressPercentage = percentage;
                ProgressSpeed = speed;

                UpdateState();
            };

            this.repositories.Add(repo);
            repo.Initialize();
        }


        
        /// <summary>
        /// Remove a synchronized folder from the CmisSync configuration.
        /// This happens after the user removes the folder.
        /// </summary>
        /// <param name="folder_path">The synchronized folder to remove</param>
        private void RemoveRepository(string folder_path)
        {
            if (this.repositories.Count > 0)
            {
                for (int i = 0; i < this.repositories.Count; i++)
                {
                    RepoBase repo = this.repositories[i];

                    if (repo.LocalPath.Equals(folder_path))
                    {
                        // Remove Cmis Database File
                        RemoveDatabase(folder_path);

                        repo.Dispose();
                        this.repositories.Remove(repo);
                        repo = null;

                        return;
                    }
                }
            }

            RemoveDatabase(folder_path);
        }


        /// <summary>
        /// Remove the local database associated with a CmisSync synchronized folder.
        /// </summary>
        /// <param name="folder_path">The synchronized folder whose database is to be removed</param>
        private void RemoveDatabase(string folder_path)
        {
            string databasefile = Path.Combine(ConfigManager.CurrentConfig.ConfigPath, Path.GetFileName(folder_path) + ".cmissync");
            if (File.Exists(databasefile)) File.Delete(databasefile);
        }


        /// <summary>
        /// Pause or un-pause synchronization for a particular folder.
        /// </summary>
        /// <param name="repoName">the folder to pause/unpause</param>
        public void StartOrSuspendRepository(string repoName)
        {
            foreach (RepoBase aRepo in this.repositories)
            {
                if (aRepo.Name == repoName)
                {
                    if (aRepo.Status != SyncStatus.Suspend)
                        aRepo.Suspend();
                    else aRepo.Resume();
                }
            }
        }


        /// <summary>
        /// Check the configured CmisSync synchronized folders.
        /// Remove the ones whose folders have been deleted.
        /// </summary>
        private void CheckRepositories()
        {
            lock (this.check_repos_lock)
            {
                string path = ConfigManager.CurrentConfig.FoldersPath;

                // If folder has been renamed, rename it in configuration too.
                foreach (string folder_path in Directory.GetDirectories(path))
                {
                    string folder_name = Path.GetFileName(folder_path);

                    if (ConfigManager.CurrentConfig.GetIdentifierForFolder(folder_name) == null)
                    {
                        string identifier_file_path = Path.Combine(folder_path, ".CmisSync");

                        if (!File.Exists(identifier_file_path))
                            continue;

                        string identifier = File.ReadAllText(identifier_file_path).Trim();

                        if (ConfigManager.CurrentConfig.IdentifierExists(identifier))
                        {
                            RemoveRepository(folder_path);
                            ConfigManager.CurrentConfig.RenameFolder(identifier, folder_name);

                            string new_folder_path = Path.Combine(path, folder_name);
                            AddRepository(new_folder_path);

                            Logger.Info("Controller | Renamed folder with identifier " + identifier + " to '" + folder_name + "'");
                        }
                    }
                }

                // If folder has been deleted, remove it from configuration too.
                foreach (string folder_name in ConfigManager.CurrentConfig.Folders)
                {
                    string folder_path = new Folder(folder_name).FullPath;

                    if (!Directory.Exists(folder_path))
                    {
                        RemoveRepository(folder_path);
                        ConfigManager.CurrentConfig.RemoveFolder(folder_name);

                        Logger.Info("Controller | Removed folder '" + folder_name + "' from config");

                    }
                    else
                    {
                        AddRepository(folder_path);
                    }
                }

                // Update UI.
                FolderListChanged();
            }
        }


        /// <summary>
        /// Fires events for the current syncing state.
        /// </summary>
        private void UpdateState()
        {
            bool has_unsynced_repos = false;

            foreach (RepoBase repo in Repositories)
            {
                repo.SyncInBackground();
            }

            if (has_unsynced_repos)
                OnError();
            else
                OnIdle();
        }


        /// <summary>
        /// Fix the file attributes of a folder, recursively.
        /// </summary>
        /// <param name="path">Folder to fix</param>
        private void ClearFolderAttributes(string path)
        {
            if (!Directory.Exists(path))
                return;

            string[] folders = Directory.GetDirectories(path);

            foreach (string folder in folders)
                ClearFolderAttributes(folder);

            string[] files = Directory.GetFiles(path);

            foreach (string file in files)
                if (!IsSymlink(file))
                    File.SetAttributes(file, FileAttributes.Normal);
        }


        /// <summary>
        /// Whether a file is a symbolic link.
        /// </summary>
        private bool IsSymlink(string file)
        {
            FileAttributes attributes = File.GetAttributes(file);
            return ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint);
        }


        /// <summary>
        /// Reacts when a local change occurs.
        /// Not implemented yet, see https://github.com/nicolas-raoul/CmisSync/issues/122
        /// </summary>
        /// <param name="o">File that has changed</param>
        /// <param name="args">Nature of the change</param>
        public void OnFolderActivity(object o, FileSystemEventArgs args)
        {
            // TODO
            if (Directory.Exists(args.FullPath) && args.ChangeType == WatcherChangeTypes.Created)
                return;
        }


        /// <summary>
        /// Create a new CmisSync synchronized folder.
        /// </summary>
        public void StartFetcher(string address, string remote_path, string local_path,
            string repository, string path, string user, string password, string localrepopath)
        {
            repoInfo = new RepoInfo(local_path, ConfigManager.CurrentConfig.ConfigPath);
            repoInfo.Address = new Uri(address);
            repoInfo.RemotePath = remote_path;
            repoInfo.RepoID = repository;
            repoInfo.User = user;
            repoInfo.Password = Crypto.Obfuscate(password);
            repoInfo.TargetDirectory = localrepopath;
            repoInfo.PollInterval = 5000;

            fetcher = new Fetcher(repoInfo, activityListenerAggregator);

            // Actions.

            this.fetcher.Finished += delegate(bool repo_is_encrypted, bool repo_is_empty, string[] warnings)
            {
                FinishFetcher();
            };

            this.fetcher.Failed += delegate
            {
                FolderFetchError(this.fetcher.RemoteUrl.ToString(), this.fetcher.GetErrors());
                StopFetcher();
            };

            this.fetcher.ProgressChanged += delegate(double percentage)
            {
                FolderFetching(percentage);
            };

            this.FinishFetcher();
        }


        /// <summary>
        /// Stop fetching if failed
        /// TODO: necessary?
        /// </summary>
        public void StopFetcher()
        {
            if (Directory.Exists(this.fetcher.TargetFolder))
            {
                try
                {
                    Directory.Delete(this.fetcher.TargetFolder, true);
                    Logger.Info("Deleted " + this.fetcher.TargetFolder);

                }
                catch (Exception e)
                {
                    Logger.Info("Failed to delete " + this.fetcher.TargetFolder + ": " + e.Message);
                }
            }

            this.fetcher = null;
        }


        /// <summary>
        /// Finalize the creation of a new CmisSync synchronized folder.
        /// </summary>
        public void FinishFetcher()
        {
            // Add folder to XML config file.
            ConfigManager.CurrentConfig.AddFolder(repoInfo);

            FolderFetched(this.fetcher.RemoteUrl.ToString());

            // Initialize in the UI.
            AddRepository(repoInfo.TargetDirectory);
            FolderListChanged();

            this.fetcher = null;
        }


        /// <summary>
        /// Show first-time wizard.
        /// </summary>
        public void ShowSetupWindow(PageType page_type)
        {
            ShowSetupWindowEvent(page_type);
        }


        /// <summary>
        /// Show info about CmisSync
        /// </summary>
        public void ShowAboutWindow()
        {
            ShowAboutWindowEvent();
        }


        /// <summary>
        /// Quit CmisSync.
        /// </summary>
        public virtual void Quit()
        {
            foreach (RepoBase repo in Repositories)
                repo.Dispose();

            Environment.Exit(0);
        }


        /// <summary>
        /// A download or upload has started, so run task icon animation.
        /// </summary>
        public void ActivityStarted()
        {
            OnSyncing();
        }

        /// <summary>
        /// No download nor upload, so no task icon animation.
        /// </summary>
        public void ActivityStopped()
        {
            OnIdle();
        }
    }
}
