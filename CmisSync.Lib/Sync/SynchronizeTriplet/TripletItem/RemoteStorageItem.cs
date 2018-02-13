using System;
using CmisSync.Lib.Cmis;

namespace CmisSync.Lib.Sync.SynchronizeTriplet.TripletItem
{

    public class RemoteStorageItem : BaseStorageItem
    {

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="T:CmisSync.Lib.Sync.SyncTriplet.TripletItem.RemoteStorageItem"/> class.
        /// If the relativePath is null, the item does not exist.
        /// </summary>
        /// <param name="rootPath">Root path.</param>
        /// <param name="relativePath">Relative path.</param>
        /// <param name="lastModified">Last modified.</param>
        public RemoteStorageItem (String rootPath, String relativePath, DateTime? lastModified)
        {
            RootPath = rootPath;
            RelativePath = relativePath;
            Exist = true;
            if (null == relativePath) {
                FullPath = null;
                Exist = false;
            } else {
                FullPath = CmisUtils.PathCombine (RootPath, RelativePath);
            }

            LastModified = (lastModified.HasValue) ? ((DateTime)lastModified).ToUniversalTime () : (DateTime?)null ;
        }

        /// <summary>
        /// Gets or sets the last modified.
        /// </summary>
        /// <value>The last modified.</value>
        public DateTime? LastModified { get; set; }

    }
}
