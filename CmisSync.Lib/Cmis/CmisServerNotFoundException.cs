using System;
using System.Runtime.Serialization;

namespace CmisSync.Lib.Cmis
{
    [Serializable]
    public class CmisServerNotFoundException : Exception
    {
        public CmisServerNotFoundException() { }
        public CmisServerNotFoundException(string message) : base(message) { }
        public CmisServerNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected CmisServerNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

}
