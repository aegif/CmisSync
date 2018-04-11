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
        public SyncTriplet(bool isFolder) {
            IsFolder = isFolder;
            Delayed = true;
            LocalStorage = null;
            RemoteStorage = null;
            DBStorage = null;
        }

        /// <summary>
        /// The logger.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SyncTriplet));


        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="T:CmisSync.Lib.Sync.SyncTriplet.SyncTriplet"/> is delayed.
        /// 
        /// Delayed is a propert to solve folder deletion problem in concurrent environment:
        ///    if /a/b/c.txt and /a/b/ are processed concurrently, 
        ///    it can not be guaranteed /a/b/ is processed after /a/b/c.txt
        /// This is important especially when remote folder is removed while one file in local folder is modified.
        /// 
        /// Btw, this property is only useful when sb. want to delete a folder. When the processor find this property is true,
        /// it will not process this triplet but move it to delayed Queue. When one processor task has completed, it will start
        /// delayed queue processing. Beaware that delayed queue should be ordered in lexicographical order.
        /// </summary>
        /// <value><c>true</c> if delayed; otherwise, <c>false</c>.</value>
        public bool Delayed { get; set;  }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:CmisSync.Lib.Sync.SyncTriplet.SyncTriplet"/>.
        /// </summary>
        /// <returns>A <see cref="T:System.String"/> that represents the current <see cref="T:CmisSync.Lib.Sync.SyncTriplet.SyncTriplet"/>.</returns>
        public override string ToString () { return Name; }

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
        public bool LocalExist { get {
                return !(null == LocalStorage) && (IsFolder ? Directory.Exists (Utils.PathCombine (LocalStorage.RootPath, LocalStorage.RelativePath)) : 
                                                   File.Exists (Utils.PathCombine (LocalStorage.RootPath, LocalStorage.RelativePath)));
            }}

        /// <summary>
        /// Gets a value indicating whether RemoteStorageItem exist.
        /// </summary>
        /// <value><c>true</c> if remote exist; otherwise, <c>false</c>.</value>
        public bool RemoteExist { get {
                return !(null == RemoteStorage);
            }}

        /// <summary>
        /// Gets a value indicating whether DBStorageItem exist.
        /// </summary>
        /// <value><c>true</c> if DBE xist; otherwise, <c>false</c>.</value>
        public bool DBExist { get {
                return (null != DBStorage) && (null != DBStorage.DBLocalPath) && (null != DBStorage.DBRemotePath);
            }}

        /// <summary>
        /// Gets a value indicating whether this <see cref="T:CmisSync.Lib.Sync.SyncTriplet.SyncTriplet"/> local eq db.
        /// Two cases:
        ///     LS ne , DB ne, therefore LS = DB
        ///     LS e, DB e, and LS relpath = DB relpath and LS chksum == DB chksum
        /// </summary>
        /// <value><c>true</c> if local eq db; otherwise, <c>false</c>.</value>
        public bool LocalEqDB {
            get {
                return ( !LocalExist && !DBExist ) || 
                    ( LocalExist && DBExist && LocalStorage.RelativePath == DBStorage.DBLocalPath  && 
                        ( IsFolder ? true : LocalStorage.CheckSum == DBStorage.Checksum ));
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="T:CmisSync.Lib.Sync.SyncTriplet.SyncTriplet"/> remote eq db.
        /// Two cases:
        ///     RS ne, DB ne, therefore RS = DB
        ///     RS e, DB e, RS relpath = DB relpath, ( is not folder : RS lastmodi = DB lastmodi )
        /// </summary>
        /// <value><c>true</c> if remote eq db; otherwise, <c>false</c>.</value>
        public bool RemoteEqDB {
            get {
                return ( !DBExist && !RemoteExist) ||
                    (DBExist && RemoteExist && (RemoteStorage.RelativePath == DBStorage.DBRemotePath) && (
                        IsFolder ? true : (RemoteStorage.LastModified.ToString() == DBStorage.ServerSideModificationDate.ToString())
                    )
                    );
            }
        }

        /// <summary>
        /// Gets the information of triplet. For debug
        /// </summary>
        /// <value>The information.</value>
        public string Information {
            get {
                return String.Format("  %% LocalExist? {0}\n" +
                                     "     DBExist? {1}\n" +
                                     "     RemoteExist? {2}\n\n" +
                                     "     LocalRelative? {3}\n" +
                                     "     DB -local Relative? {4}\n" +
                                     "     DB -remote Relative? {5}\n" +
                                     "     RemoteRelative? {6}\n\n" +
                                     "     LocalChkSum? {7}\n" +
                                     "     DBChkSum? {8}\n\n" +
                                     "     RemoteLastModify? {9}\n" +
                                     "     DBLastModify? {10}\n",
                                     LocalExist.ToString (),
                                     DBExist.ToString (),
                                     RemoteExist.ToString (),
                                     
                                     !LocalExist ? null : LocalStorage.RelativePath,
                                     !DBExist ? null : DBStorage.DBLocalPath,
                                     !DBExist ? null : DBStorage.DBRemotePath,
                                     !RemoteExist ? null : RemoteStorage.RelativePath,
                                     
                                     (IsFolder || !LocalExist ? null : LocalStorage.CheckSum),
                                     (IsFolder || !DBExist ? null : DBStorage.Checksum),
                                     
                                     !RemoteExist ? null : RemoteStorage.LastModified,
                                     !DBExist ? null : DBStorage.ServerSideModificationDate.ToString ()
                                     );
                                    }
        }

   }
}
