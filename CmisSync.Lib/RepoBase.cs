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

    public enum SyncStatus
    {
        Idle,
        SyncUp,
        SyncDown,
        Error,
        Suspend
    }


    public abstract class RepoBase
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(RepoBase));

        public abstract void SyncInBackground();
        public abstract double Size { get; }

        /**
         * <param>new <c>SyncStatus</c> value</param>
         */
        public Action<SyncStatus> SyncStatusChanged { get; set; }

        /**
         * <param>percentage</param>
         * <param>speed</param>
         */
        public Action<double, string> ProgressChanged { get; set; }

        /**
         * <param><c>ChangeSet</c> value</param>
         */
        public Action<ChangeSet> NewChangeSet { get; set; }

        public Action ConflictResolved { get; set; }
        public Action ChangesDetected { get; set; }


        public readonly string LocalPath;
        public readonly string Name;
        public readonly Uri RemoteUrl;
        public SyncStatus Status { get; private set; }


        //public virtual string[] UnsyncedFilePaths
        //{
        //    get
        //    {
        //        return new string[0];
        //    }
        //}

        public void Resume()
        {
            Status = SyncStatus.Idle;
        }

        public void Suspend()
        {
            Status = SyncStatus.Suspend;
        }

        protected RepoInfo RepoInfo { get; set; }


        private Watcher watcher;
        private TimeSpan poll_interval = PollInterval.Short;
        private DateTime last_poll = DateTime.Now;
        private Timers.Timer remote_timer = new Timers.Timer();

        private static class PollInterval
        {
            public static readonly TimeSpan Short = new TimeSpan(0, 0, 5, 0);
            public static readonly TimeSpan Long = new TimeSpan(0, 0, 15, 0);
        }


        public RepoBase(RepoInfo repoInfo)
        {
            RepoInfo = repoInfo;
            LocalPath = repoInfo.TargetDirectory;
            Name = Path.GetFileName(LocalPath);
            RemoteUrl = repoInfo.Address;

            Logger.Info(String.Format("Repo [{0}] - Set poll interval to {1} ms", repoInfo.Name, repoInfo.PollInterval));
            this.remote_timer.Interval = repoInfo.PollInterval;

            SyncStatusChanged += delegate(SyncStatus status)
            {
                Status = status;
            };

            this.watcher = new Watcher(LocalPath);

            this.remote_timer.Elapsed += delegate
            {
                int time_comparison = DateTime.Compare(this.last_poll, DateTime.Now.Subtract(this.poll_interval));
                bool time_to_poll = (time_comparison < 0);

                if (time_to_poll)
                {
                    this.last_poll = DateTime.Now;
                }

                // In the unlikely case that we haven't synced up our
                // changes or the server was down, sync up again
                SyncInBackground();
            };
        }


        public void Initialize()
        {
            this.watcher.ChangeEvent += OnFileActivity;

            // Sync up everything that changed
            // since we've been offline
            SyncInBackground();

            this.remote_timer.Start();
        }


        public void OnFileActivity(object sender, FileSystemEventArgs args)
        {
            ChangesDetected();
            //string relative_path = args.FullPath.Replace(LocalPath, "");

            this.watcher.Disable();
            // TODO
            this.watcher.Enable();
        }


        protected internal void OnConflictResolved()
        {
            ConflictResolved();
        }


        // Recursively gets a folder's size in bytes
        private double CalculateSize(DirectoryInfo parent)
        {
            if (!Directory.Exists(parent.ToString()))
                return 0;

            double size = 0;

            try
            {
                foreach (FileInfo file in parent.GetFiles())
                {
                    if (!file.Exists)
                        return 0;

                    size += file.Length;
                }

                foreach (DirectoryInfo directory in parent.GetDirectories())
                    size += CalculateSize(directory);

            }
            catch (Exception)
            {
                return 0;
            }

            return size;
        }


        public void Dispose()
        {
            this.remote_timer.Stop();
            this.remote_timer.Dispose();

            this.watcher.Dispose();
        }
    }
}
