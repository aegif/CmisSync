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


using log4net;
using System;
using System.IO;
using Timers = System.Timers;
using CmisSync.Auth;

namespace CmisSync.Lib
{

    /// <summary>
    /// Synchronizes a remote folder.
    /// This class contains the loop that synchronizes every X seconds.
    /// </summary>
    public abstract class RepoBase : IDisposable
    {
        /// <summary>
        /// Log.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(RepoBase));


        /// <summary>
        /// Perform a synchronization if one is not running already.
        /// </summary>
        public abstract void SyncInBackground();


        /// <summary>
        /// Perform a synchronization if one is not running already.
        /// </summary>
        public abstract void SyncInBackground(bool syncFull);


        /// <summary>
        /// Local disk size taken by the repository.
        /// </summary>
        public abstract double Size { get; }


        /// <summary>
        /// Whether this folder's synchronization is running right now.
        /// </summary>
        public abstract bool isSyncing();


        /// <summary>
        /// Whether this folder's synchronization is suspended right now.
        /// </summary>
        public abstract bool isSuspended();


        /// <summary>
        /// Path of the local synchronized folder.
        /// </summary>
        public readonly string LocalPath;


        /// <summary>
        /// Name of the synchronized folder, as found in the CmisSync XML configuration file.
        /// </summary>
        public readonly string Name;


        /// <summary>
        /// URL of the remote CMIS endpoint.
        /// </summary>
        public readonly Uri RemoteUrl;


        /// <summary>
        /// Current status of the synchronization (paused or not).
        /// </summary>
        public bool Enabled { get; private set; }


        /// <summary>
        /// Stop syncing momentarily.
        /// </summary>
        public void Disable()
        {
            Enabled = false;
            RepoInfo.IsSuspended = true;

            //Get configuration
            Config config = ConfigManager.CurrentConfig;
            CmisSync.Lib.Config.SyncConfig.Folder syncConfig = config.GetFolder(this.Name);
            syncConfig.IsSuspended = true;
            config.Save();
        }

        /// <summary>
        /// Restart syncing.
        /// </summary>
        public virtual void Enable()
        {
            Enabled = true;
            RepoInfo.IsSuspended = false;

            //Get configuration
            Config config = ConfigManager.CurrentConfig;
            CmisSync.Lib.Config.SyncConfig.Folder syncConfig = config.GetFolder(this.Name);
            syncConfig.IsSuspended = false;
            config.Save();
        }

        /// <summary>
        /// Return the synchronized folder's information.
        /// </summary>
        protected RepoInfo RepoInfo { get; set; }


        /// <summary>
        /// Listener we inform about activity (used by spinner).
        /// </summary>
        private IActivityListener activityListener;


        /// <summary>
        /// Watches the local filesystem for changes.
        /// </summary>
        public Watcher Watcher { get; private set; }

        /// <summary>
        /// Timer for watching the local and remote filesystems.
        /// </summary>
        private Timers.Timer remote_timer = new Timers.Timer();

        /// <summary>
        /// Timer to delay syncing after local change is made.
        /// </summary>
        private Timers.Timer local_timer = new Timers.Timer();

        /// <summary>
        /// Timer for syncing after local change is made.
        /// </summary>
        private readonly double delay_interval = 15 * 1000; //15 seconds.

        /// <summary>
        /// When the last full sync completed.
        /// </summary>
        private DateTime last_sync;

        /// <summary>
        /// When the last partial sync completed.
        /// </summary>
        private DateTime last_partial_sync;

        /// <summary>
        /// Folder lock.
        /// </summary>
        private FolderLock folderLock;

        /// <summary>
        /// Track whether <c>Dispose</c> has been called.
        /// </summary>
        private bool disposed = false;


        /// <summary>
        /// Constructor.
        /// </summary>
        public RepoBase(RepoInfo repoInfo, IActivityListener activityListener, bool enableWatcher)
        {
            RepoInfo = repoInfo;
            LocalPath = repoInfo.TargetDirectory;
            Name = repoInfo.Name;
            RemoteUrl = repoInfo.Address;

            this.activityListener = activityListener;

            Enabled = ! repoInfo.IsSuspended;

            // Folder lock.
            // Disabled for now. Can be an interesting feature, but should be made opt-in, as
            // most users would be surprised to see this file appear.
            // folderLock = new FolderLock(LocalPath);

            if (enableWatcher)
            {
                Watcher = new Watcher(LocalPath);
                Watcher.EnableRaisingEvents = true;
            }
            
            // Main loop syncing every X seconds.
            remote_timer.Elapsed += delegate
            {
                // Synchronize.
                SyncInBackground();
            };
            remote_timer.AutoReset = true;
            Logger.Info("Repo " + repoInfo.Name + " - Set poll interval to " + repoInfo.PollInterval + "ms");
            remote_timer.Interval = repoInfo.PollInterval;

            //Partial sync interval..
            local_timer.Elapsed += delegate
            {
                // Run partial sync.
                SyncInBackground(false);
            };
            local_timer.AutoReset = false;
            local_timer.Interval = delay_interval;
        }


        /// <summary>
        /// Destructor.
        /// </summary>
        ~RepoBase()
        {
            Dispose(false);
        }


