using System;
using System.IO;

using log4net;

using CmisSync.Lib.Cmis;
using DotCMIS.Client;
using CmisSync.Lib.Database;

namespace CmisSync.Lib.Events
{
    /// <summary></summary>
    public class FSDeletionHandler : SyncEventHandler
    {
        private Database.Database database;

        private ISession session;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(FSDeletionHandler));

        private static readonly int FSDELETIONPRIORITY = 100;

        /// <summary></summary>
        /// <param name="database"></param>
        /// <param name="session"></param>
        public FSDeletionHandler(Database.Database database, ISession session)
        {
            if (database == null)
            {
                throw new ArgumentNullException("Argument null in FSDeletionHandler Constructor", "database");
            }
            if (session == null)
            {
                throw new ArgumentNullException("Argument null in FSDeletionHandler Constructor", "session");
            }
            this.database = database;
            this.session = session;
        }

        /// <summary></summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public override bool Handle(ISyncEvent e)
        {
            if(!(e is FSEvent))
            {
                return false;
            }
            FSEvent fsEvent = e as FSEvent;
            if (fsEvent.Type != WatcherChangeTypes.Deleted)
            {
                return false;
            }
            return true;
        }

        /// <summary></summary>
        public override int Priority
        {
            get {return FSDELETIONPRIORITY;}
        }
    }
}

