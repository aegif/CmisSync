using System;
using System.Runtime.Serialization;

namespace CmisSync.Lib.Cmis
{
    /// <summary>
    /// Exception launched when the CMIS server can not be found.
    /// </summary>
    [Serializable]
    public class ServerNotFoundException : BaseException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public ServerNotFoundException() { }


        /// <summary>
        /// Constructor.
        /// </summary>
        public ServerNotFoundException(string message) : base(message) { }


        /// <summary>
        /// Constructor.
        /// </summary>
        public ServerNotFoundException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ServerNotFoundException(Exception inner) : base(inner) { }

        /// <summary>
        /// Constructor.
        /// </summary>
        protected ServerNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

}
