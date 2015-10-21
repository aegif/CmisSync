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

using log4net;
using System.Security.Permissions;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Utils;
using System.Timers;
using System.Collections.Generic;


namespace CmisSync.Lib
{
    /// <summary>
    /// Watches the local filesystem for changes.
    /// </summary>
    public class Watcher : IDisposable
    {
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Watcher));

        private FileSystemWatcher fileSystemWatcher;

        /// <summary>
        /// The thime (in milliseconds) we should wait for other events before the last received one.
        /// This is to ensure that a multi event operation (move, copy, tree delete) has finished.
        /// </summary>
        private const int EVENTS_QUARANTINE_TIME = 5000;

        private const int MAX_QUEUED_EVENTS = 10000;

        /// <summary>
        /// the file/folder pathname (relative to <c>Path</c>) list for changes
        /// </summary>
        private LimitedQueue<FileSystemEventArgs> _changesList = new LimitedQueue<FileSystemEventArgs>(MAX_QUEUED_EVENTS);

        private DateTime lastChangeTime;

        private Timer event_timer;

        /// <summary>
        /// Constructor.
        /// </summary>
        public Watcher(string folder)
        {
#if __MonoCS__
            // http://stackoverflow.com/questions/16859372/why-doesnt-the-servicestack-razor-filesystemwatcher-work-on-mono-mac-os-x
            Environment.SetEnvironmentVariable("MONO_MANAGED_WATCHER", "enabled");
#endif

            fileSystemWatcher = new FileSystemWatcher()
            {
                Path = System.IO.Path.GetFullPath(folder)
            };

            fileSystemWatcher.IncludeSubdirectories = true;
            fileSystemWatcher.Filter = "*";
            fileSystemWatcher.InternalBufferSize = 4 * 1024 * 16;
            fileSystemWatcher.NotifyFilter = NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            fileSystemWatcher.Error += new ErrorEventHandler(OnError);
            fileSystemWatcher.Created += new FileSystemEventHandler(OnCreated);
            fileSystemWatcher.Deleted += new FileSystemEventHandler(OnDeleted);
            fileSystemWatcher.Changed += new FileSystemEventHandler(OnChanged);
            fileSystemWatcher.Renamed += new RenamedEventHandler(OnRenamed);

            event_timer = new Timer()
            {
                AutoReset = false,
                Interval = EVENTS_QUARANTINE_TIME + 1
            };
            event_timer.Enabled = false;
            event_timer.Elapsed += event_timer_Elapsed;


            fileSystemWatcher.EnableRaisingEvents = true;
            EnableEvent = true;
        }

        void event_timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            InvokeChangeEvent();
        }

        /// <summary>
        /// Get the change queue.
        /// </summary>
        public LinkedList<FileSystemEventArgs> GetChanges()
        {
            LinkedList<FileSystemEventArgs> changes;
            lock (this._changesList)
            {
                changes = new LinkedList<FileSystemEventArgs>(this._changesList);
                this._changesList.Clear();
            }

            if (Logger.IsDebugEnabled)
            {
                if (lastChangeTime != null && lastChangeTime.AddMilliseconds(EVENTS_QUARANTINE_TIME) > DateTime.Now)
                {
                    Logger.Warn("The changes list has been retrieved before the EVENTS_QUARANTINE_TIME (" + EVENTS_QUARANTINE_TIME + "). Some events may be missings.");
                }
            }

            Utils.LinkedListIterator<FileSystemEventArgs> iterator = new Utils.LinkedListIterator<FileSystemEventArgs>(changes, Utils.LinkedListIterator<FileSystemEventArgs>.InitialPosition.End);
            //LinkedListNode<FileSystemEventArgs> node = changes.Last;
            while (iterator.HasPrevious())
            {
                FileSystemEventArgs change = iterator.Previous();
                switch (change.ChangeType)
                {
                    case WatcherChangeTypes.Changed:
                        handleChanged(iterator);
                        break;
                    case WatcherChangeTypes.Created:
                        handleCreated(iterator);
                        break;
                    case WatcherChangeTypes.Deleted:
                        handleDeleted(iterator);
                        break;
                    case WatcherChangeTypes.Renamed:
                        handleRenamed(iterator);
                        break;
                    default:
                        Logger.Warn("Unattended WatcherChangeTypes: change.ChangeType");
                        continue;
                }
            }
            return changes;
        }

        private void handleChanged(Utils.LinkedListIterator<FileSystemEventArgs> iterator)
        {
            Utils.LinkedListIterator<FileSystemEventArgs> innerIterator = iterator.Clone();
            while (innerIterator.HasPrevious())
            {
                FileSystemEventArgs previousChange = innerIterator.Previous();

                //Iterate backwards through the list of changes, find any prior operations on this specific file
                if (iterator.Current.FullPath.Equals(previousChange.FullPath))
                {
                    switch (previousChange.ChangeType)
                    {
                        case WatcherChangeTypes.Changed:
                            handleChanged(innerIterator);
                            //now this is the last prior Changed event
                            //remove it, keep the recent one (node)
                            if (innerIterator.Current != null)
                            {
                                innerIterator.Remove();
                            }
                            return;
                        case WatcherChangeTypes.Created:
                            handleCreated(innerIterator);
                            switch (previousChange.ChangeType)
                            {
                                case WatcherChangeTypes.Changed:
                                    innerIterator.Remove();
                                    break;
                                case WatcherChangeTypes.Created:
                                    iterator.Remove();
                                    return;
                                default:
                                    throw new InvalidOperationException("Unattended change type: " + previousChange.ChangeType);
                            }
                            return;
                        case WatcherChangeTypes.Deleted:
                            Logger.Warn("Found Deleted ChangeType before a Changed widouth a Create for file '" + iterator.Current.FullPath + "': will be ignored (probably the Created has been lost)");
                            innerIterator.Remove();
                            break;
                        case WatcherChangeTypes.Renamed:
                            //FullPath is the newPath of the renamed file.
                            //keep the event
                            break;
                        default:
                            Logger.Warn("Unattended WatcherChangeTypes:" + iterator.Current.ChangeType);
                            continue;
                    }
                }
            }
        }

        private void handleCreated(Utils.LinkedListIterator<FileSystemEventArgs> iterator)
        {
            Utils.LinkedListIterator<FileSystemEventArgs> innerIterator = iterator.Clone();
            while (innerIterator.HasPrevious())
            {
                FileSystemEventArgs previousChange = innerIterator.Previous();

                if (iterator.Current.FullPath.Equals(previousChange.FullPath))
                {
                    switch (previousChange.ChangeType)
                    {
                        case WatcherChangeTypes.Changed:
                            Logger.Warn("Found Changed ChangeType before a Created for file '" + iterator.Current.FullPath + "': will be ignored (probably some changes had been lost)");
                            innerIterator.Remove();
                            break;
                        case WatcherChangeTypes.Created:
                            Logger.Warn("Found duplicated Created ChangeType for file '" + iterator.Current.FullPath + "': will be ignored (probably some changes had been lost)");
                            break;
                        case WatcherChangeTypes.Deleted:
                            iterator.Current = new FileSystemEventArgs(
                                WatcherChangeTypes.Changed,
                                Path.GetDirectoryName(iterator.Current.FullPath),
                                Path.GetFileName(iterator.Current.FullPath));
                            innerIterator.Remove();
                            break;
                        case WatcherChangeTypes.Renamed:
                            //ok
                            Logger.Warn("Found Renamed ChangeType before Create for file '" + iterator.Current.FullPath + "': will be ignored (probably some changes had been lost)");
                            break;
                        default:
                            Logger.Warn("Unattended WatcherChangeTypes:" + iterator.Current.ChangeType);
                            continue;
                    }
                }
                else // ! change.FullPath.Equals(previousChange.FullPath)
                {
                    string fileName = System.IO.Path.GetFileName(iterator.Current.FullPath);
                    string dirPath = System.IO.Path.GetDirectoryName(iterator.Current.FullPath);
                    string previousName = System.IO.Path.GetFileName(previousChange.FullPath);
                    string previousDirPath = System.IO.Path.GetDirectoryName(previousChange.FullPath);
                    if (previousName.Equals(fileName) && !previousDirPath.Equals(dirPath))
                    {
                        //Move detected...
                        iterator.Current = new MovedEventArgs(WatcherChangeTypes.Renamed, previousDirPath, dirPath, fileName);
                        innerIterator.Remove();
                        break;
                    }
                }
            }
        }

        private void handleDeleted(Utils.LinkedListIterator<FileSystemEventArgs> iterator)
        {
            Utils.LinkedListIterator<FileSystemEventArgs> innerIterator = iterator.Clone();
            while (innerIterator.HasPrevious())
            {
                FileSystemEventArgs previousChange = innerIterator.Previous();

                if (Object.Equals(iterator.Current.FullPath, previousChange.FullPath))
                {
                    switch (previousChange.ChangeType)
                    {
                        case WatcherChangeTypes.Changed:
                            innerIterator.Remove();
                            break;
                        case WatcherChangeTypes.Created:
                            innerIterator.Remove();
                            iterator.Remove();
                            return;
                        case WatcherChangeTypes.Deleted:
                            Logger.Warn("Found duplicated Delete for file '" + iterator.Current.FullPath + "': will be ignored");
                            innerIterator.Remove();
                            break;
                        case WatcherChangeTypes.Renamed:
                            //check for file swap: first check for another rename between this and the delete
                            handleFileSwap(innerIterator, iterator);
                            if (innerIterator.Current != null)
                            {
                                handleRenamed(innerIterator);
                                switch (previousChange.ChangeType)
                                {
                                    case WatcherChangeTypes.Created:
                                        innerIterator.Remove();
                                        iterator.Remove();
                                        return;
                                    case WatcherChangeTypes.Renamed:
                                        innerIterator.Remove();
                                        RenamedEventArgs rename = previousChange as RenamedEventArgs;
                                        iterator.Current = new FileSystemEventArgs(
                                            WatcherChangeTypes.Deleted,
                                            Path.GetDirectoryName(rename.OldFullPath),
                                            Path.GetFileName(rename.OldFullPath));
                                        return;
                                    default:
                                        throw new InvalidOperationException("Unattended change type: " + previousChange.ChangeType);
                                }
                            }
                            break;
                        default:
                            Logger.Warn("Unattended WatcherChangeTypes:" + iterator.Current.ChangeType);
                            continue;
                    }
                }
            }
        }

        private void handleFileSwap(LinkedListIterator<FileSystemEventArgs> renameIterator, LinkedListIterator<FileSystemEventArgs> deleteIterator)
        {
            FileSystemEventArgs delete = deleteIterator.Current;
            Utils.LinkedListIterator<FileSystemEventArgs> innerIterator = renameIterator.Clone();
            while (innerIterator.hasNext())
            { 
                FileSystemEventArgs nextChange = innerIterator.Next();
                if(nextChange == delete) {
                    return;
                }
                RenamedEventArgs erlierRename = renameIterator.Current as RenamedEventArgs;
                RenamedEventArgs rename = nextChange as RenamedEventArgs;
                if (rename != null && Object.Equals(erlierRename.OldFullPath, rename.FullPath))
                {
                    //this is a file swap!
                    renameIterator.Remove();
                    innerIterator.Remove();
                    deleteIterator.Current = new FileSystemEventArgs(
                        WatcherChangeTypes.Changed,
                        Path.GetDirectoryName(erlierRename.OldFullPath),
                        Path.GetFileName(erlierRename.OldFullPath));

                    //remove any events related to temporary swap file (rename.OldFullPath)
                    string tmpFileFullPath = rename.OldFullPath;
                    Utils.LinkedListIterator<FileSystemEventArgs> tmpIterator = renameIterator.Clone();
                    while (tmpIterator.HasPrevious())
                    { 
                        FileSystemEventArgs e = tmpIterator.Previous();
                        if(Object.Equals(e.FullPath, tmpFileFullPath))
                        {
                            if(e.ChangeType == WatcherChangeTypes.Renamed){
                                handleRenamed(tmpIterator);
                                if (tmpIterator.Current != null && tmpIterator.Current.ChangeType == WatcherChangeTypes.Renamed)
                                {
                                    tmpFileFullPath = ((RenamedEventArgs)tmpIterator.Current).OldFullPath;
                                }
                            }
                            if (tmpIterator.Current != null)
                            {
                                tmpIterator.Remove();
                            }
                        }
                    }
                }
            }
        }

        private void handleRenamed(Utils.LinkedListIterator<FileSystemEventArgs> iterator)
        {
            Utils.LinkedListIterator<FileSystemEventArgs> innerIterator = iterator.Clone();
            while (innerIterator.HasPrevious())
            {
                FileSystemEventArgs previousChange = innerIterator.Previous();

                if (Object.Equals(iterator.Current.FullPath, previousChange.FullPath))
                {
                    switch (previousChange.ChangeType)
                    {
                        case WatcherChangeTypes.Changed:
                            handleChanged(innerIterator);
                            break;
                        case WatcherChangeTypes.Created:
                            innerIterator.Current = new FileSystemEventArgs(
                                WatcherChangeTypes.Created,
                                Path.GetDirectoryName(iterator.Current.FullPath),
                                Path.GetFileName(iterator.Current.FullPath));
                            previousChange = innerIterator.Current;
                            iterator.Remove();
                            return;
                        case WatcherChangeTypes.Deleted:
                            //ok, the old file with the same name has been deleted
                            break;
                        case WatcherChangeTypes.Renamed:
                            throw new InvalidOperationException();
                        default:
                            Logger.Warn("Unattended WatcherChangeTypes:" + iterator.Current.ChangeType);
                            continue;
                    }
                }
            }
        }

        public int GetChangeCount()
        {
            lock (_changesList)
            {
                return _changesList.Count;
            }
        }

        /// <summary>
        /// insert <param name="change"/> in changes.
        /// </summary>
        private void AddChange(FileSystemEventArgs change)
        {
            lock (_changesList)
            {
                if (change is RenamedEventArgs)
                {
                    Logger.DebugFormat("{0}: {1} -> {2}", change.ChangeType, ((RenamedEventArgs)change).OldFullPath, change.FullPath);
                }
                else
                {
                    Logger.DebugFormat("{0}: {1}", change.ChangeType, change.FullPath);
                }

                _changesList.Enqueue(change);

                if (Logger.IsDebugEnabled)
                {
                    this.lastChangeTime = DateTime.Now;
                    if (_changesList.DequeuedItemsDueToLimit() > 0)
                    {
                        Logger.Warn("Some events lost due to event queue full.");
                    }
                }
            }
        }
        /// <summary>
        /// remove all from changes
        /// </summary>
        public void Clear()
        {
            lock (_changesList)
            {
                if (Logger.IsDebugEnabled)
                {
                    foreach (FileSystemEventArgs change in _changesList)
                    {
                        Logger.Debug("Clearing from change list: " + change.ChangeType + ": " + change.Name);
                    }
                }
                _changesList.Clear();
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
            Debug.Assert(change.FullPath.StartsWith(fileSystemWatcher.Path), String.Format("Invalid change path {0} for watcher {1}.", change.FullPath, fileSystemWatcher.Path));
            AddChange(change);

            event_timer.Stop();
            event_timer.Start();//reset
        }
        private void OnError(object source, ErrorEventArgs e)
        {
            Logger.Error("Error occurred for FileSystemWatcher", e.GetException());
        }

        /// <summary>
        /// Event when a local file has changed
        /// </summary>
        public event EventHandler ChangeEvent;

        public bool Enable
        {
            get { return fileSystemWatcher.EnableRaisingEvents; }
            set { fileSystemWatcher.EnableRaisingEvents = value; }
        }

        private bool _enableEvent = true;
        /// <summary>
        /// Whether to enable <c>ChangeEvent</c>. 
        /// If set to false the events will be still collected, but not reported until enabled again.
        /// </summary>
        public bool EnableEvent
        {
            get { return _enableEvent; }
            set
            {
                if (_enableEvent != value)
                {
                    _enableEvent = value;
                    event_timer.Enabled = value;
                    if (_enableEvent = true && _changesList.Count > 0)
                    {
                        InvokeChangeEvent();
                    }
                }
            }
        }

        private void InvokeChangeEvent()
        {
            if (EnableEvent && ChangeEvent != null)
            {
                ChangeEvent(this, new EventArgs());
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

        #region IDisposable Members

        private bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    fileSystemWatcher.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion IDisposable Members
    }
}