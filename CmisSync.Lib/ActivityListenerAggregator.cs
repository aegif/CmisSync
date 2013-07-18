using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib
{
    /// <summary>
    /// Aggregates the activity status of multiple processes
    /// 
    /// The overall activity is considered "started" if any of the processes is "started";
    /// 
    /// Example chronology (only started/stopped are important, active/down here for readability):
    /// 
    /// PROCESS1 PROCESS2 OVERALL
    /// DOWN     DOWN     DOWN
    /// STARTED  DOWN     STARTED
    /// ACTIVE   STARTED  ACTIVE
    /// ACTIVE   ACTIVE   ACTIVE
    /// STOPPED  ACTIVE   ACTIVE
    /// DOWN     ACTIVE   ACTIVE
    /// DOWN     STOPPED  STOPPED
    /// DOWN     DOWN     DOWN
    /// </summary>
    public class ActivityListenerAggregator : IActivityListener
    {
        /// <summary>
        /// The listener to which overall activity messages are sent.
        /// </summary>
        private IActivityListener overall;


        /// <summary>
        /// Number of processes that have been started but not stopped yet.
        /// </summary>
        private int numberOfActiveProcesses;


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="overallListener">The activity listener to which aggregated activity will be sent.</param>
        public ActivityListenerAggregator(IActivityListener overallListener)
        {
            this.overall = overallListener;
        }


        /// <summary>
        /// Call this method to indicate that activity has started.
        /// </summary>
        public void ActivityStarted()
        {
            numberOfActiveProcesses++;
            overall.ActivityStarted();
        }


        /// <summary>
        /// Call this method to indicate that activity has stopped.
        /// </summary>
        public void ActivityStopped()
        {
            numberOfActiveProcesses--;
            if (numberOfActiveProcesses == 0)
            {
                overall.ActivityStopped();
            }
        }
    }
}
