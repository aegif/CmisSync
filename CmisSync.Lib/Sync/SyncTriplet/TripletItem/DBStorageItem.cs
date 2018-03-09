using System;
using CmisSync.Lib.Database;
using log4net;

namespace CmisSync.Lib.Sync.SyncTriplet.TripletItem
{
    /// <summary>
    /// DBI tem.
    /// </summary>
    public class DBStorageItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:CmisSync.Lib.Sync.SyncTriplet.TripletItem.DBStorageItem"/> class.
        /// If remote relative path can not be found in database, we set local relative path to null too, and vice versa.
        /// </summary>
        /// <param name="database">Database reference.</param>
        /// <param name="relativePath">Relative path.</param>
        /// <param name="isFolder">If <c>true</c> the record is a folder else a file.</param>
        /// <param name="r2l">If set to <c>true</c> lookup local path by remote path, vice versa.</param>
        public DBStorageItem(
            Database.Database database,
            String relativePath,
            Boolean isFolder,
            Boolean r2l 
        )
        {
            Database = database;
            IsFolder = isFolder;
            if (r2l) 
            {
                DBLocalPath = database.NullableRemoteToLocal (relativePath, isFolder);
                DBRemotePath = DBLocalPath == null ? null : relativePath;
            }
            else 
            {
                DBRemotePath = database.NullableLocalToRemote (relativePath, isFolder);
                DBLocalPath = DBRemotePath == null ? null : relativePath;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:CmisSync.Lib.Sync.SyncTriplet.TripletItem.DBStorageItem"/> class
        /// directly from DB record
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="localRelativePath">Local relative path.</param>
        /// <param name="remoteRelativePath">Remote relative path.</param>
        /// <param name="isFolder">If set to <c>true</c> is folder.</param>
        public DBStorageItem(
            Database.Database database,
            String localRelativePath,
            String remoteRelativePath,
            Boolean isFolder
        ) {
            Database = database;
            IsFolder = isFolder;
            DBLocalPath = localRelativePath;
            DBRemotePath = remoteRelativePath;
        }

        /// <summary>
        /// Gets or sets the database.
        /// </summary>
        /// <value>The database.</value>
        public Database.Database Database { get; set; }

        /// <summary>
        /// Gets or sets the remote path.
        /// </summary>
        /// <value>The remote path.</value>
        public string DBRemotePath { get; set; }

        /// <summary>
        /// Gets or sets the local path.
        /// </summary>
        /// <value>The local path.</value>
        public string DBLocalPath { get; set; }

        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this
        /// <see cref="T:CmisSync.Lib.Sync.SyncTriplet.TripletItem.DBStorageItem"/> is folder;
        /// The reason to duplicate IsFolder property in DBStorage
        /// is that in Database folders and files are stored in seperated tables.
        /// </summary>
        /// <value><c>true</c> if is folder; otherwise, <c>false</c>.</value>
        public bool IsFolder { get; set; }

        /// <summary>
        /// Gets or sets the server side modification date.
        /// </summary>
        /// <value>The server side modification date.</value>
        public DateTime? ServerSideModificationDate { get {
                return Database.GetServerSideModificationDate (DBRemotePath, IsFolder);
            } }

        /// <summary>
        /// Lazy getting the checksum.
        /// </summary>
        /// <value>The cheksum.</value>
        public String Checksum { get {
                return Database.GetChecksum (this.DBLocalPath);
            } }

    }
}
