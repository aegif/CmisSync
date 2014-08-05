using System;
using CmisSync.Lib;
using Notifications;
namespace CmisSync
{
    public class UserNotificationListenerLinux : UserNotificationListener
    {
		StatusIcon status;
		public UserNotificationListenerLinux(StatusIcon icon)
        {
			status = icon;
        }
        public void NotifyUser(string message){
			status.NotifyUser (message);
    }
}
}
