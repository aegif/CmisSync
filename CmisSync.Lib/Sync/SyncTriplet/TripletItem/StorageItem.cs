using System;
using System.IO;
using CmisSync.Lib.Cmis;

namespace CmisSync.Lib.Sync.SyncTriplet.TripletItem
{
    /// <summary>
    /// Storage item.
    /// </summary>
    public class StorageItem
    {

        /// <summary>
        /// Gets or sets the root path.
        /// </summary>
        /// <value>The root path.</value>
        public string RootPath { get; set; }

        /// <summary>
        /// Gets or sets the relative path to the root path.
        /// </summary>
        /// <value>The relative path.</value>
        public string RelativePath { get; set; }


        /// <summary>
        /// Gets or sets the full path.
        /// </summary>
        /// <value>The full path.</value>
        public string FullPath { get; set; }

        /// <summary>
        /// Gets or sets the leaf name.
        /// </summary>
        /// <value>The leaf name.</value>
        public string LeafName { get; set; }

    }

    public class LocalStorageItem : StorageItem
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="T:CmisSync.Lib.Sync.SyncTriplet.TripletItem.LocalStorageItem"/> class.
        /// </summary>
        /// <param name="rootPath">Root path.</param>
        /// <param name="relativePath">Relative path.</param>
        public LocalStorageItem(String rootPath, String relativePath)
        {
            RootPath = rootPath;
            RelativePath = relativePath;
            FullPath = rootPath + relativePath;
            Exist = File.Exists (FullPath);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:CmisSync.Lib.Sync.SyncTriplet.TripletItem.LocalStorageItem"/> class.
        /// </summary>
        /// <param name="rootPath">Root path.</param>
        /// <param name="relativePath">Relative path.</param>
        /// <param name="getChkSum">If set to <c>true</c> get checkk sum of the file.</param>
        public LocalStorageItem (String rootPath, String relativePath, Boolean getChkSum)
        {
            RootPath = rootPath;
            RelativePath = relativePath;
            FullPath = CmisUtils.PathCombine (rootPath, relativePath);
            Exist = File.Exists (FullPath);
            if (getChkSum) 
            {
                // TODO get Check Sum
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this file exists.
        /// </summary>
        /// <value><c>true</c> if exist; otherwise, <c>false</c>.</value>
        public Boolean Exist { get; set; }

        /// <summary>
        /// Gets or sets the check sum.
        /// </summary>
        /// <value>The check sum.</value>
        public String CheckSum { get; set; }
    }


    public class RemoteStorageItem : StorageItem
    {

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="T:CmisSync.Lib.Sync.SyncTriplet.TripletItem.RemoteStorageItem"/> class.
        /// </summary>
        /// <param name="rootPath">Root path.</param>
        /// <param name="relativePath">Relative path.</param>
        /// <param name="lastModified">Last modified.</param>
        public RemoteStorageItem (String rootPath, String relativePath, DateTime? lastModified) 
        {
            RootPath = rootPath;
            RelativePath = relativePath;
            FullPath = CmisUtils.PathCombine (rootPath, relativePath);
            LastModified = lastModified ?? ((DateTime)lastModified).ToUniversalTime ();
        }

        /// <summary>
        /// Gets or sets the last modified.
        /// </summary>
        /// <value>The last modified.</value>
        public DateTime LastModified { get; set; }

    }
}
