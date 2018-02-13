using System;

namespace CmisSync.Lib.Sync.SynchronizeTriplet.TripletItem
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

        /// <summary>
        /// Gets or sets a value indicating whether this file exists.
        /// </summary>
        /// <value><c>true</c> if exist; otherwise, <c>false</c>.</value>
        public Boolean Exist { get; set; }

    }
}
