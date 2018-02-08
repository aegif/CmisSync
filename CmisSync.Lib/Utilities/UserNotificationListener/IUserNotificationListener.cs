using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib.Utilities.UserNotificationListener
{
    /// <summary>
    /// Interface for a component that can display a notification message to the end user.
    /// A GUI program might raise a pop-up, a CLI program might print a line.
    /// </summary>
    public interface IUserNotificationListener
    {
        /// <summary>
        /// Send a message to the end user.
        /// </summary>
        void NotifyUser(string message);
    }
}
