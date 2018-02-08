using System;
using log4net;

namespace CmisSync.Lib.Utilities.UserNotificationListener
{
    public static class UserNotificationListenerUtil
    {
        private static ILog logger = LogManager.GetLogger(typeof (UserNotificationListenerUtil));

        /// <summary>
        /// Component which will receive notifications intended for the end-user.
        /// A GUI program might raise a pop-up, a CLI program might print a line.
        /// </summary>
        private static IUserNotificationListener userNotificationListener;


        /// <summary>
        /// Register the component which will receive notifications intended for the end-user.
        /// </summary>
        public static void SetUserNotificationListener (IUserNotificationListener listener)
        {
            userNotificationListener = listener;
        }


        /// <summary>
        /// Send a message to the end user.
        /// </summary>
        public static void NotifyUser (string message)
        {
            logger.Info (message);
            if (userNotificationListener != null) {
                userNotificationListener.NotifyUser (message);
            }
        }    
    }
}
