using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib
{
    /**
     * Listen to activity/inactivity.
     * Typically used by a status spinner:
     * - Start spinning when activity starts
     * - Stop spinning when activity stops
     */
    public interface IActivityListener
    {
        void ActivityStarted();
        void ActivityStopped();
    }
}
