using log4net;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync.SyncMachine.ProcessWorker;


namespace CmisSync.Lib.Sync.SyncMachine.Internal
{
    public class ItemsDependencies
    {

        // itemName -- <dep1, dep2, ... depN>
        private Dictionary<string, HashSet<string>> itemsDeps = null;
        private HashSet<string> _failed = null;
        private HashSet<string> _conflicted = null;

        // depName -- <item1, item2, ..., itemN>
        // reverse lookup table
        private Dictionary<string, HashSet<string>> _LUT = null;

        private object locker = new object ();

        public ItemsDependencies ()
        {
            itemsDeps = new Dictionary<string, HashSet<string>> ();
            _failed = new HashSet<string> ();
            _conflicted = new HashSet<string> ();
            _LUT = new Dictionary<string, HashSet<string>> ();
        }

        public bool isAllResolved ()
        {
            lock (locker) {
                return itemsDeps.Count == 0;
            }
        }

        public bool IsResolved(string item) {
            lock(locker) {
                if (!itemsDeps.ContainsKey (item)) return true;
                return itemsDeps [item].Count == 0;
            }
        }

        public HashSet<string> GetItemDependences(string item) {
            lock(locker) {
                if (!itemsDeps.ContainsKey (item)) return new HashSet<string>();
                return itemsDeps [item];
            }
        }

        public int GetItemDependenceCount(string item) {
            lock(locker) {
                if (!itemsDeps.ContainsKey (item)) return 0;
                else return itemsDeps [item].Count;
            }
        }

        /// <summary>
        /// Adds the item dependence.
        /// </summary>
        /// <param name="item">item.</param>
        /// <param name="depName">Dep name.</param>
        public void AddItemDependence (string item, string depName)
        {
            // root item of both remote and local do not require fdps
            if (item.Equals (Path.DirectorySeparatorChar.ToString ()) ||
                item.Equals(CmisUtils.CMIS_FILE_SEPARATOR.ToString())) return;

            // root would also not be added as dependency
            if (depName.Equals (Path.DirectorySeparatorChar.ToString ()) ||
                depName.Equals (CmisUtils.CMIS_FILE_SEPARATOR.ToString ())) return;

            lock (locker) {
                // add dep to item's dependencies
                if (itemsDeps.ContainsKey (item)) itemsDeps [item].Add (depName);
                else itemsDeps [item] = new HashSet<string> { depName };

                // add item to LUT table given deps
                if (_LUT.ContainsKey (depName)) _LUT [depName].Add (item);
                else _LUT [depName] = new HashSet<string> { item };
            }
        }

        /// <summary>
        /// Adds the item dependence.
        /// </summary>
        /// <param name="item">item.</param>
        /// <param name="triplet">Triplet.</param>
        public void AddItemDependence(string item, SyncTriplet.SyncTriplet triplet) {
            AddItemDependence (item, triplet.Name);
        }


        /// <summary>
        /// Removes the item dependence, give depName only.
        /// If succeed flag is set to false, the depended item's sync
        /// has failed. One should set all it relating item to ConflictOrFailed.
        /// </summary>
        /// <param name="depName">Dependence name.</param>
        /// <param name="result">Result returned by sync worker</param>
        public void RemoveItemDependence(string depName, SyncResult result) {
            lock(locker) {
                // Confirm that only the item can delete itself from idps dictionary.
                // Else, eg:
                //   folder1: start -> has deps, processing, failed, (long time)
                //   folder1/f1: finished and delete folder1's dep, idps dictionary's count is 0 and full-sync-queue set to IsAddingComplete
                //   --> after folder1's thread end, it will push folder1 back to queue because its state is unresolved. error
                if (itemsDeps.ContainsKey (depName) && itemsDeps [depName].Count == 0) {
                    itemsDeps.Remove (depName);
                }
                if (!_LUT.ContainsKey (depName)) return;
                foreach (string item in _LUT [depName]) {
                    if (!itemsDeps.ContainsKey (item)) continue;
                    if (itemsDeps [item].Remove (depName)) {
                        /*
                        if (itemsDeps[item].Count == 0) {
                            itemsDeps.Remove (item);
                        }
                        */
                    } else {
                        Console.WriteLine (" I [ WorkerThread: {0} ] Remove item {1}'s dependency: {2} failed",
                            Thread.CurrentThread.ManagedThreadId, item, depName);
                    }

                    if (result != SyncResult.SUCCEED) {

                        // If the item's processing failed,
                        // all items depend on this item should
                        // be marked as either failed or conflict
                        if (result == SyncResult.FAILED) {
                            _failed.Add (item);
                        }
                        if (result == SyncResult.CONFLICT) {
                            _conflicted.Add (item);
                        }
                    }
                }
            } 
        }

        /// <summary>
        /// Check if there is any failure in the processed dependencies. 
        /// If yes, the processor should directly ignore it and remove it
        /// in the ItemDependencies dictionary and mark itself as failure.
        /// </summary>
        /// <returns><c>true</c>, if failed dependence exists, <c>false</c> otherwise.</returns>
        /// <param name="item">Item.</param>
        public bool HasFailedDependence(String item) { 
            lock (locker) {
                return _failed.Contains (item);
            }
        }

        /// <summary>
        /// Check if there is any conflicting in the processed dependencies.
        /// If (exists and the operation is deletion), one should do solve-conflicting
        /// process to this object.
        /// </summary>
        /// <returns><c>true</c>, if conflicted dependence was hased, <c>false</c> otherwise.</returns>
        /// <param name="item">Item.</param>
        public bool HasConflictedDependence(String item)
        {
            lock (locker) {
                return _conflicted.Contains (item);
            }
        }


        /// <summary>
        /// Removes the item dependence, given item Name and dependence name
        /// </summary>
        /// <param name="item">item.</param>
        /// <param name="depName">Dep name.</param>
        public void RemoveItemDependence(string item, string depName) {
            lock(locker) {
                if (!itemsDeps.ContainsKey (item)) return;
                if (!itemsDeps [item].Remove (depName)) {
                    Console.WriteLine ("  Remove item {0}'s dependency: {1} failed", item, depName);
                }
            }
        }

        private void OutputItemDependences(string item) {
            string output = "";
            foreach (string s in itemsDeps [item]) output += s + ", ";
            Console.WriteLine (output);
        }

        public void OutputItemsDependences() {
            List<string> keys = itemsDeps.Keys.ToList<string> ();
            keys.Sort (new ReverseLexicoGraphicalComparer<string> ());
            foreach (string k in keys) {
                Console.Write (" ## item {0}'s deps: ", k);
                OutputItemDependences (k);
            }
        }

        private void OutputItemDepLUT(string item)
        {
            string output = "";
            foreach (string s in _LUT [item]) output += s + ", ";
            Console.WriteLine (output);
        }

        public void OutputItemsDepLUT()
        {
            List<string> keys = _LUT.Keys.ToList<string> ();
            keys.Sort (new ReverseLexicoGraphicalComparer<string> ());
            foreach (string k in keys) {
                Console.Write (" ## items depend on {0}: ", k);
                OutputItemDepLUT(k);
            }
        }
    }
}
