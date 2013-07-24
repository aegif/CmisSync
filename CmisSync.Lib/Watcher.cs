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
using System.IO;

namespace CmisSync.Lib
{
    /// <summary>
    /// Watches the local filesystem for changes.
    /// </summary>
    public class Watcher : FileSystemWatcher {

        /// <summary>
        /// Event when a local file has changed.
        /// </summary>
        public event EventHandler<FileSystemEventArgs> ChangeEvent;


        /// <summary>
        /// Lock used when modifying EnableRaisingEvents.
        /// </summary>
        private Object thread_lock = new Object ();


        /// <summary>
        /// Whether this object has been disposed or not.
        /// </summary>
        private bool disposed;


        /// <summary>
        /// Constructor.
        /// </summary>
        public Watcher (string path) : base (path)
        {
            IncludeSubdirectories = true;
            EnableRaisingEvents   = true;
            Filter                = "*";

            Changed += OnChanged;
            Created += OnChanged;
            Deleted += OnChanged;
            Renamed += OnChanged;
        }


        /// <summary>
        /// A local modification has happened.
        /// </summary>
        private void OnChanged (object sender, FileSystemEventArgs args)
        {
            // Disabled for now. ChangeEvent(sender, args);
        }


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
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }


        /// <summary>
        /// Enable the watcher.
        /// </summary>
        public void Enable ()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            lock (this.thread_lock)
                EnableRaisingEvents = true;
        }


        /// <summary>
        /// Disable the watcher.
        /// </summary>
        public void Disable ()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            lock (this.thread_lock)
                EnableRaisingEvents = false;
        }
    }
}