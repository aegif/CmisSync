using System;
using System.Runtime.Serialization;

namespace CmisSync.Lib.Cmis
{
    /// <summary>
    /// Exception launched when the CMIS server errors.
    /// </summary>
    [Serializable]
    public class CmisBaseException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisBaseException() { }


        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisBaseException(string message) : base(message) { }


        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisBaseException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisBaseException(Exception inner) : base(inner.Message, inner) { }

        /// <summary>
        /// Constructor.
        /// </summary>
        protected CmisBaseException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

}
