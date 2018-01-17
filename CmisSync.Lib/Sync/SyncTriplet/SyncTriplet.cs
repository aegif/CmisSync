using System;
using System.IO;
using log4net;
using CmisSync.Lib.Sync.SyncTriplet.TripletItem;

namespace CmisSync.Lib.Sync.SyncTriplet
{
    // The examples below are for this item:
    //
    // Local: C:\Users\nico\CmisSync\A Project\adir\afile.txt
    // Remote: /sites/aproject/adir/a<file
    //
    // Notice how:
    // - Slashes and antislashes can differ
    // - File names can differ
    // - Remote and local have different sets of fobidden characters
    //
    // For that reason, never convert a local path to a remote path (or vice versa) without checking the database.

    /// <summary>
    /// Sync triplet.
    /// </summary>
    public class SyncTriplet
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:CmisSync.Lib.Sync.SyncTriplet.SyncTriplet"/> class.
        /// </summary>
        public SyncTriplet() {
            Logger.Debug("A new sync triplet is created");
            IsFolder = false;
            LocalStorage = null;
            RemoteStorage = null;
            DBStorage = null;
        }

        /// <summary>
        /// The logger.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger (typeof (SyncItem));

        /// <summary>
        /// Whether the item is a folder or a file.
        /// </summary>
        public bool IsFolder { get; set; }

        /// <summary>
        /// The local storage of sync triplet.
        /// </summary>
        public LocalStorageItem LocalStorage { get; set; }

        /// <summary>
        /// The remote storage of sync triplet.
        /// </summary>
        public RemoteStorageItem RemoteStorage { get; set; }

        /// <summary>
        /// The DBS torage of sync triplet.
        /// </summary>
        public DBStorageItem DBStorage { get; set; }
   }
}
