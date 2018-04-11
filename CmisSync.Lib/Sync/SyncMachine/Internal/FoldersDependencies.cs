using log4net;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Cmis;


namespace CmisSync.Lib.Sync.SyncMachine.Internal
{
    public class FoldersDependencies
    {

        // folderName -- <dep1, dep2, ... depN>
        private Dictionary<string, HashSet<string>> foldersDeps = null;
        private Dictionary<string, bool> conflictOrFailed = null;

        // depName -- <folder1, folder2, ..., folderN>
        // reverse lookup table
        private Dictionary<string, HashSet<string>> _LUT = null;

        private object locker = new object ();

        public FoldersDependencies ()
        {
            foldersDeps = new Dictionary<string, HashSet<string>> ();
            conflictOrFailed = new Dictionary<string, bool> ();
            _LUT = new Dictionary<string, HashSet<string>> ();
        }

        public bool IsClear(string folder) {
            lock(locker) {
                if (!foldersDeps.ContainsKey (folder)) return true;
                return foldersDeps [folder].Count == 0 && conflictOrFailed [folder] == false;
            }
        }

        public HashSet<string> GetFolderDependences(string folder) {
            lock(locker) {
                if (!foldersDeps.ContainsKey (folder)) return null;
                return foldersDeps [folder];
            }
        }

        public int GetFolderDependenceCount(string folder) {
            lock(locker) {
                if (!foldersDeps.ContainsKey (folder)) return 0;
                else return foldersDeps [folder].Count;
            }
        }

        /// <summary>
        /// Adds the folder dependence.
        /// </summary>
        /// <param name="folder">Folder.</param>
        /// <param name="depName">Dep name.</param>
        public void AddFolderDependence (string folder, string depName)
        {
            // root folder of both remote and local do not require fdps
            if (folder.Equals (Path.DirectorySeparatorChar.ToString ()) ||
                folder.Equals(CmisUtils.CMIS_FILE_SEPARATOR.ToString())) return;

            lock (locker) {
                // add dep to folder's dependencies
                if (foldersDeps.ContainsKey (folder)) foldersDeps [folder].Add (depName);
                else foldersDeps [folder] = new HashSet<string> { depName };
                conflictOrFailed [folder] = false;

                // add folder to LUT table given deps
                if (_LUT.ContainsKey (depName)) _LUT [depName].Add (folder);
                else _LUT [depName] = new HashSet<string> { folder };
            }
        }

        /// <summary>
        /// Adds the folder dependence.
        /// </summary>
        /// <param name="folder">Folder.</param>
        /// <param name="triplet">Triplet.</param>
        public void AddFolderDependence(string folder, SyncTriplet.SyncTriplet triplet) {
            AddFolderDependence (folder, triplet.Name);
        }


        /// <summary>
        /// Removes the folder dependence, give depName only
        /// </summary>
        /// <param name="depName">Dep name.</param>
        public void RemoveFolderDependence(string depName, bool succeed) {
            lock(locker) {
                if (!_LUT.ContainsKey (depName)) return;
                foreach (string folder in _LUT [depName]) {
                    if (!foldersDeps.ContainsKey (folder)) continue;
                    if (!foldersDeps [folder].Remove (depName)) {
                        Console.WriteLine ("  Remove folder {0}'s dependency: {1} failed", folder, depName);
                    }
                    if (!succeed) this.conflictOrFailed [folder] = true;
                }
            } 
        }

        /// <summary>
        /// Removes the folder dependence, given folder Name and dependence name
        /// </summary>
        /// <param name="folder">Folder.</param>
        /// <param name="depName">Dep name.</param>
        public void RemoveFolderDependence(string folder, string depName) {
            lock(locker) {
                if (!foldersDeps.ContainsKey (folder)) return;
                if (!foldersDeps [folder].Remove (depName)) {
                    Console.WriteLine ("  Remove folder {0}'s dependency: {1} failed", folder, depName);
                }
            }
        }

        public void OutputFolderDependences(string folder) {
            string output = "";
            foreach (string s in foldersDeps [folder]) output += s + ", ";
            Console.WriteLine (output);
        }

        public void OutputFoldersDependences() {
            List<string> keys = foldersDeps.Keys.ToList<string> ();
            keys.Sort (new ReverseLexicoGraphicalComparer<string> ());
            foreach (string k in keys) {
                Console.Write (" ## Folder {0}'s deps: ", k);
                OutputFolderDependences (k);
            }
        }
    }
}
