using System;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Utilities.FileUtilities;

namespace CmisSync.Lib.Sync.SyncTriplet.TripletItem
{
    /*
     * TODO:
     *   if it is necessary to keep an ICmisObject reference
     *   in the RS item
     */
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
            LastModified = (lastModified.HasValue) ? ((DateTime)lastModified).ToUniversalTime () : (DateTime?)null ;
        }

        public RemoteStorageItem(RemoteStorageItem storage) 
        {
            this.RootPath = storage.RootPath;
            this.RelativePath = storage.RelativePath;
            // DateTime is a data type, = means copy
            this.LastModified = storage.LastModified;
        }

        /// <summary>
        /// Gets or sets the last modified.
        /// </summary>
        /// <value>The last modified.</value>
        public DateTime? LastModified { get; set; }


        public string FullPath {
            get {
                return CmisFileUtil.PathCombine (RootPath, RelativePath);
            }
        }
    }
}
