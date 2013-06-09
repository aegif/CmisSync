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
using System.Diagnostics;
using System.IO;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;

namespace CmisSync {

    public class Controller : ControllerBase {

        public Controller () : base ()
        {
        }


        // Creates a .desktop entry in autostart folder to
        // start CmisSync automatically at login
        public override void CreateStartupItem ()
        {
            string autostart_path = Path.Combine (
                Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "autostart");

            string desktopfile_path = Path.Combine (autostart_path, "cmissync.desktop");

            if (!Directory.Exists (autostart_path))
                Directory.CreateDirectory (autostart_path);

            if (!File.Exists (desktopfile_path)) {
                try {
                    File.WriteAllText (desktopfile_path,
                        "[Desktop Entry]\n" +
                        "Type=Application\n" +
                        "Name=CmisSync\n" +
                        "Exec=cmissync start\n" +
                        "Icon=folder-cmissync\n" +
                        "Terminal=false\n" +
                        "X-GNOME-Autostart-enabled=true\n" +
                        "Categories=Network");

                    Logger.Info ("Added CmisSync to login items");

                } catch (Exception e) {
                    Logger.Info ("Failed adding CmisSync to login items: " + e.Message);
                }
            }
        }
        
        
        public override void InstallProtocolHandler ()
        {
            // cmissync-invite-opener.desktop launches the handler on newer
            // systems (like GNOME 3) that implement the last freedesktop.org specs.
            // For GNOME 2 however we need to tell gconf about the protocol manually

            try {
                // Add the handler to gconf...
                Process process = new Process ();
                process.StartInfo.FileName  = "gconftool-2";
                process.StartInfo.Arguments =
                    "-s /desktop/gnome/url-handlers/cmissync/command 'cmissync open %s' --type String";

                process.Start ();
                process.WaitForExit ();

                // ...and enable it
                process.StartInfo.Arguments = "-s /desktop/gnome/url-handlers/cmissync/enabled --type Boolean true";

                process.Start ();
                process.WaitForExit ();

            } catch {
                // Pity...
            }
        }


        // Adds the CmisSync folder to the user's
        // list of bookmarked places
        public override void AddToBookmarks ()
        {
            string bookmarks_file_path   = Path.Combine (
                    Environment.GetFolderPath (Environment.SpecialFolder.Personal), ".gtk-bookmarks");
            string cmissync_bookmark = "file://" + FoldersPath + " CmisSync";

            if (File.Exists (bookmarks_file_path)) {
                string bookmarks = File.ReadAllText (bookmarks_file_path);

                if (!bookmarks.Contains (cmissync_bookmark))
                    File.AppendAllText (bookmarks_file_path, "file://" + FoldersPath + " CmisSync");

            } else {
                File.WriteAllText (bookmarks_file_path, "file://" + FoldersPath + " CmisSync");
            }
        }


        // Creates the CmisSync folder in the user's home folder
        public override bool CreateCmisSyncFolder ()
        {
            if (!Directory.Exists (FoldersPath)) {
                Directory.CreateDirectory (FoldersPath);
                Logger.Info ("Created '" + FoldersPath + "'");

                string gvfs_command_path = new string [] { Path.VolumeSeparatorChar.ToString (),
                        "usr", "bin", "gvfs-set-attribute" }.Combine ();

                // Add a special icon to the CmisSync folder
                if (File.Exists (gvfs_command_path)) {
                    Process process = new Process ();
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName        = "gvfs-set-attribute";

                    // Clear the custom (legacy) icon path
                    process.StartInfo.Arguments = "-t unset " +
                        FoldersPath + " metadata::custom-icon";

                    process.Start ();
                    process.WaitForExit ();

                    // Give the CmisSync folder an icon name, so that it scales
                    process.StartInfo.Arguments = FoldersPath +
                        " metadata::custom-icon-name 'folder-cmissync'";

                    process.Start ();
                    process.WaitForExit ();
                }

                return true;
            }

            return false;
        }
        

        public override string EventLogHTML {
            get {
                string html_path = new string [] { /*Defines.INSTALL_DIR*/"/usr/share/cmissync", "html", "event-log.html" }.Combine ();
                string jquery_file_path = new string [] { /*Defines.INSTALL_DIR*/"/usr/share/cmissync", "html", "jquery.js" }.Combine ();

                string html   = File.ReadAllText (html_path);
                string jquery = File.ReadAllText (jquery_file_path);

                return html.Replace ("<!-- $jquery -->", jquery);
            }
        }

        
        public override string DayEntryHTML {
            get {
                string path = new string [] { /*Defines.INSTALL_DIR*/"/usr/share/cmissync", "html", "day-entry.html" }.Combine ();
                return File.ReadAllText (path);
            }
        }

        
        public override string EventEntryHTML {
            get {
                string path = new string [] { /*Defines.INSTALL_DIR*/"/usr/share/cmissync", "html", "event-entry.html" }.Combine ();
                return File.ReadAllText (path);
            }
        }

            
        public override void OpenFolder (string path)
        {
            OpenFile (path);
        }


        public override void OpenFile (string path)
        {
            Process process             = new Process ();
            process.StartInfo.FileName  = "xdg-open";
            process.StartInfo.Arguments = "\"" + path + "\"";
            process.Start ();
        }

        public void OpenCmisSyncFolder()
        {
            OpenFolder(ConfigManager.CurrentConfig.FoldersPath);
        }


        public void OpenCmisSyncFolder(string name)
        {
            OpenFolder(new Folder(name).FullPath);
        }

        public void OpenRemoteFolder(string name)
        {
            RepoInfo repo = ConfigManager.CurrentConfig.GetRepoInfo(name);
            Process.Start(CmisUtils.GetBrowsableURL(repo));
        }


    }
}
