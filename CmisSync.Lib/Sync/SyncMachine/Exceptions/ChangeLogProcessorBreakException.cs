using System;

namespace CmisSync.Lib.Sync.SyncMachine.Exceptions
{
    [Serializable]
    public class ChangeLogProcessorBreakException : Exception
    {
        public ChangeLogProcessorBreakException () 
        { }

        public ChangeLogProcessorBreakException (string msg) : base (msg) 
        { }

        public ChangeLogProcessorBreakException (string msg, Exception innerException) : base (msg, innerException) 
        { }
    }

}
