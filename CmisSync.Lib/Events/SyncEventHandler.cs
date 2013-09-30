using System;

namespace CmisSync.Lib
{
    public interface SyncEventHandler
    {
        bool handle(SyncEvent e);
        uint getPriority();
    }
}

