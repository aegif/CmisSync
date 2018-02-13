using System;
using System.IO;
using CmisSync.Lib.Utilities.FileUtilities;

namespace CmisSync.Lib.Sync.SynchronizeTriplet.TripletItem
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
            if (null == relativePath) {
                Exist = false;
            } else {
                RelativePath = relativePath;
                FullPath = rootPath + relativePath;
                Exist = File.Exists (FullPath);
            }
        }

        /// <summary>
        /// Gets or sets the check sum.
        /// </summary>
        /// <value>The check sum.</value>
        public String CheckSum {
            get {
                return CheckSumUtil.Checksum (FullPath);
            }
        }
    }
}