        /// <summary>
        /// Implement IDisposable interface. 
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.remote_timer.Stop();
                    this.remote_timer.Dispose();
                    this.local_timer.Stop();
                    this.local_timer.Dispose();
                    this.Watcher.Dispose();
                }
                this.disposed = true;
            }
        }


        /// <summary>
        /// Initialize the watcher.
        /// </summary>
        public void Initialize()
        {
            this.Watcher.ChangeEvent += OnFileActivity;

            // Sync up everything that changed
            // since we've been offline
            if (RepoInfo.SyncAtStartup)
            {
                SyncInBackground();
                Logger.Info(String.Format("Repo {0} - sync launch at startup", RepoInfo.Name));
            }
            else
            {
                Logger.Info(String.Format("Repo {0} - sync not launch at startup", RepoInfo.Name));
                // if LastSuccessSync + pollInterval >= DateTime.Now => Sync
                DateTime tm = RepoInfo.LastSuccessedSync.AddMilliseconds(RepoInfo.PollInterval);
                // http://msdn.microsoft.com/fr-fr/library/system.datetime.compare(v=vs.110).aspx
                if (DateTime.Compare(DateTime.Now, tm) >= 0)
                {
                    SyncInBackground();
                    Logger.Info(String.Format("Repo {0} - sync launch based on last success time sync + poll interval", RepoInfo.Name));
                }
                else
                {
                    Logger.Info(String.Format("Repo {0} - sync not launch based on last success time sync + poll interval - Next sync at {1}", RepoInfo.Name, tm));
                }
            }
        }


        /// <summary>
        /// Update repository settings.
        /// </summary>
        public virtual void UpdateSettings(string password, int pollInterval, bool syncAtStartup)
        {
            //Get configuration
            Config config = ConfigManager.CurrentConfig;
            CmisSync.Lib.Config.SyncConfig.Folder syncConfig = config.GetFolder(this.Name);

            //Pause sync
            this.remote_timer.Stop();
            if (Enabled)
            {
                Disable();
            }

            //Update password...
            if (!String.IsNullOrEmpty(password))
            {
                this.RepoInfo.Password = new Password(password.TrimEnd());
                syncConfig.ObfuscatedPassword = RepoInfo.Password.ObfuscatedPassword;
                Logger.Debug("Updated \"" + this.Name + "\" password");
            }

            // Sync at startup
            syncConfig.SyncAtStartup = syncAtStartup;

            //Update poll interval
            this.RepoInfo.PollInterval = pollInterval;
            this.remote_timer.Interval = pollInterval;
            syncConfig.PollInterval = pollInterval;
            Logger.Debug("Updated \"" + this.Name + "\" poll interval: " + pollInterval);

            //Save configuration
            config.Save();

            //Always resume sync...
            Enable();
            this.remote_timer.Start();
        }


        /// <summary>
        /// Will send message the currently running sync thread (if one exists) to stop syncing as soon as the next
        /// blockign operation completes.
        /// </summary>
        public abstract void CancelSync();


        /// <summary>
        /// Manual sync.
        /// </summary>
        public void ManualSync()
        {
            SyncInBackground();
        }


        /// <summary>
        /// Some file activity has been detected, sync changes.
        /// </summary>
        public void OnFileActivity(object sender, FileSystemEventArgs args)
        {
            local_timer.Stop();
            local_timer.Start(); //Restart the local timer...
        }


        /// <summary>
        /// A conflict has been resolved.
        /// </summary>
        protected internal void OnConflictResolved()
        {
            // ConflictResolved(); TODO
        }


        /// <summary>
        /// Called when sync starts.
        /// </summary>
        public void OnSyncStart(bool syncFull)
        {
            Logger.Info((syncFull ? "Full" : "Partial") + " Sync Started: " + LocalPath);
            if (syncFull)
            {
                remote_timer.Stop();
                local_timer.Stop();
            }
            //Watcher.EnableRaisingEvents = false; //Disable events while syncing...
            //Watcher.EnableEvent = false;
        }


        /// <summary>
        /// Called when sync completes.
        /// </summary>
        public void OnSyncComplete(bool syncFull)
        {
            if (syncFull)
            {
                remote_timer.Start();
                last_sync = DateTime.Now;
            }
            else
            {
                last_partial_sync = DateTime.Now;
            }

            if (Watcher != null)
            {
                if (Watcher.GetChangeCount() > 0)
                {
                    //Watcher was stopped (due to error) so clear and restart sync
                    Watcher.Clear();
                }
                Watcher.EnableRaisingEvents = true;
                Watcher.EnableEvent = true;
            }

            Logger.Info((syncFull ? "Full" : "Partial") + " Sync Complete: " + LocalPath);

            // Save last sync
            RepoInfo.LastSuccessedSync = DateTime.Now;
            // TODO write it to database.
        }


        /// <summary>
        /// Called when sync encounters an error.
        /// </summary>
        public void OnSyncError(Exception exception)
        {
            Logger.Info("Sync Error: " + exception.GetType() + ", " + exception.Message);
            activityListener.ActivityError(new Tuple<string, Exception>(Name, exception));
        }


        /// <summary>
        /// Recursively gets a folder's size in bytes.
        /// </summary>
        private double CalculateSize(DirectoryInfo parent)
        {
            if (!Directory.Exists(parent.ToString()))
                return 0;

            double size = 0;

            try
            {
                // All files at this level.
                foreach (FileInfo file in parent.GetFiles())
                {
                    if (!file.Exists)
                        return 0;

                    size += file.Length;
                }

                // Recurse.
                foreach (DirectoryInfo directory in parent.GetDirectories())
                    size += CalculateSize(directory);

            }
            catch (Exception)
            {
                return 0;
            }

            return size;
        }
    }
}
