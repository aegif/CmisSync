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
using System.Reflection;
using Forms = System.Windows.Forms;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;

namespace CmisSync
{
    /// <summary>
    /// Windows-specific part of the main CmisSync controller.
    /// </summary>
    public class Controller : ControllerBase, UserNotificationListener
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Controller()
            : base()
        {
            CmisSync.Lib.Utils.SetUserNotificationListener(this);
			PathRepresentationConverter.SetConverter(new WindowsPathRepresentationConverter());
        }


        /// <summary>
        /// Initialize the controller
        /// </summary>
        /// <param name="firstRun">Whether it is the first time that CmisSync is being run.</param>
        public override void Initialize(Boolean firstRun)
        {
            base.Initialize(firstRun);
        }


        /// <summary>
        /// Add CmisSync to the list of programs to be started up when the user logs into Windows.
        /// </summary>
        public override void CreateStartupItem()
        {
            string startup_folder_path = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcut_path = Path.Combine(startup_folder_path, "CmisSync.lnk");

            if (File.Exists(shortcut_path))
                File.Delete(shortcut_path);

            string shortcut_target = Forms.Application.ExecutablePath;

            Shortcut shortcut = new Shortcut();
            shortcut.Create(shortcut_target, shortcut_path);
        }


        /// <summary>
        /// Add CmisSync to the user's Windows Explorer bookmarks.
        /// </summary>
        public override void AddToBookmarks()
        {
            string user_profile_path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string shortcut_path = Path.Combine(user_profile_path, "Links", "CmisSync.lnk");

            if (File.Exists(shortcut_path))
                File.Delete(shortcut_path);

            Shortcut shortcut = new Shortcut();

            shortcut.Create(FoldersPath, shortcut_path, Forms.Application.ExecutablePath, 0);
        }


        /// <summary>
        /// Create the user's CmisSync settings folder.
        /// This folder contains databases, the settings file and the log file.
        /// </summary>
        /// <returns></returns>
        public override bool CreateCmisSyncFolder()
        {
            if (Directory.Exists(FoldersPath))
                return false;

            Directory.CreateDirectory(FoldersPath);
            File.SetAttributes(FoldersPath, File.GetAttributes(FoldersPath) | FileAttributes.System);

            Logger.Info("Config | Created '" + FoldersPath + "'");

            string app_path = Path.GetDirectoryName(Forms.Application.ExecutablePath);
            string icon_file_path = Path.Combine(app_path, "Pixmaps", "cmissync-folder.ico");

            if (!File.Exists(icon_file_path))
            {
                icon_file_path = Assembly.GetExecutingAssembly().Location;
            }
            string ini_file_path = Path.Combine(FoldersPath, "desktop.ini");

            string ini_file = "[.ShellClassInfo]\r\n" +
                    "IconFile=" + icon_file_path + "\r\n" +
                    "IconIndex=0\r\n" +
                    "InfoTip=CmisSync\r\n" +
                    "IconResource=" + icon_file_path + ",0\r\n" +
                    "[ViewState]\r\n" +
                    "Mode=\r\n" +
                    "Vid=\r\n" +
                    "FolderType=Generic\r\n";

            try
            {
                File.WriteAllText(ini_file_path, ini_file);

                    File.SetAttributes(ini_file_path,
                        File.GetAttributes(ini_file_path) | FileAttributes.Hidden | FileAttributes.System);

            }
            catch (IOException e)
            {
                Logger.Info("Config | Failed setting icon for '" + FoldersPath + "': " + e.Message);
            }
            return true;
        }


        /// <summary>
        /// With Windows Explorer, open the folder where the local synchronized folders are.
        /// </summary>
        public void OpenCmisSyncFolder()
        {
            Utils.OpenFolder(ConfigManager.CurrentConfig.FoldersPath);
        }


        /// <summary>
        /// With Windows Explorer, open the local folder of a CmisSync synchronized folder.
        /// </summary>
        /// <param name="name">Name of the synchronized folder</param>
        public void OpenCmisSyncFolder(string name)
        {
            Config.SyncConfig.Folder folder = ConfigManager.CurrentConfig.GetFolder(name);
            if (folder != null)
                Utils.OpenFolder(folder.LocalPath);
            else if (String.IsNullOrWhiteSpace(name))
                OpenCmisSyncFolder();
            else
                Logger.Warn("Could not find requested config for \"" + name + "\"");
        }

        /// <summary>
        /// With the default web browser, open the remote folder of a CmisSync synchronized folder.
        /// </summary>
        /// <param name="name">Name of the synchronized folder</param>
        public void OpenRemoteFolder(string name)
        {
            Config.SyncConfig.Folder folder = ConfigManager.CurrentConfig.GetFolder(name);
            if (folder != null)
            {
                RepoInfo repo = folder.GetRepoInfo();
                Process.Start(CmisUtils.GetBrowsableURL(repo));
            }
            else
            {
                Logger.Warn("Could not find requested config for \"" + name + "\"");
            }
        }


        /// <summary>
        /// Send a message to the end user.
        /// </summary>
        public void NotifyUser(string message)
        {
            System.Windows.Forms.MessageBox.Show(message, "CmisSync notification");
        }


        /// <summary>
        /// Quit CmisSync.
        /// </summary>
        public override void Quit()
        {
            base.Quit();
        }

        /// <summary>
        /// Open the log file so that the user can check what is going on, and send it to developers.
        /// </summary>
        /// <param name="path">Path to the log file</param>
        public void ShowLog(string path)
        {
            var p = new Process();
            p.StartInfo.FileName = path;
            p.StartInfo.UseShellExecute = true;
            p.Start();
        }
    }
}