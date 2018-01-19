// CmisSync, a collaboration and sharing tool.
// Copyright (C) 2010 Hylke Bons <hylkebons@gmail.com>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

using log4net;
using System.Security.Permissions;
using CmisSync.Lib.Cmis;
using System.Threading;


namespace CmisSync.Lib
{
    /// <summary>
    /// Watches the local filesystem for changes.
    /// </summary>
    public class Watcher : FileSystemWatcher
    {
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Watcher));

        /// <summary>
        /// thread lock for <c>changeList</c> and <c>changes</c>
        /// </summary>
        private Object changeLock = new Object();

        /// <summary>
        /// the file/folder pathname (relative to <c>Path</c>) list for changes
        /// </summary>
        private List<WatcherEvent> changeList = new List<WatcherEvent>();


        /// <summary>
        /// Constructor.
        /// </summary>
        public Watcher(string folder)
        {
#if __MonoCS__
            // http://stackoverflow.com/questions/16859372/why-doesnt-the-servicestack-razor-filesystemwatcher-work-on-mono-mac-os-x
            Environment.SetEnvironmentVariable("MONO_MANAGED_WATCHER", "enabled");
#endif

            Path = System.IO.Path.GetFullPath(folder);
            IncludeSubdirectories = true;
            Filter = "*";
            InternalBufferSize = 4 * 1024 * 16;
            NotifyFilter = NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            Error += new ErrorEventHandler(OnError);
            Created += new FileSystemEventHandler(OnCreated);
            Deleted += new FileSystemEventHandler(OnDeleted);
            Changed += new FileSystemEventHandler(OnChanged);
            Renamed += new RenamedEventHandler(OnRenamed);

            EnableRaisingEvents = true;
            EnableEvent = true;
        }


        /// <summary>
        /// Get the change queue.
        /// </summary>
        public Queue<WatcherEvent> GetChangeQueue()
        {
            lock (changeLock)
            {
                Queue<WatcherEvent> changeQueue = new Queue<WatcherEvent>();
                int changeListCount = changeList.Count;
                for (int i = 0; i < changeListCount; i++)
                {
                    WatcherEvent watcherEvent = changeList[i];
                    FileSystemEventArgs change = watcherEvent.GetFileSystemEventArgs();
                    string fileName = System.IO.Path.GetFileName(change.FullPath);
                    string dirName = System.IO.Path.GetDirectoryName(change.FullPath);
                    bool redundantChange = false;
                    if (change.ChangeType == WatcherChangeTypes.Deleted)
                    {
                        //Detect a file/folder move...
                        FileSystemEventArgs nextChange = ((i + 1) < changeListCount) ? changeList[i + 1].GetFileSystemEventArgs() : null;
                        if (nextChange != null)
                        {
                            string nextFileName = System.IO.Path.GetFileName(nextChange.FullPath);
                            string nextDirName = System.IO.Path.GetDirectoryName(nextChange.FullPath);
                            if (nextChange.ChangeType == WatcherChangeTypes.Created &&
                            nextFileName.Equals(fileName) && !nextDirName.Equals(dirName))
                            {
                                //Move detected...
                                change = new MovedEventArgs(WatcherChangeTypes.Renamed, dirName, nextDirName, fileName);
                                ++i; //Skip nextChange
                            }
                        }
                    }
                    else if (change.ChangeType == WatcherChangeTypes.Changed)
                    {
                        //Detect redundant changes...
                        for (int j = i - 1; j >= 0; j--)
                        {
                            //Iterate backwards through the list of changes, find the most recent operation on this specific file
                            if (change.FullPath.Equals(changeList[j].GetFileSystemEventArgs().FullPath))
                            {
                                //If the most recent operation is a created or changed operation this operation is redundant
                                if (changeList[j].GetFileSystemEventArgs().ChangeType == WatcherChangeTypes.Created ||
                                    changeList[j].GetFileSystemEventArgs().ChangeType == WatcherChangeTypes.Changed)
                                {
                                    redundantChange = true;
                                }
                                break;
                            }
                        }
                    }
                    if (redundantChange)
                    {
                        Logger.DebugFormat("Local file change {0} discarded because redundant", change.ChangeType);
                    }
                    else
                    {
                        changeQueue.Enqueue(watcherEvent);
                    }
                }
                return changeQueue;
            }
        }


        /// <summary>
        /// Get the change queue.
        /// </summary>
        public int GetChangeCount()
        {
            lock (changeLock)
            {
                return changeList.Count;
            }
        }

        
        /// <summary>
        /// insert <param name="change"/> in changes.
        /// </summary>
        private void AddChange(FileSystemEventArgs change)
        {
            lock (changeLock)
            {
                Logger.DebugFormat("{0}: {1}", change.ChangeType, change.FullPath);
                changeList.Add(new WatcherEvent(change));
            }
        }


        /// <summary>
        /// remove all from changes
        /// </summary>
        public void Clear()
        {
            lock (changeLock)
            {
                if (Logger.IsDebugEnabled)
                {
                    foreach (WatcherEvent change in changeList)
                    {
                        Logger.Debug("Clearing from change list: " +
                            change.GetFileSystemEventArgs().ChangeType + ": " +
                            change.GetFileSystemEventArgs().Name);
                    }
                }
                changeList.Clear();
            }
        }

        private void OnCreated(object source, FileSystemEventArgs e)
        {
            ChangeHandle(e);
        }
        private void OnDeleted(object source, FileSystemEventArgs e)
        {
            ChangeHandle(e);
        }
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            ChangeHandle(e);
        }
        private void OnRenamed(object source, RenamedEventArgs e)
        {
            ChangeHandle(e);
        }
        private void ChangeHandle(FileSystemEventArgs change)
        {
            Debug.Assert(change.FullPath.StartsWith(Path), String.Format("Invalid change path {0} for watcher {1}.", change.FullPath, Path));
            AddChange(change);
            InvokeChangeEvent(change);
        }
        private void OnError(object source, ErrorEventArgs e)
        {
            Logger.Error("Error occurred for FileSystemWatcher");
            EnableRaisingEvents = false;
        }


        /// <summary>
        /// Whether this object has been disposed or not.
        /// </summary>
        private bool disposed = false;


        /// <summary>
        /// Dispose of the watcher.
        /// Called by RepoBase.Dispose via System.ComponentModel
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }


        /// <summary>
        /// Event when a local file has changed
        /// </summary>
        public event EventHandler<FileSystemEventArgs> ChangeEvent;

        /// <summary>
        /// Whether to enable <c>ChangeEvent</c>
        /// </summary>
        public bool EnableEvent { get; set; }

        private void InvokeChangeEvent(FileSystemEventArgs args)
        {
            if (EnableEvent)
            {
                ChangeEvent(this, args);
            }
        }

        /// <summary>
        /// Provides data for a file moved event.
        /// </summary>
        public class MovedEventArgs : FileSystemEventArgs
        {
            private string oldDirectory;
            private string oldFullPath;

            /// <summary>
            /// Constructor.
            /// </summary>
            public MovedEventArgs(WatcherChangeTypes changeType, string oldDirectory, string directory, string name)
                : base(changeType, directory, name)
            {
                // Ensure that the directory name ends with a "\"
                if (!oldDirectory.EndsWith("\\", StringComparison.Ordinal))
                {
                    oldDirectory = oldDirectory + "\\";
                }

                // Ensure that the directory name ends with a "\"
                if (!directory.EndsWith("\\", StringComparison.Ordinal))
                {
                    directory = directory + "\\";
                }
                
                this.oldDirectory = oldDirectory;
                this.oldFullPath = System.IO.Path.Combine(oldDirectory, name);
            }

            /// <summary>
            /// Gets the previous fully qualified path of the affected file or directory.
            /// </summary>
            public string OldFullPath
            {
                get
                {
                    new FileIOPermission(FileIOPermissionAccess.Read, System.IO.Path.GetPathRoot(oldFullPath)).Demand();
                    return oldFullPath;
                }
            }
        }
    }


    /// <summary>
    /// A grace time is accorded to deletion events before initiating any server-side deletion.
    /// 
    /// In many programs (like Microsoft Word), deletion is often just a save:
    /// 1. Save data to temporary file ~wrdxxxx.tmp
    /// 2. Delete Example.doc
    /// 3. Rename ~wrdxxxx.tmp to Example.doc
    /// See https://support.microsoft.com/en-us/kb/211632
    /// So, upon deletion, wait a bit for any save operation to hopefully finalize, then sync.
    /// This is not 100% foolproof, as saving can last for more than the grace time, but probably
    /// the best we can do without mind-reading third-party programs.
    /// </summary>
    public class Grace
    {
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Grace));


        /// <summary>
        /// Approximate date of the event.
        /// A few seconds of error is not a problem.
        /// </summary>
        private DateTime graceEnd;


        /// <summary>
        /// The grace time to wait for.
        /// Expressed in seconds.
        /// </summary>
        public static int GRACE_TIME = 15;


        /// <summary>
        /// Create a new Grace starting from now.
        /// </summary>
        public Grace()
        {
            // GRACE_TIME seconds from now.
            graceEnd = DateTime.Now.Add(new TimeSpan(0, 0, GRACE_TIME));
        }


        /// <summary>
        /// Wait until the grace time has expired.
        /// </summary>
        public void WaitGraceTime()
        {
            while (DateTime.Now < graceEnd)
            {
                Logger.Debug("Waiting grace time");
                Thread.Sleep(1000); // Wait a second.
            }
            Logger.Debug("Grace time reached");
        }
    }


    /// <summary>
    /// An event detected by the watcher.
    /// </summary>
    public class WatcherEvent
    {
        /// <summary>
        /// Object describing the change.
        /// </summary>
        private FileSystemEventArgs args;


        /// <summary>
        /// Grace time associated to the change.
        /// If the change is a deletion, we will wait until this time has passed before initiating any server-side deletion.
        /// </summary>
        private Grace grace;


        /// <summary>
        /// Constructor.
        /// </summary>
        public WatcherEvent(FileSystemEventArgs args)
        {
            this.args = args;

            // Start the grace time count.
            // A few seconds might have passed already, but we don't care since grace time is around tenfold that.
            grace = new Grace();
        }


        public FileSystemEventArgs GetFileSystemEventArgs()
        {
            return args;
        }


        public Grace GetGrace()
        {
            return grace;
        }
    }
}