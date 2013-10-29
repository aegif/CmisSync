using System;

using log4net;

namespace CmisSync.Lib.Events
{
    public interface ISyncEventHandler
    {
        bool Handle(ISyncEvent e);
        int Priority {get;}
    }

    public class DebugLoggingHandler : ISyncEventHandler
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(DebugLoggingHandler));
        private static readonly int DEBUGGINGLOGGERPRIORITY = 10000;

        public bool Handle(ISyncEvent e)
        {
            Logger.Debug("Incomming Event: " + e.ToString());
            return false;
        }

        public int Priority
        {
            get {return DEBUGGINGLOGGERPRIORITY;}
        }
    }
}

