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
        START, LOADING, ABORTED, REQUEST_FAILURE, DONE
    }
}
