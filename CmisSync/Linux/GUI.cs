//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;

using Gtk;
using CmisSync.Lib;

namespace CmisSync {

    public class GUI {

        public StatusIcon StatusIcon;
        public Setup Setup;
        public About About;

        public static string AssetsPath =
            (null != Environment.GetEnvironmentVariable("CMISSYNC_ASSETS_DIR"))
            ? Environment.GetEnvironmentVariable("CMISSYNC_ASSETS_DIR") : Defines.ASSETS_DIR;

        public GUI ()
        {
            Application.Init();

            Setup      = new Setup ();
            About      = new About ();
            StatusIcon = new StatusIcon ();
			CmisSync.Lib.Utils.SetUserNotificationListener (new UserNotificationListenerLinux (StatusIcon));
            Program.Controller.UIHasLoaded ();
        }


        // Runs the application
        public void Run ()
        {
            Application.Run ();
        }
    }
}
