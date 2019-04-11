using System;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Utilities.FileUtilities;

using DotCMIS.Client;

namespace CmisSync.Lib.Sync.SyncTriplet.TripletItem
{
    /*
     * Cmis server itself is stateless.
     * Therefore session actually is kept completely client side.
     * It should be safe to keep the reference of the remote ICmisObject
     * in the RemoteStorageItem
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
        /// <param name="cmisObject">Cmis object.</param>
        public RemoteStorageItem (String rootPath, String relativePath, ICmisObject cmisObject)
        {
            RootPath = rootPath;
            RelativePath = relativePath;
            this.CmisObject = cmisObject;
        }

        public RemoteStorageItem(RemoteStorageItem storage) 
        {
            this.RootPath = storage.RootPath;
            this.RelativePath = storage.RelativePath;
            this.CmisObject = storage.CmisObject;
        }

        public ICmisObject CmisObject { get; }

        /// <summary>
        /// Gets or sets the last modified.
        /// </summary>
        /// <value>The last modified.</value>
        public DateTime? LastModified { get { return CmisObject?.LastModificationDate; }}

        public string FullPath {
            get {
                return CmisFileUtil.PathCombine (RootPath, RelativePath);
            }
        }

    }
}
