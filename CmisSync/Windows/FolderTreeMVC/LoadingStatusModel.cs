using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.CmisTree
{
    /// <summary>
    /// Status of a datatype, which children could be loaded asynchronous
    /// </summary>
    public enum LoadingStatus
    {
        /// <summary>
        /// Status before loading
        /// </summary>
        START,
        /// <summary>
        /// Status while loading is in progress
        /// </summary>
        LOADING,
        /// <summary>
        /// Status for aborted loading progresses
        /// </summary>
        ABORTED,
        /// <summary>
        /// Failure status for failed requests while loading
        /// </summary>
        REQUEST_FAILURE,
        /// <summary>
        /// Status if loaded correctly
        /// </summary>
        DONE
    }
}
