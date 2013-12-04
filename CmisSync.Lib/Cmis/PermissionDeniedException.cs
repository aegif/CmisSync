using System;
using System.Runtime.Serialization;

namespace CmisSync.Lib.Cmis
{
    /// <summary>
    /// Exception launched when the CMIS repository denies an action.
    /// </summary>
    [Serializable]
    public class PermissionDeniedException : BaseException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public PermissionDeniedException() { }
        
        /// <summary>
        /// Constructor.
        /// </summary>
        public PermissionDeniedException(string message) : base(message) { }
        
        /// <summary>
        /// Constructor.
        /// </summary>
        public PermissionDeniedException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Constructor.
        /// </summary>
        public PermissionDeniedException(Exception inner) : base(inner) { }

        /// <summary>
        /// Constructor.
        /// </summary>
        protected PermissionDeniedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
