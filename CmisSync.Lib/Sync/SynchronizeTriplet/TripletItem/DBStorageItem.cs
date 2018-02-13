using System;
using CmisSync.Lib.Database;
using log4net;

namespace CmisSync.Lib.Sync.SynchronizeTriplet.TripletItem
{
    /// <summary>
    /// DBI tem.
    /// </summary>
    public class DBStorageItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:CmisSync.Lib.Sync.SyncTriplet.TripletItem.DBStorageItem"/> class.
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
            Exist = true;
            if (r2l)
            {
                DBRemotePath = relativePath;
                DBLocalPath = database.NullableRemoteToLocal (DBRemotePath, isFolder);
                if (null == DBLocalPath) {
                    Exist = false;
                }
            } 
            else 
            {
                DBLocalPath = relativePath;
                DBRemotePath = database.NullableLocalToRemote (DBLocalPath, isFolder);
                if (null == DBRemotePath) {
                    Exist = false;
                }
            }
        }

        private static ILog logger = LogManager.GetLogger (typeof (DBStorageItem));

        /// <summary>
        /// Gets or sets the database.
        /// </summary>
        /// <value>The database.</value>
        public Database.Database Database { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether record exists in database.
        /// </summary>
        /// <value><c>true</c> if exist; otherwise, <c>false</c>.</value>
        public Boolean Exist { get; set; }

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
        /// Gets or sets the server side modification date.
        /// </summary>
        /// <value>The server side modification date.</value>
        public DateTime ServerSideModificationDate { get; set; }

        /// <summary>
        /// Gets the checksum.
        /// </summary>
        /// <value>The checksum.</value>
        public String Checksum { get; }

        /// <summary>
        /// Fetchs data from database.
        /// </summary>
        public void FetchDataFromDatabase()
        {
            
        }
    }
}
