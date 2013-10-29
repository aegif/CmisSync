using System;

using log4net;

namespace CmisSync.Lib.Events
{
    public interface ISyncEventHandler
    {
        bool Handle(ISyncEvent e);
        int Priority {get;}
    }
}

