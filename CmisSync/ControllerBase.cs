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
    public abstract class ControllerBase : IActivityListener
    {
        /// <summary>
        /// Log.
        /// </summary>
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(ControllerBase));


        /// <summary>
        /// Whether it is the first time that CmisSync is being run.
        /// </summary>
        private bool firstRun;


        /// <summary>
        /// All the info about the CmisSync synchronized folder being created.
        /// </summary>
        private RepoInfo repoInfo;


        /// <summary>
        /// Whether the reporsitories have finished loading.
        /// </summary>
        public bool RepositoriesLoaded { get; private set; }


        /// <summary>
        /// List of the CmisSync synchronized folders.
        /// </summary>
        private List<RepoBase> repositories = new List<RepoBase>();


        /// <summary>
        /// Dictionary of the edit folder diaglogs
        /// Key: synchronized folder name
        /// Value: <c>Edit</c>
        /// </summary>
        private Dictionary<string, Edit> edits = new Dictionary<string, Edit>();


        /// <summary>
        /// Path where the CmisSync synchronized folders are by default.
        /// </summary>
        public string FoldersPath { get; private set; }


        public event ShowSetupWindowEventHandler ShowSetupWindowEvent = delegate { };
        public delegate void ShowSetupWindowEventHandler(PageType page_type);

        public event Action ShowAboutWindowEvent = delegate { };

        public event FolderFetchedEventHandler FolderFetched = delegate { };
        public delegate void FolderFetchedEventHandler(string remote_url);

        public event FolderFetchingHandler FolderFetching = delegate { };
        public delegate void FolderFetchingHandler(double percentage);

        public event Action FolderListChanged = delegate { };


        public event Action OnIdle = delegate { };
        public event Action OnSyncing = delegate { };
        public event Action OnError = delegate { };


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
                List<string> folders = new List<string>();
                foreach (Config.SyncConfig.Folder f in ConfigManager.CurrentConfig.Folder)
                    folders.Add(f.DisplayName);
                folders.Sort();

                return folders;
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
        private IActivityListener activityListenerAggregator;


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
                ConfigManager.CurrentConfig.Notifications = true;
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
        private void AddRepository(RepoInfo repositoryInfo)
        {
            RepoBase repo = null;
            repo = new CmisSync.Lib.Sync.CmisRepo(repositoryInfo, activityListenerAggregator);

            repo.ChangesDetected += delegate
            {
                UpdateState();
            };

            repo.SyncStatusChanged += delegate(SyncStatus status)
            {
                UpdateState();
            };

            this.repositories.Add(repo);
            repo.Initialize();
        }

        public void RemoveRepositoryFromSync(string reponame)
        {
            lock (this.repo_lock)
            {
                Config.SyncConfig.Folder f = ConfigManager.CurrentConfig.getFolder(reponame);
                if (f != null)
                {
                    Edit edit = null;
                    if (edits.TryGetValue(reponame, out edit))
                    {
#if __MonoCS__
                        edit.Destroy();
#else
                        edit.Close();
#endif
                    }
                    RemoveRepository(f);
                    ConfigManager.CurrentConfig.Folder.Remove(f);
                    ConfigManager.CurrentConfig.Save();
                    FolderListChanged();
                }
                else
                {
                    Logger.Warn("Reponame \"" + reponame + "\" could not be found: Removing Repository failed");
                }
            }
        }

        public void EditRepositoryFolder(string reponame)
        {
            Config.SyncConfig.Folder folder;

            lock (this.repo_lock)
            {
                folder = ConfigManager.CurrentConfig.getFolder(reponame);
                if (folder == null)
                {
                    Logger.Warn("Reponame \"" + reponame + "\" could not be found: Editing Repository failed");
                    return;
                }

                Edit edit = null;
                if (edits.TryGetValue(reponame, out edit))
                {
                    edit.Show();
                    return;
                }

                RepoInfo.CmisPassword password = new RepoInfo.CmisPassword();
                password.ObfuscatedPassword = folder.ObfuscatedPassword;
                Uri address = folder.RemoteUrl;
                List<string> oldIgnores = new List<string>();
                foreach (Config.IgnoredFolder ignore in folder.IgnoredFolders)
                {
                    oldIgnores.Add(ignore.Path);
                }
                edit = new Edit(folder.DisplayName, folder.UserName, password.ToString(), address.ToString(), folder.RepositoryId, folder.RemotePath, oldIgnores, folder.LocalPath);
                edits.Add(reponame, edit);

                edit.Controller.SaveFolderEvent += delegate
                {
                    lock (this.repo_lock)
                    {
                        folder.IgnoredFolders.Clear();
                        foreach (string ignore in edit.Ignores)
                        {
                            folder.IgnoredFolders.Add(new Config.IgnoredFolder() { Path = ignore });
                        }
                        ConfigManager.CurrentConfig.Save();
                        foreach (string oldIgnore in oldIgnores)
                        {
                            if (String.IsNullOrEmpty(edit.Ignores.Find(
                                delegate(string ignore)
                                {
                                    if (ignore == oldIgnore || oldIgnore.StartsWith(ignore + "/"))
                                    {
                                        return true;
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                })))
                            {
                                Logger.Info(String.Format("The remote folder {0} should be synced after editing ignore folders", oldIgnore));
                                lock (this.repo_lock)
                                {
                                    foreach (RepoBase repo in this.repositories)
                                    {
                                        if (repo.Name == reponame)
                                        {
                                            //  force a full tree work sync
                                            repo.Watcher.EnableRaisingEvents = false;
                                        }
                                    }
                                }
                                break;
                            }
                        }
                    }
                };

                edit.Controller.CloseWindowEvent += delegate
                {
                    lock (this.repo_lock)
                    {
                        edits.Remove(reponame);
                    }
                };

                edit.Show();
            }
        }

        /// <summary>
        /// Remove a synchronized folder from the CmisSync configuration.
        /// This happens after the user removes the folder.
        /// </summary>
        /// <param name="folder">The synchronized folder to remove</param>
        private void RemoveRepository(Config.SyncConfig.Folder folder)
        {
            foreach (RepoBase repo in this.repositories)
            {
                if (repo.LocalPath.Equals(folder.LocalPath))
                {
                    repo.Dispose();
                    this.repositories.Remove(repo);
                    break;
                }
            }

            // Remove Cmis Database File
            string dbfilename = folder.DisplayName;
            dbfilename = dbfilename.Replace("\\", "_");
            dbfilename = dbfilename.Replace("/", "_");
            RemoveDatabase(dbfilename);
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
            lock (this.repo_lock)
            {
                foreach (RepoBase aRepo in this.repositories)
                {
                    if (aRepo.Name == repoName)
                    {
                        if (aRepo.Status != SyncStatus.Suspend)
                        {
                            aRepo.Suspend();
                            Logger.Debug("Requested to syspend sync of repo " + aRepo.Name);
                        }
                        else
                        {
                            aRepo.Resume();
                            Logger.Debug("Requested to resume sync of repo " + aRepo.Name);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Check the configured CmisSync synchronized folders.
        /// Remove the ones whose folders have been deleted.
        /// </summary>
        private void CheckRepositories()
        {
            lock (this.repo_lock)
            {
                List<Config.SyncConfig.Folder> toBeDeleted = new List<Config.SyncConfig.Folder>();
                // If folder has been deleted, remove it from configuration too.
                foreach (Config.SyncConfig.Folder f in ConfigManager.CurrentConfig.Folder)
                {
                    string folder_name = f.DisplayName;
                    string folder_path = f.LocalPath;

                    if (!Directory.Exists(folder_path))
                    {
                        RemoveRepository(f);
                        toBeDeleted.Add(f);

                        Logger.Info("Controller | Removed folder '" + folder_name + "' from config");

                    }
                    else
                    {
                        AddRepository(f.GetRepoInfo());
                    }
                }

                foreach(Config.SyncConfig.Folder f in toBeDeleted){
                    ConfigManager.CurrentConfig.Folder.Remove(f);
                }
                if(toBeDeleted.Count>0)
                    ConfigManager.CurrentConfig.Save();
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
        public void StartFetcher(string name, Uri address, string user, string password, string repository, string remote_path, string local_path,
            List<string> ignoredPaths)
        {
            repoInfo = new RepoInfo(name, ConfigManager.CurrentConfig.ConfigPath);
            repoInfo.Address = address;
            repoInfo.User = user;
            repoInfo.Password = password;
            repoInfo.RepoID = repository;
            repoInfo.RemotePath = remote_path;
            repoInfo.TargetDirectory = local_path;
            repoInfo.PollInterval = 5000;
            repoInfo.MaxUploadRetries = 2;
            foreach (string ignore in ignoredPaths)
                repoInfo.addIgnorePath(ignore);

            fetcher = new Fetcher(repoInfo, activityListenerAggregator);
            this.FinishFetcher();
        }


        /// <summary>
        /// Finalize the creation of a new CmisSync synchronized folder.
        /// </summary>
        public void FinishFetcher()
        {
            lock (this.repo_lock)
            {
                // Add folder to XML config file.
                ConfigManager.CurrentConfig.AddFolder(repoInfo);

                FolderFetched(this.fetcher.RemoteUrl.ToString());

                // Initialize in the UI.
                AddRepository(repoInfo);
                FolderListChanged();

                this.fetcher.Dispose();
                this.fetcher = null;
            }
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
