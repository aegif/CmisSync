using System;

namespace CmisSync.Lib.Sync.SynchronizeMachine
{
    public class SyncState
    {
        public SyncState ()
        {
        }
    }

    /*
     *   Local    DB     Remote |  States
     * ------------------------------------
     *   Ext      Ext    Ext    |  Synced / OutofSync
     *   Ext      Ext    None   |  OutofSync
     *   Ext      None   Ext    |  Not synced
     *   Ext      None   None   |  Local New
     *   None     Ext    Ext    |  Not synced
     *   None     Ext    None   |  Both Removed
     *   None     None   Ext    |  Remote New
     *   None     None   None   |  WTF
     */
}
