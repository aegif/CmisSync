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
        private List<FileSystemEventArgs> changeList = new List<FileSystemEventArgs>();

        /// <summary>
        /// supported change type
        /// rename a file: <c>Deleted</c> for the old name, and <c>Created</c> for the new name
        /// </summary>
        /*public enum ChangeTypes
        {
            /// <summary>
            /// no change for the file/folder
            /// </summary>
            None,

            /// <summary>
            /// a new file/folder is created
            /// </summary>
            Created,

            /// <summary>
            /// the content for the file is changed
            /// </summary>
            Changed,

            /// <summary>
            /// the file/folder is deleted
            /// </summary>
            Deleted
        };*/

        /// <summary>
        /// key is the element in <c>changeList</c>
        /// </summary>
        //private Dictionary<string, ChangeTypes> changes = new Dictionary<string, ChangeTypes>();


        /*/// <summary>
        /// <returns>the file/folder pathname (relative to <c>Path</c>) list for changes</returns>
        /// </summary>
        public List<string> GetChangeList()
        {
            lock (changeLock)
            {
                return new List<string>(changeList);
            }
        }


        /// <summary>
        /// <returns><c>ChangeTypes</c> for <param name="name">the file/folder</param></returns>
        /// </summary>
        public ChangeTypes GetChangeType(string name)
        {
            lock (changeLock)
            {
                ChangeTypes type;
                if (changes.TryGetValue(name, out type))
                {
                    return type;
                }
                else
                {
                    return ChangeTypes.None;
                }
            }
        }


        /// <summary>
        /// remove <param name="name">the file/folder</param> from changes
        /// </summary>
        public void RemoveChange(string name)
        {
            lock (changeLock)
            {
                if (changes.Remove(name))
                {
                    changeList.Remove(name);
                }
            }
        }


        /// <summary>
        /// remove <param name="name">the file/folder</param> from changes for <param name="change"/>
        /// </summary>
        public void RemoveChange(string name, ChangeTypes change)
        {
            lock (changeLock)
            {
                ChangeTypes type;
                if (changes.TryGetValue(name, out type))
                {
                    if (type == change)
                    {
                        changes.Remove(name);
                        changeList.Remove(name);
                    }
                }
            }
        }

        /// <summary>
        /// Insert the file/folder for change parameter.
        /// It should do nothing if the file/folder exists in changes.
        /// </summary>
        /// <param name="name">the file/folder</param>
        /// <param name="change"></param>
        public void InsertChange(string name, ChangeTypes change)
        {
            if (ChangeTypes.None == change)
            {
                return;
            }

            lock (changeLock)
            {
                if (!changes.ContainsKey(name))
                {
                    changeList.Add(name);
                    changes[name] = change;
                }
            }
        }


        /// <summary>
        /// remove all from changes
        /// </summary>
        public void RemoveAll()
        {
            lock (changeLock)
            {
                changes.Clear();
                changeList.Clear();
            }
        }


        /// <summary>
        /// Get the number of elements in the change queue.
        /// </summary>
        public int GetChangeCount()
        {
            lock (changeLock)
            {
                return changeList.Count;
            }
        }*/


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
        public Queue<FileSystemEventArgs> GetChangeQueue()
        {
            lock (changeLock)
            {
                Queue<FileSystemEventArgs> changeQueue = new Queue<FileSystemEventArgs>();
                int changeListCount = changeList.Count;
                for (int i = 0; i < changeListCount; i++)
                {
                    FileSystemEventArgs change = changeList[i];
                    string fileName = System.IO.Path.GetFileName(change.FullPath);
                    string dirName = System.IO.Path.GetDirectoryName(change.FullPath);
                    bool redundantChange = false;
                    if (change.ChangeType == WatcherChangeTypes.Deleted)
                    {
                        //Detect a file/folder move...
                        FileSystemEventArgs nextChange = ((i + 1) < changeListCount) ? changeList[i + 1] : null;
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
                            if (change.FullPath.Equals(changeList[j].FullPath))
                            {
                                //If the most recent operation is a created or changed operation this operation is redundant
                                if (changeList[j].ChangeType == WatcherChangeTypes.Created ||
                                changeList[j].ChangeType == WatcherChangeTypes.Changed)
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
                        changeQueue.Enqueue(change);
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
                changeList.Add(change);
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
                    foreach (FileSystemEventArgs change in changeList)
                    {
                        Logger.Debug("Clearing from change list: " + change.ChangeType + ": " + change.Name);
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


        /*private void OnRenamed(object source, RenamedEventArgs e)
        {
            Logger.Debug("FS Object renaming detected: " + e.OldFullPath + " to " + e.FullPath);
            string oldname = e.OldFullPath;
            string newname = e.FullPath;
            if (oldname.StartsWith(Path))
            {
                FileSystemEventArgs eventDelete = new FileSystemEventArgs(
                    WatcherChangeTypes.Deleted,
                    System.IO.Path.GetDirectoryName(oldname),
                    System.IO.Path.GetFileName(oldname));
                OnDeleted(source, eventDelete);
            }
            if (newname.StartsWith(Path))
            {
                FileSystemEventArgs eventCreate = new FileSystemEventArgs(
                    WatcherChangeTypes.Created,
                    System.IO.Path.GetDirectoryName(newname),
                    System.IO.Path.GetFileName(newname));
                OnCreated(source, eventCreate);
            }
            InvokeChangeEvent(e);
        }*/


        /// <summary>
        /// Whether this object has been disposed or not.
        /// </summary>
        private bool disposed;


        /// <summary>
        /// Dispose of the watcher.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                }
                disposed = true;
            }
            base.Dispose(disposing);
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
}