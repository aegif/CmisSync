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
using System.Collections.Generic;

namespace CmisSync.Lib
{

    public static class ListenerFactory {

        private static List<ListenerBase> listeners = new List<ListenerBase> ();


        public static ListenerBase CreateListener (string folder_name, string folder_identifier)
        {
            // Check if the user wants to use a global custom notification service
            string uri = Config.DefaultConfig.GetConfigOption ("announcements_url");

            // Check if the user wants a use a custom notification service for this folder
            if (string.IsNullOrEmpty (uri))
                uri = Config.DefaultConfig.GetFolderOptionalAttribute (folder_name, "announcements_url");

            // This is CmisSync's centralized notification service.
            // It communicates "It's time to sync!" signals between clients.
            //
            // Please see the CmisSync wiki if you wish to run
            // your own service instead
            if (string.IsNullOrEmpty (uri))
                uri = "tcp://notifications.CmisSync.org:80";

            Uri announce_uri = new Uri (uri);

            // Use only one listener per notification service to keep
            // the number of connections as low as possible
            foreach (ListenerBase listener in listeners) {
                if (listener.Server.Equals (announce_uri)) {
                    Logger.LogInfo ("ListenerFactory", "Refered to existing listener for " + announce_uri);

                    // We already seem to have a listener for this server,
                    // refer to the existing one instead
                    listener.AlsoListenTo (folder_identifier);
                    return (ListenerBase) listener;
                }
            }

            listeners.Add (new ListenerTcp (announce_uri, folder_identifier));
            Logger.LogInfo ("ListenerFactory", "Issued new listener for " + announce_uri);

            return (ListenerBase) listeners [listeners.Count - 1];
        }
    }
}
