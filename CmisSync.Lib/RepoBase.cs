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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;

using Timers = System.Timers;

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
        /// Local disk size taken by the repository.
        /// </summary>
        public abstract double Size { get; }


        /// <summary>
        /// Affect a new <c>SyncStatus</c> value.
        /// </summary>
        public Action<SyncStatus> SyncStatusChanged { get; set; }


        /// <summary>
        /// Local changes have been detected.
        /// </summary>
        public Action ChangesDetected { get; set; }


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
        public SyncStatus Status { get; private set; }


        /// <summary>
        /// Stop syncing momentarily.
        /// </summary>
        public void Suspend()
        {
            Status = SyncStatus.Suspend;
        }

        /// <summary>
        /// Restart syncing.
        /// </summary>
        public void Resume()
        {
            Status = SyncStatus.Idle;
        }


        /// <summary>
        /// Return the synchronized folder's information.
        /// </summary>
        protected RepoInfo RepoInfo { get; set; }


        /// <summary>
        /// Watches the local filesystem for changes.
        /// </summary>
        private Watcher watcher;


        /// <summary>
        /// Interval at which the local and remote filesystems should be polled.
        /// </summary>
        private TimeSpan poll_interval = PollInterval.Short;


        /// <summary>
        /// When the local and remote filesystems were last checked for modifications.
        /// </summary>
        private DateTime last_poll = DateTime.Now;


        /// <summary>
        /// Timer for watching the local and remote filesystems.
        /// </summary>
        private Timers.Timer remote_timer = new Timers.Timer();


        /// <summary>
        /// Intervals for local and remote filesystems polling.
        /// Currently the polling interval is fixed.
        /// </summary>
        private static class PollInterval
        {
            public static readonly TimeSpan Short = new TimeSpan(0, 0, 5, 0);
        }


        /// <summary>
        /// Track whether <c>Dispose</c> has been called.
        /// </summary>
        private bool disposed = false;


        /// <summary>
        /// Constructor.
        /// </summary>
        public RepoBase(RepoInfo repoInfo)
        {
            RepoInfo = repoInfo;
            LocalPath = repoInfo.TargetDirectory;
            Name = Path.GetFileName(LocalPath);
            RemoteUrl = repoInfo.Address;

            Logger.Info("Repo " + repoInfo.Name + " - Set poll interval to " + repoInfo.PollInterval + "ms");
            this.remote_timer.Interval = repoInfo.PollInterval;

            SyncStatusChanged += delegate(SyncStatus status)
            {
                Status = status;
            };

            this.watcher = new Watcher(LocalPath);

            // Main loop syncing every X seconds.
            this.remote_timer.Elapsed += delegate
            {
                int time_comparison = DateTime.Compare(this.last_poll, DateTime.Now.Subtract(this.poll_interval));
                bool time_to_poll = (time_comparison < 0);

                if (time_to_poll)
                {
                    this.last_poll = DateTime.Now;
                }

                // Synchronize.
                SyncInBackground();
            };
            
            ChangesDetected += delegate { };
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
                    this.watcher.Dispose();
                }
                this.disposed = true;
            }
        }


        /// <summary>
        /// Initialize the watcher.
        /// </summary>
        public void Initialize()
        {
            this.watcher.ChangeEvent += OnFileActivity;

            // Sync up everything that changed
            // since we've been offline
            SyncInBackground();

            this.remote_timer.Start();
        }


        /// <summary>
        /// Some file activity has been detected, sync changes.
        /// </summary>
        public void OnFileActivity(object sender, FileSystemEventArgs args)
        {
            ChangesDetected();

            this.watcher.Disable();
            // TODO
            this.watcher.Enable();
        }


        /// <summary>
        /// A conflict has been resolved.
        /// </summary>
        protected internal void OnConflictResolved()
        {
            // ConflictResolved(); TODO
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


    /// <summary>
    /// Current status of the synchronization.
    /// TODO: It was used in SparkleShare for up/down/error but is not useful anymore, should be removed.
    /// </summary>
    public enum SyncStatus
    {
        /// <summary>
        /// Normal operation.
        /// </summary>
        Idle,

        /// <summary>
        /// Synchronization is suspended.
        /// TODO this should be written in XML configuration instead.
        /// </summary>
        Suspend
    }
}
