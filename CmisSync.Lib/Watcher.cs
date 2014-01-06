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
        private Queue<Tuple<string, ChangeTypes>> changeQueue = new Queue<Tuple<string, ChangeTypes>>();

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
        /// Get the change queue.
        /// </summary>
        public int GetChangeCount()
        {
            lock (changeLock)
            {
                return changeQueue.Count;
            }
        }


        /// <summary>
        /// Get the earliest change from the queue.
        /// </summary>
        public Tuple<string, ChangeTypes> GetEarliestChange()
        {
            lock (changeLock)
            {
                return changeQueue.Peek();
            }
        }

        /// <summary>
        /// Remove the earliest change from the queue.
        /// </summary>
        public Tuple<string, ChangeTypes> RemoveEarliestChange()
        {
            lock (changeLock)
            {
                return changeQueue.Dequeue();
            }
        }

        /// <summary>
        /// insert <param name="name">the file/folder</param> for <param name="change"/> in changes
        /// It should do nothing if the file/folder exists in changes
        /// </summary>
        public void AddChange(string name, ChangeTypes change)
        {
            if (ChangeTypes.None == change)
            {
                return;
            }

            lock (changeLock)
            {
                Logger.Debug(change + ": " + name);
                changeQueue.Enqueue(new Tuple<string, ChangeTypes>(name, change));
            }
        }


        /// <summary>
        /// remove all from changes
        /// </summary>
        public void RemoveAll()
        {
            lock (changeLock)
            {
                if (Logger.IsDebugEnabled)
                {
                    foreach (Tuple<string, ChangeTypes> change in changeQueue)
                    {
                        Logger.Debug("Removing -> " + change.Item2 + ": " + change.Item1);
                    }

                }
                changeQueue.Clear();
            }
        }


        /// <summary>
        /// Constructor.
        /// </summary>
        public Watcher(string folder)
        {
#if __MonoCS__
            //  http://stackoverflow.com/questions/16859372/why-doesnt-the-servicestack-razor-filesystemwatcher-work-on-mono-mac-os-x
            Environment.SetEnvironmentVariable("MONO_MANAGED_WATCHER", "enabled");
#endif

            Path = System.IO.Path.GetFullPath(folder);
            IncludeSubdirectories = true;
            Filter = "*";
            InternalBufferSize = 4 * 1024 * 16;

            Error += new ErrorEventHandler(OnError);
            Created += new FileSystemEventHandler(OnCreated);
            Deleted += new FileSystemEventHandler(OnDeleted);
            Changed += new FileSystemEventHandler(OnChanged);
            Renamed += new RenamedEventHandler(OnRenamed);

            EnableRaisingEvents = true;
            EnableEvent = true;
        }


        private void OnCreated(object source, FileSystemEventArgs e)
        {
            ChangeHandle(e.FullPath, ChangeTypes.Created);
            InvokeChangeEvent(e);
        }


        private void OnDeleted(object source, FileSystemEventArgs e)
        {
            ChangeHandle(e.FullPath, ChangeTypes.Deleted);
            InvokeChangeEvent(e);
        }


        private void OnChanged(object source, FileSystemEventArgs e)
        {
            ChangeHandle(e.FullPath, ChangeTypes.Changed);
            InvokeChangeEvent(e);
        }


        private void ChangeHandle(string name, ChangeTypes type)
        {
            lock (changeLock)
            {
                Debug.Assert(name.StartsWith(Path));
                AddChange(name, type);
            }
        }


        private void OnRenamed(object source, RenamedEventArgs e)
        {
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
            Logger.Error("Error occurred for FileSystemWatcher");
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
