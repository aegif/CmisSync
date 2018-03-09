using System;

namespace CmisSync.Lib.Sync.SyncTriplet.TripletItem
{
    /// <summary>
    /// Storage item.
    /// </summary>
    public class BaseStorageItem
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

    }
}
