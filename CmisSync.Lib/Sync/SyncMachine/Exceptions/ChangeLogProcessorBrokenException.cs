using System;

namespace CmisSync.Lib.Sync.SyncMachine.Exceptions
{
    [Serializable]
    public class ChangeLogProcessorBrokenException : Exception
    {
        public ChangeLogProcessorBrokenException () 
        { }

        public ChangeLogProcessorBrokenException (string msg) : base (msg) 
        { }

        public ChangeLogProcessorBrokenException (string msg, Exception innerException) : base (msg, innerException) 
        { }
    }

}
