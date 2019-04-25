using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

using CmisSync.Lib.Config;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Sync.SyncMachine.Crawler;
using CmisSync.Lib.Sync.SyncMachine.Exceptions;
using CmisSync.Lib.Sync.SyncMachine.Internal;
using CmisSync.Lib.Utilities.FileUtilities;



namespace CmisSync.Lib.Sync.SyncMachine.Crawler
{
    public class LocalWatcherProcessor : IDisposable
    {
        private BlockingCollection<SyncTriplet.SyncTriplet> outputQueue = null;

        private ItemsDependencies itemsDeps = null;

        private HashSet<string> possibleProcessedParentBuffer = new HashSet<string> ();

        private HashSet<string> duplicatedTripletBuffer = new HashSet<string> ();

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private Watcher _watcher = null;

        public LocalWatcherProcessor (
            CmisSyncFolder.CmisSyncFolder folder,
            Watcher watcher,
            BlockingCollection<SyncTriplet.SyncTriplet> semi,
            ItemsDependencies idps) 
        {
            this.cmisSyncFolder = folder;
            this._watcher = watcher;
            this.outputQueue = semi;
            this.itemsDeps = idps; 
        }

        public void Start()
        {
            Queue<WatcherEvent> changes = _watcher.GetChangeQueue ();
            _watcher.Clear ();

            foreach (WatcherEvent change in changes) {
                var e = change.GetFileSystemEventArgs ();

                if (e.FullPath.StartsWith(cmisSyncFolder.LocalPath, StringComparison.CurrentCulture)) {

                    if (!File.Exists (e.FullPath) && !Directory.Exists (e.FullPath)) {
                        Console.WriteLine ("%% Local file/folder: {0} not found, might be deleted.", e.FullPath);
                        continue;
                    }

                    SyncTriplet.SyncTriplet triplet = null;

                    // only do item dependencies resolving in create and change events.
                    switch (e.ChangeType) {
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Changed:
                        // if !exist, not worth syncing
                        if (SyncFileUtil.WorthSyncing (e.FullPath, cmisSyncFolder)) {

                            triplet = Utils.IsFolder (e.FullPath) ?
                                SyncTripletFactory.CreateFromLocalFolder (e.FullPath, cmisSyncFolder) :
                                SyncTripletFactory.CreateFromLocalDocument (e.FullPath, cmisSyncFolder);
                        }

                        break;
                    case WatcherChangeTypes.Renamed:
                        // TODO:
                        // why should I grace?
                        bool isFolder = Utils.IsFolder (e.FullPath);

                        string oldFullPath = ((RenamedEventArgs)e).OldFullPath;
                        string newFullPath = e.FullPath;

                        bool oldPathnameWorthSyncing = SyncFileUtil.WorthSyncing (oldFullPath, isFolder, cmisSyncFolder);
                        bool newPathnameWorthSyncing = SyncFileUtil.WorthSyncing (newFullPath, isFolder, cmisSyncFolder);

                        // case both worth sycning
                        if (oldPathnameWorthSyncing && newPathnameWorthSyncing) {
                            // if oldItem not exist in db, do update
                            if (!DataBaseContainsLocalObject (cmisSyncFolder.Database, oldFullPath, isFolder)) {
                                triplet = isFolder ?
                                    SyncTripletFactory.CreateFromLocalFolder (newFullPath, cmisSyncFolder) :
                                    SyncTripletFactory.CreateFromLocalDocument (newFullPath, cmisSyncFolder);
                            } else {

                                // oldItem DBexist && newItem DBexist, equals update new file
                                if (DataBaseContainsLocalObject (cmisSyncFolder.Database, newFullPath, isFolder)) {
                                    DoChangeGraceWait (change.GetGrace ());
                                    // upgrade the new file
                                    triplet = isFolder ?
                                        SyncTripletFactory.CreateFromLocalFolder (newFullPath, cmisSyncFolder) :
                                        SyncTripletFactory.CreateFromLocalDocument (newFullPath, cmisSyncFolder);
                                } else {
                                    // oldItem DBexist && !newItem DBexist, do rename
                                    bool newDBExist = DataBaseContainsLocalObject (cmisSyncFolder.Database, newFullPath, isFolder);
                                    triplet = SyncTripletFactory.CreateFromLocalRenamedObject (oldFullPath, newFullPath, isFolder, cmisSyncFolder);
                                }
                            }
                        } else {
                            // case only the new one worth syncing, create
                            if (!oldPathnameWorthSyncing && newPathnameWorthSyncing) {
                                triplet = isFolder  ?
                                    SyncTripletFactory.CreateFromLocalFolder (e.FullPath, cmisSyncFolder) :
                                    SyncTripletFactory.CreateFromLocalDocument (e.FullPath, cmisSyncFolder);
                            } else if (oldPathnameWorthSyncing && !newPathnameWorthSyncing) {
                                // case both not worth syncing, do grace
                                DoChangeGraceWait (change.GetGrace ());
                            }
                        }
                        break;
                    case WatcherChangeTypes.Deleted:
                        DoChangeGraceWait (change.GetGrace ());
                        break;
                    default:
                        break;
                    }

                    if (triplet != null) {

                        // if triplet is not null, push it to output queue.
                        string localpath = triplet.LocalStorage.RelativePath;

                        // push the triplet's parent to possible_processed_parent_buffer
                        String parent = CmisFileUtil.GetUpperFolderOfCmisPath (localpath);
                        if (parent.Length > 0) {
                            parent = parent + CmisUtils.CMIS_FILE_SEPARATOR;

                            // current object depends on its parent
                            itemsDeps.AddItemDependence (
                                triplet.IsFolder ? localpath + CmisUtils.CMIS_FILE_SEPARATOR : localpath,
                                parent);
                            possibleProcessedParentBuffer.Add (parent);
                        }

                        if (triplet.IsFolder) {
                            possibleProcessedParentBuffer.Remove (triplet.Name);
                        }

                        if (!duplicatedTripletBuffer.Contains (triplet.Name)) {
                            outputQueue.TryAdd (triplet);
                            duplicatedTripletBuffer.Add (triplet.Name);
                            itemsDeps.OutputItemsDependences ();
                        }

                        Console.WriteLine ("%% Change: {1}, SyncTriplet: \n {0}", triplet.Name, change.GetFileSystemEventArgs ().GetType ());

                    } else {
                        Console.WriteLine ("%% Local file/folder: {0} not found, might be deleted.", e.FullPath);
                    }
                }
            }

            /*
             * It is possible that the parent of an object [o] is already acquired before [o] comes.
             * So the previous possibleProcessedParentBuffer.Remove will not work
             */
            foreach (string unincluded in possibleProcessedParentBuffer) {
                if (!duplicatedTripletBuffer.Contains (unincluded)) {
                    Console.WriteLine (" - triplet {0} is not included in the current changes, remove it from idps.", unincluded);
                    itemsDeps.RemoveItemDependence (unincluded, ProcessWorker.SyncResult.SUCCEED);
                }
            }
        }

        private bool DoChangeGraceWait(Grace grace)
        {
            grace.WaitGraceTime ();
            return false;
        }

        private bool DataBaseContainsLocalObject(Database.Database db, string path, bool isFolder)
        {
            if (isFolder) return db.ContainsLocalPath (path);
            else return db.ContainsLocalFile (path);
        }

        ~LocalWatcherProcessor ()
        {
            Dispose (false);
        }

        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        private Object disposeLock = new object ();
        private bool disposed = false;
        protected virtual void Dispose (bool disposing)
        {
            lock (disposeLock) {
                if (!this.disposed) {
                    if (disposing) {
                    }
                    this.disposed = true;
                }
            }
        }
    }
}
