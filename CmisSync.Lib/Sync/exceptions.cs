using System;

namespace DotCMIS.Exceptions
{
    [Serializable]
    public class CmisMissingSyncFolderException : CmisBaseException
    {
        public CmisMissingSyncFolderException() : base() { }
        public CmisMissingSyncFolderException(string message) : base(message) { }
        public CmisMissingSyncFolderException(string message, Exception inner) : base(message, inner) { }
        protected CmisMissingSyncFolderException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisMissingSyncFolderException(string message, long? code)
            : base(message) { }
        public CmisMissingSyncFolderException(string message, string errorContent)
            : base(message) { }
        public CmisMissingSyncFolderException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

}
