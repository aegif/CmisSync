using System;
using System.Runtime.Serialization;

namespace CmisSync.Lib.Cmis
{
    /// <summary>
    /// Exception launched when the CMIS server errors.
    /// </summary>
    [Serializable]
    public class BaseException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public BaseException() { }


        /// <summary>
        /// Constructor.
        /// </summary>
        public BaseException(string message) : base(message) { }


        /// <summary>
        /// Constructor.
        /// </summary>
        public BaseException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Constructor.
        /// </summary>
        public BaseException(Exception inner) : base(inner.Message, inner) { }

        /// <summary>
        /// Constructor.
        /// </summary>
        protected BaseException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

}
