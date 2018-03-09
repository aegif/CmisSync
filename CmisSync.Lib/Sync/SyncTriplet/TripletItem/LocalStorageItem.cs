using System;
using System.IO;
using CmisSync.Lib.Utilities.FileUtilities;

namespace CmisSync.Lib.Sync.SyncTriplet.TripletItem
{
    public class LocalStorageItem : BaseStorageItem
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="T:CmisSync.Lib.Sync.SyncTriplet.TripletItem.LocalStorageItem"/> class.
        /// If relative path is null, the local item does not exist.
        /// If relative path is not null but Exist = false, the local item was deleted.
        /// </summary>
        /// <param name="rootPath">Root path.</param>
        /// <param name="relativePath">Relative path.</param>
        public LocalStorageItem (String rootPath, String relativePath)
        {
            RootPath = rootPath;
            RelativePath = relativePath;
        }

        public LocalStorageItem(LocalStorageItem storage) 
        {
            this.RootPath = storage.RootPath;
            this.RelativePath = storage.RelativePath;
        }

        /// <summary>
        /// Gets or sets the check sum.
        /// </summary>
        /// <value>The check sum.</value>
        public String CheckSum {
            get {
                return CheckSumUtil.Checksum (Path.Combine(RootPath, RelativePath));
            }
        }

        public String FullPath {
            get {
                return Path.Combine (RootPath, RelativePath);
            }
        }
    }
}
