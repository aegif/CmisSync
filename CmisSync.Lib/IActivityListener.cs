using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib
{
    /// <summary>
    /// Listen to activity/inactivity.
    /// Typically used by a status spinner:
    /// - Start spinning when activity starts
    /// - Stop spinning when activity stops
    /// </summary>
    public interface IActivityListener
    {
        /// <summary>
        /// Call this method to indicate that activity has started.
        /// </summary>
        void ActivityStarted();


        /// <summary>
        /// Call this method to indicate that activity has stopped.
        /// </summary>
        void ActivityStopped();
    }
}
