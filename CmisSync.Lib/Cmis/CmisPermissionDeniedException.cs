using System;
using System.Runtime.Serialization;

namespace CmisSync.Lib.Cmis
{
    /// <summary>
    /// Exception launched when the CMIS repository denies an action.
    /// </summary>
    [Serializable]
    public class CmisPermissionDeniedException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisPermissionDeniedException() { }
        
        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisPermissionDeniedException(string message) : base(message) { }
        
        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisPermissionDeniedException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Constructor.
        /// </summary>
        protected CmisPermissionDeniedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
