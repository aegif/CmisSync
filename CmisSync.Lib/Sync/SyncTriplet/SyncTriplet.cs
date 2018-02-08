using System;
using System.IO;
using log4net;
using CmisSync.Lib.Sync.SyncTriplet.TripletItem;

namespace CmisSync.Lib.Sync.SyncTriplet
{
    /// <summary>
    /// Sync triplet.
    /// </summary>
    public class SyncTriplet
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:CmisSync.Lib.Sync.SyncTriplet.SyncTriplet"/> class.
        /// </summary>
        public SyncTriplet() {
            // TODO: debug
            Logger.Debug("A new sync triplet is initialized.");

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

        /// <summary>
        /// Gets a value indicating whether LocalStorageItem exist.
        /// </summary>
        /// <value><c>true</c> if local exist; otherwise, <c>false</c>.</value>
        public Boolean LocalExist { get {
                return (null == LocalStorage) ? false : LocalStorage.Exist;
            }}

        /// <summary>
        /// Gets a value indicating whether RemoteStorageItem exist.
        /// </summary>
        /// <value><c>true</c> if remote exist; otherwise, <c>false</c>.</value>
        public Boolean RemoteExist { get {
                return (null == RemoteStorage) ? false : RemoteStorage.Exist;
            }}

        /// <summary>
        /// Gets a value indicating whether DBStorageItem exist.
        /// </summary>
        /// <value><c>true</c> if DBE xist; otherwise, <c>false</c>.</value>
        public Boolean DBExist { get {
                return (null == DBStorage) ? false : DBStorage.Exist;
            }}
   }
}
