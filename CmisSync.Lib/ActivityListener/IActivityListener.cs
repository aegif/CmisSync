using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib.ActivityListener
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


        /// <summary>
        /// Call this method to indicate that is in error state.
        /// </summary>
        void ActivityError(Tuple<string, Exception> error);
    }


    /// <summary>
    /// RAII class for IActivityListener
    /// </summary>
    public class ActivityListenerResource : IDisposable
    {
        private IActivityListener activityListener;

        private bool disposed = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ActivityListenerResource(IActivityListener listener)
        {
            activityListener = listener;
            activityListener.ActivityStarted();
        }

        /// <summary>
        /// Implement <code>IDisposable.Dispose</code>
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose pattern
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if(!disposed)
            {
                activityListener.ActivityStopped();
                disposed = true;
            }
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~ActivityListenerResource()
        {
            Dispose(false);
        }
    }
}
