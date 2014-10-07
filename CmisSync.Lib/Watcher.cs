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
        private List<string> changeList = new List<string>();

        /// <summary>
        /// supported change type
        /// rename a file: <c>Deleted</c> for the old name, and <c>Created</c> for the new name
        /// </summary>
        public enum ChangeTypes
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
        };

        /// <summary>
        /// key is the element in <c>changeList</c>
        /// </summary>
        private Dictionary<string, ChangeTypes> changes = new Dictionary<string, ChangeTypes>();


        /// <summary>
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
        }


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

            EnableRaisingEvents = false;
            EnableEvent = false;
        }


        private void OnCreated(object source, FileSystemEventArgs e)
        {
            Logger.Debug("FS Object creation detected: " + e.FullPath);
            List<ChangeTypes> checks = new List<ChangeTypes>();
            checks.Add(ChangeTypes.Deleted);
            ChangeHandle(e.FullPath, ChangeTypes.Created, checks);
            InvokeChangeEvent(e);
        }


        private void OnDeleted(object source, FileSystemEventArgs e)
        {
            Logger.Debug("FS Object deletion detected: " + e.FullPath);
            List<ChangeTypes> checks = new List<ChangeTypes>();
            checks.Add(ChangeTypes.Created);
            checks.Add(ChangeTypes.Changed);
            ChangeHandle(e.FullPath, ChangeTypes.Deleted, checks);
            InvokeChangeEvent(e);
        }


        private void OnChanged(object source, FileSystemEventArgs e)
        {
            Logger.Debug("FS Object change detected: " + e.FullPath);
            List<ChangeTypes> checks = new List<ChangeTypes>();
            checks.Add(ChangeTypes.Created);
            checks.Add(ChangeTypes.Changed);
            ChangeHandle(e.FullPath, ChangeTypes.Changed, checks);
            InvokeChangeEvent(e);
        }


        private void ChangeHandle(string name, ChangeTypes type, List<ChangeTypes> checks)
        {
            lock (changeLock)
            {
                Debug.Assert(name.StartsWith(Path));
                ChangeTypes oldType;
                if (changes.TryGetValue(name, out oldType))
                {
                    Debug.Assert(checks.Contains(oldType));
                    changeList.Remove(name);
                }
                changeList.Add(name);
                changes[name] = type;
            }
        }


        private void OnRenamed(object source, RenamedEventArgs e)
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
        }


        private void OnError(object source, ErrorEventArgs e)
        {
            Logger.Warn("Error occurred for FileSystemWatcher");
            EnableRaisingEvents = false;
        }


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
    }
}