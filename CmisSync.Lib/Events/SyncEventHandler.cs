using System;

using log4net;

namespace CmisSync.Lib
{
    public interface ISyncEventHandler
    {
        bool handle(ISyncEvent e);
        int getPriority();
    }

    abstract public class EventFilter : ISyncEventHandler {
        private static readonly int EVENTFILTERPRIORITY = 1000;
        public abstract bool handle(ISyncEvent e);
        public int getPriority() {
            return EVENTFILTERPRIORITY;
        }
    }

    public class IgnoreFolderFilter : EventFilter {

        public override bool handle(ISyncEvent e) {
            return false;
        }
    };

    public class DebugLoggingHandler : ISyncEventHandler
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(DebugLoggingHandler));
        private static readonly int DEBUGGINGLOGGERPRIORITY = 10000;

        public bool handle(ISyncEvent e)
        {
            Logger.Debug("Incomming Event: " + e.ToString());
            return false;
        }

        public int getPriority()
        {
            return DEBUGGINGLOGGERPRIORITY;
        }
    }
}

