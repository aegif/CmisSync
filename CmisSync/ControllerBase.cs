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


using CmisSync.Lib;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.ObjectModel;

using CmisSync.Lib.Cmis;
using CmisSync.Lib.Events;
using CmisSync.Auth;

#if __COCOA__
// using Edit = CmisSync.EditWizardController;
#endif

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
        /// Whether the repositories have finished loading.
        /// </summary>
        public bool RepositoriesLoaded { get; private set; }


        /// <summary>
        /// List of the CmisSync synchronized folders.
        /// </summary>
        private List<RepoBase> repositories = new List<RepoBase>();


        /// <summary>
        /// Path where the CmisSync synchronized folders are by default.
        /// </summary>
        public string FoldersPath { get; private set; }


        /// <summary>
        /// Show setup window event.
        /// </summary>
        public event ShowSetupWindowEventHandler ShowSetupWindowEvent = delegate { };
        /// <summary>
        /// Show setup window event.
        /// </summary>
        public delegate void ShowSetupWindowEventHandler(PageType page_type);

        /// <summary>
        /// Show about window event.
        /// </summary>
        public event Action ShowAboutWindowEvent = delegate { };

        /// <summary>
        /// Folder list changed.
        /// </summary>
        public event Action FolderListChanged = delegate { };

        public event Action OnTransmissionListChanged = delegate { };

        /// <summary>
        /// Called with status changes to idle.
        /// </summary>
        public event Action OnIdle = delegate { };
        /// <summary>
        /// Called with status changes to syncing.
        /// </summary>
        public event Action OnSyncing = delegate { };
        /// <summary>
        /// Called with status changes to error.
        /// </summary>
        public event Action<Tuple<string, Exception>> OnError = delegate { };
        /// <summary>
        /// Called with status changes to error resolved.
        /// </summary>
        public event Action OnErrorResolved = delegate { };

        /// <summary>
        /// Alert notification.
        /// </summary>
        public event AlertNotificationRaisedEventHandler AlertNotificationRaised = delegate { };
        /// <summary>
        /// Alert notification.
        /// </summary>
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


        private ActiveActivitiesManager activitiesManager;


        /// <summary>
        /// A folder lock for the base directory.
        /// </summary>
        private FolderLock folderLock;

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
            activitiesManager = new ActiveActivitiesManager();
            this.activitiesManager.ActiveTransmissions.CollectionChanged += delegate(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
                OnTransmissionListChanged();
            };
        }

        public List<FileTransmissionEvent> ActiveTransmissions() {
            return this.activitiesManager.ActiveTransmissions.ToList<FileTransmissionEvent>();
        }


        /// <summary>
        /// Initialize the controller.
        /// </summary>
        /// <param name="firstRun">Whether it is the first time that CmisSync is being run.</param>
        public virtual void Initialize(Boolean firstRun)
        {
            this.firstRun = firstRun;

            // Create the CmisSync folder and add it to the bookmarks
            bool syncFolderCreated = CreateCmisSyncFolder();

            if (syncFolderCreated)
            {
                AddToBookmarks();
            }

            if (firstRun)
            {
                ConfigManager.CurrentConfig.Notifications = true;
            }

            folderLock = new FolderLock(FoldersPath);
        }


        /// <summary>
        /// Once the GUI has loaded, show setup window if it is the first run, or check the repositories.
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
                    // Update GUI.
                    FolderListChanged();
                }).Start();
            }
        }


        /// <summary>
        /// Initialize (in the GUI and syncing mechanism) an existing CmisSync synchronized folder.
        /// </summary>
        /// <param name="repositoryInfo">Synchronized folder path</param>
        private void AddRepository(RepoInfo repositoryInfo)
        {
            RepoBase repo = null;
            repo = new CmisSync.Lib.Sync.CmisRepo(repositoryInfo, activityListenerAggregator);

            repo.EventManager.AddEventHandler(
                new GenericSyncEventHandler<FileTransmissionEvent>( 50, delegate(ISyncEvent e){
                this.activitiesManager.AddTransmission(e as FileTransmissionEvent);
                return false;
            }));
            this.repositories.Add(repo);
            repo.Initialize();
        }

        /// <summary>
        /// Update settings for repository.
        /// </summary>
        public void UpdateRepositorySettings(string repoName, string password, int pollInterval, bool syncAtStartup)
        {
            foreach (RepoBase repoBase in this.repositories)
            {
                if (repoBase.Name == repoName)
                {
                    repoBase.UpdateSettings(password, pollInterval, syncAtStartup);
                    OnErrorResolved();
                    FolderListChanged();
                }
            }
        }

        /// <summary>
        /// Remove repository from sync.
        /// </summary>
        public void RemoveRepositoryFromSync(string reponame)
        {
            Config.SyncConfig.Folder f = ConfigManager.CurrentConfig.getFolder(reponame);
            if (f != null)
            {
                RemoveRepository(f);
                ConfigManager.CurrentConfig.RemoveFolder(reponame);
                FolderListChanged();
            }
            else
            {
                Logger.Warn("Reponame \"" + reponame + "\" could not be found: Removing Repository failed");
            }
        }

        /// <summary>
        /// Run a sync manually.
        /// </summary>
        public void ManualSync(string reponame)
        {
            foreach (RepoBase aRepo in this.repositories)
            {
                if (aRepo.Name == reponame && aRepo.Status == SyncStatus.Idle)
                {

                    aRepo.ManualSync();
                    Logger.Debug("Requested to manually sync " + aRepo.Name);
                }
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
                    repo.CancelSync();
                    repo.Dispose();
                    this.repositories.Remove(repo);
                    Logger.Info("Removed Repository: " + repo.Name);
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
            if (File.Exists(databasefile))
            {
                File.Delete(databasefile);
                Logger.Info("Removed database: " + databasefile);
            }
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
                    if (aRepo.Status != SyncStatus.Suspend)
                    {
                        aRepo.Suspend();
                        Logger.Debug("Requested to suspend sync of repo " + aRepo.Name);
                    }
                    else
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

                foreach (Config.SyncConfig.Folder f in toBeDeleted)
                {
                    ConfigManager.CurrentConfig.Folder.Remove(f);
                }
                if (toBeDeleted.Count > 0)
                    ConfigManager.CurrentConfig.Save();
            }

            // Update GUI.
            FolderListChanged();
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
                if (!CmisSync.Lib.Utils.IsSymlink(file))
                    File.SetAttributes(file, FileAttributes.Normal);
        }

        /// <summary>
        /// Create a new CmisSync synchronized folder.
        /// </summary>
        public void CreateRepository(string name, Uri address, string user, string password, string repository, string remote_path, string local_path,
            List<string> ignoredPaths, bool syncAtStartup)
        {
            repoInfo = new RepoInfo(name, ConfigManager.CurrentConfig.ConfigPath);
            repoInfo.Address = address;
            repoInfo.User = user;
            repoInfo.Password = new Password(password);
            repoInfo.RepoID = repository;
            repoInfo.RemotePath = remote_path;
            repoInfo.TargetDirectory = local_path;
            repoInfo.PollInterval = Config.DEFAULT_POLL_INTERVAL;
            repoInfo.IsSuspended = false;
            repoInfo.LastSuccessedSync = new DateTime(1900, 01, 01);
            repoInfo.SyncAtStartup = syncAtStartup;
            repoInfo.MaxUploadRetries = 2;

            foreach (string ignore in ignoredPaths)
                repoInfo.addIgnorePath(ignore);

            // Check that the CmisSync root folder exists.
            if (!Directory.Exists(ConfigManager.CurrentConfig.FoldersPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} does not exist", ConfigManager.CurrentConfig.FoldersPath));
                throw new DirectoryNotFoundException("Root folder don't exist !");
            }

            // Check that the folder is writable.
            if (!CmisSync.Lib.Utils.HasWritePermissionOnDir(ConfigManager.CurrentConfig.FoldersPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} is not writable", ConfigManager.CurrentConfig.FoldersPath));
                throw new UnauthorizedAccessException("Root folder is not writable!");
            }

            // Check that the folder exists.
            if (Directory.Exists(repoInfo.TargetDirectory))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Repository Folder {0} already exist", repoInfo.TargetDirectory));
                throw new UnauthorizedAccessException("Repository folder already exists!");
            }

            // Create the local folder.
            Directory.CreateDirectory(repoInfo.TargetDirectory);

            // Add folder to XML config file.
            ConfigManager.CurrentConfig.AddFolder(repoInfo);

            // Initialize in the GUI.
            AddRepository(repoInfo);
            FolderListChanged();
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
        /// Show an alert to the user.
        /// </summary>
        public void ShowAlert(string title, string message)
        {
            AlertNotificationRaised(Properties_Resources.CmisSync + " " + title, message);
        }


        /// <summary>
        /// Quit CmisSync.
        /// </summary>
        public virtual void Quit()
        {
            foreach (RepoBase repo in Repositories)
                repo.Dispose();

            folderLock.Dispose();

            Logger.Info("Exiting.");
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

        /// <summary>
        /// Error occured.
        /// </summary>
        public void ActivityError(Tuple<string, Exception> error)
        {
            OnError(error);
        }
    }
}
