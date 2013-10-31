using System;
using System.IO;

using log4net;

namespace CmisSync.Lib.Events
{
    public class FSDeletionHandler : SyncEventHandler
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(FSDeletionHandler));
        private static readonly int FSDELETIONPRIORITY = 100;

        public override bool Handle(ISyncEvent e)
        {
            if(!(e is FSEvent)){
                return false;
            }
            FSEvent fsEvent = e as FSEvent;
            if (fsEvent.Type != WatcherChangeTypes.Deleted){
                return false;
            }
            return true;
        }

        public override int Priority
        {
            get {return FSDELETIONPRIORITY;}
        }
    }
}

