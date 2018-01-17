using System;
using CmisSync.Lib.Database;

namespace CmisSync.Lib.Sync.SyncTriplet.TripletItem
{
    /// <summary>
    /// DBI tem.
    /// </summary>
    public class DBStorageItem
    {
        public DBStorageItem(
            Database.Database database,
            String remoteRelativePath,
            String localRelativePath
        )
        {
            Database = database;
            RemotePath = remoteRelativePath;
            LocalPath = localRelativePath;
        }

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
        public string RemotePath { get; set; }

        /// <summary>
        /// Gets or sets the local path.
        /// </summary>
        /// <value>The local path.</value>
        public string LocalPath { get; set; }

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
        /// Gets or sets the checksum of local file.
        /// </summary>
        /// <value>The checksum.</value>
        public String Checksum { get; set; }

        /// <summary>
        /// Fetchs data from database.
        /// </summary>
        public void FetchDataFromDatabase()
        {
            
        }
    }
}
