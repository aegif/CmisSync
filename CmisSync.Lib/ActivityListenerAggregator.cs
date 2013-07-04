using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib
{
    /**
     * Aggregates the activity status of multiple processes
     * 
     * The overall activity is considered "started" if any of the processes is "started";
     * 
     * Example chronology (only started/stopped are important, active/down here for readability):
     * 
     * PROCESS1 PROCESS2 OVERALL
     * DOWN     DOWN     DOWN
     * STARTED  DOWN     STARTED
     * ACTIVE   STARTED  ACTIVE
     * ACTIVE   ACTIVE   ACTIVE
     * STOPPED  ACTIVE   ACTIVE
     * DOWN     ACTIVE   ACTIVE
     * DOWN     STOPPED  STOPPED
     * DOWN     DOWN     DOWN
     */
    public class ActivityListenerAggregator : ActivityListener
    {
        /**
         * The listener to which overall activity messages are sent.
         */
        private ActivityListener overall;

        private int numberOfActiveProcesses;

        public ActivityListenerAggregator(ActivityListener overallListener)
        {
            this.overall = overallListener;
        }

        public void ActivityStarted()
        {
            numberOfActiveProcesses++;
            overall.ActivityStarted();
        }

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
