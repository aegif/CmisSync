using System;

using log4net;

namespace CmisSync.Lib.Events
{
    public class DebugLoggingHandler : SyncEventHandler
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(DebugLoggingHandler));
        private static readonly int DEBUGGINGLOGGERPRIORITY = 10000;

        public override bool Handle(ISyncEvent e)
        {
            Logger.Debug("Incomming Event: " + e.ToString());
            return false;
        }

        public override int Priority
        {
            get {return DEBUGGINGLOGGERPRIORITY;}
        }
    }
}

