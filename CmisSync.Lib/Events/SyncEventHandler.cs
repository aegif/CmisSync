using System;

using log4net;

namespace CmisSync.Lib.Events
{
    public interface ISyncEventHandler
    {
        bool Handle(ISyncEvent e);
        int GetPriority();
    }

    abstract public class EventFilter : ISyncEventHandler {
        private static readonly int EVENTFILTERPRIORITY = 1000;
        public abstract bool Handle(ISyncEvent e);
        public int GetPriority() {
            return EVENTFILTERPRIORITY;
        }
    }

    public class IgnoreFolderFilter : EventFilter {

        public override bool Handle(ISyncEvent e) {
            return false;
        }
    };

    public class DebugLoggingHandler : ISyncEventHandler
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(DebugLoggingHandler));
        private static readonly int DEBUGGINGLOGGERPRIORITY = 10000;

        public bool Handle(ISyncEvent e)
        {
            Logger.Debug("Incomming Event: " + e.ToString());
            return false;
        }

        public int GetPriority()
        {
            return DEBUGGINGLOGGERPRIORITY;
        }
    }
}

