using System;

namespace CmisSync.Lib
{
    public interface ISyncEventHandler
    {
        bool handle(ISyncEvent e);
        int getPriority();
    }
}

