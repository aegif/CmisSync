using System;
using System.Runtime.Serialization;

namespace CmisSync.Lib.Cmis
{
    /// <summary>
    /// Exception launched when the CMIS server can not be found.
    /// </summary>
    [Serializable]
    public class CmisServerNotFoundException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisServerNotFoundException() { }


        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisServerNotFoundException(string message) : base(message) { }


        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisServerNotFoundException(string message, Exception inner) : base(message, inner) { }


        /// <summary>
        /// Constructor.
        /// </summary>
        protected CmisServerNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

}
