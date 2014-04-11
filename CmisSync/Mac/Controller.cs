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
using System.Collections.Generic;
using System.Text;

using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;

using Mono.Unix.Native;

using CmisSync.Lib;
using CmisSync.Lib.Events;
using CmisSync.Lib.Cmis;

using log4net;

namespace CmisSync {

	public class Controller : ControllerBase {

        private NSUserNotificationCenter notificationCenter;
        private Dictionary<string,DateTime> transmissionFiles = new Dictionary<string, DateTime> ();
        private Object transmissionLock = new object ();
        private int notificationInterval = 5;
        private int notificationKeep = 5;

        private class ComparerNSUserNotification : IComparer<NSUserNotification>
        {
            public int Compare (NSUserNotification x, NSUserNotification y)
            {
                DateTime xDate = x.DeliveryDate;
                DateTime yDate = y.DeliveryDate;
                return xDate.CompareTo (yDate);
            }
        }

		public Controller () : base ()
        {
            using (var a = new NSAutoreleasePool ())
            {
                NSApplication.Init ();
            }

            // We get the Default notification Center
            notificationCenter = NSUserNotificationCenter.DefaultUserNotificationCenter;

            notificationCenter.DidDeliverNotification += (s, e) => 
            {
                Console.WriteLine("Notification Delivered");
            };

            notificationCenter.DidActivateNotification += (s, e) => 
            {
                LocalFolderClicked (Path.GetDirectoryName (e.Notification.InformativeText));
            };

            // If we return true here, Notification will show up even if your app is TopMost.
            notificationCenter.ShouldPresentNotification = (c, n) => { return true; };

            OnTransmissionListChanged += delegate {

                using (var a = new NSAutoreleasePool()) {
                    notificationCenter.BeginInvokeOnMainThread(delegate {
                        lock (transmissionLock) {
                            List<FileTransmissionEvent> transmissions = ActiveTransmissions();
                            NSUserNotification[] notifications = notificationCenter.DeliveredNotifications;
                            List<NSUserNotification> finishedNotifications = new List<NSUserNotification> ();
                            foreach (NSUserNotification notification in notifications) {
                                FileTransmissionEvent transmission = transmissions.Find( (FileTransmissionEvent e)=>{return (e.Path == notification.InformativeText);});
                                if (transmission == null) {
                                    finishedNotifications.Add (notification);
                                } else {
                                    if (transmissionFiles.ContainsKey (transmission.Path)) {
                                        transmissions.Remove(transmission);
                                    } else {
                                        notificationCenter.RemoveDeliveredNotification (notification);
                                    }
                                }
                            }
                            finishedNotifications.Sort (new ComparerNSUserNotification ());
                            for (int i = 0; i<(notifications.Length - notificationKeep) && i<finishedNotifications.Count; ++i) {
                                notificationCenter.RemoveDeliveredNotification (finishedNotifications[i]);
                            }
                            foreach (FileTransmissionEvent transmission in transmissions) {
                                if (transmission.Status.Aborted == true) {
                                    continue;
                                }
                                if (transmission.Status.Completed == true) {
                                    continue;
                                }
                                if (transmission.Status.FailedException != null) {
                                    continue;
                                }
                                NSUserNotification notification = new NSUserNotification();
                                notification.Title = Path.GetFileName (transmission.Path);
                                notification.Subtitle = TransmissionStatus(transmission);
                                notification.InformativeText = transmission.Path;
//                                notification.SoundName = NSUserNotification.NSUserNotificationDefaultSoundName;
                                transmission.TransmissionStatus += TransmissionReport;
                                notification.DeliveryDate = NSDate.Now;
                                notificationCenter.DeliverNotification (notification);
                                transmissionFiles.Add (transmission.Path, notification.DeliveryDate);
                                UpdateFileStatus (transmission, null);
                            }
                        }
                    });
                }
            };
		}

        private void UpdateFileStatus(FileTransmissionEvent transmission, TransmissionProgressEventArgs e)
        {
            if (e == null) {
                e = transmission.Status;
            }

            string filePath = transmission.CachePath;
            if (filePath == null || !File.Exists (filePath)) {
                filePath = transmission.Path;
            }
            if (!File.Exists (filePath)) {
                Logger.Error (String.Format ("None exist {0} for file status update", filePath));
                return;
            }

            string extendAttrKey = "com.apple.progress.fractionCompleted";

            if ((e.Aborted == true || e.Completed == true || e.FailedException != null)) {
                Syscall.removexattr (filePath, extendAttrKey);
                try {
                    NSFileAttributes attr = NSFileManager.DefaultManager.GetAttributes (filePath);
                    attr.CreationDate = (new FileInfo(filePath)).CreationTime;
                    NSFileManager.DefaultManager.SetAttributes (attr, filePath);
                } catch (Exception ex) {
                    Logger.Error (String.Format ("Exception to set {0} creation time for file status update: {1}", filePath, ex));
                }
            } else {
                double percent = transmission.Status.Percent.GetValueOrDefault() / 100;
                if (percent < 1) {
                    Syscall.setxattr (filePath, extendAttrKey, Encoding.ASCII.GetBytes (percent.ToString ()));
                    try {
                        NSFileAttributes attr = NSFileManager.DefaultManager.GetAttributes (filePath);
                        attr.CreationDate = new DateTime (1984, 1, 24, 8, 0, 0, DateTimeKind.Utc);
                        NSFileManager.DefaultManager.SetAttributes (attr, filePath);
                    } catch (Exception ex) {
                        Logger.Error (String.Format ("Exception to set {0} creation time for file status update: {1}", filePath, ex));
                    }
                } else {
                    Syscall.removexattr (filePath, extendAttrKey);
                    try {
                        NSFileAttributes attr = NSFileManager.DefaultManager.GetAttributes (filePath);
                        attr.CreationDate = (new FileInfo(filePath)).CreationTime;
                        NSFileManager.DefaultManager.SetAttributes (attr, filePath);
                    } catch (Exception ex) {
                        Logger.Error (String.Format ("Exception to set {0} creation time for file status update: {1}", filePath, ex));
                    }
                }
            }

        }

        private string TransmissionStatus(FileTransmissionEvent transmission)
        {
            string type = "Unknown";
            switch (transmission.Type) {
            case FileTransmissionType.UPLOAD_NEW_FILE:
                type = "Upload new file";
                break;
            case FileTransmissionType.UPLOAD_MODIFIED_FILE:
                type = "Update remote file";
                break;
            case FileTransmissionType.DOWNLOAD_NEW_FILE:
                type = "Download new file";
                break;
            case FileTransmissionType.DOWNLOAD_MODIFIED_FILE:
                type = "Update local file";
                break;
            }
            if (transmission.Status.Aborted == true) {
                type += " aborted";
            } else if (transmission.Status.Completed == true) {
                type += " completed";
            } else if (transmission.Status.FailedException != null) {
                type += " failed";
            }

            return String.Format("{0} ({1:###.#}% {2})",
                type,
                Math.Round (transmission.Status.Percent.GetValueOrDefault(), 1),
                CmisSync.Lib.Utils.FormatBandwidth ((long)transmission.Status.BitsPerSecond.GetValueOrDefault()));
        }

        private void TransmissionReport(object sender, TransmissionProgressEventArgs e)
        {
            using (var a = new NSAutoreleasePool()) {
                FileTransmissionEvent transmission = sender as FileTransmissionEvent;
                if (transmission == null) {
                    return;
                }
                lock (transmissionLock) {
                    if ((e.Aborted == true || e.Completed == true || e.FailedException != null)) {
                        transmission.TransmissionStatus -= TransmissionReport;
                        transmissionFiles.Remove (transmission.Path);
                    } else {
                        TimeSpan diff = NSDate.Now - transmissionFiles [transmission.Path];
                        if (diff.Seconds < notificationInterval) {
                            return;
                        }
                        transmissionFiles [transmission.Path] = NSDate.Now;
                    }
                    UpdateFileStatus (transmission, e);
                }
                notificationCenter.BeginInvokeOnMainThread (delegate
                {
                    lock (transmissionLock) {
                        NSUserNotification[] notifications = notificationCenter.DeliveredNotifications;
                        foreach (NSUserNotification notification in notifications) {
                            if (notification.InformativeText == transmission.Path) {
                                notificationCenter.RemoveDeliveredNotification (notification);
                                notification.DeliveryDate = NSDate.Now;
                                notification.Subtitle = TransmissionStatus (transmission);
                                notificationCenter.DeliverNotification (notification);
                                return;
                            }
                        }
                    }
                });
            }
        }

		public override void CreateStartupItem ()
		{
            // There aren't any bindings in MonoMac to support this yet, so
            // we call out to an applescript to do the job
            Process process = new Process ();
            process.StartInfo.FileName               = "osascript";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute        = false;

            process.StartInfo.Arguments = "-e 'tell application \"System Events\" to " +
                "make login item at end with properties {path:\"" + NSBundle.MainBundle.BundlePath + "\", hidden:false}'";

            process.Start ();
            process.WaitForExit ();
		}

		// Adds the CmisSync folder to the user's
		// list of bookmarked places
		public override void AddToBookmarks ()
        {
			/*
            NSMutableDictionary sidebar_plist = NSMutableDictionary.FromDictionary (
                NSUserDefaults.StandardUserDefaults.PersistentDomainForName ("com.apple.sidebarlists"));

            // Go through the sidebar categories
            foreach (NSString sidebar_category in sidebar_plist.Keys) {

                // Find the favorites
                if (sidebar_category.ToString ().Equals ("favorites")) {

                    // Get the favorites
                    NSMutableDictionary favorites = NSMutableDictionary.FromDictionary(
                        (NSDictionary) sidebar_plist.ValueForKey (sidebar_category));

                    // Go through the favorites
                    foreach (NSString favorite in favorites.Keys) {

                        // Find the custom favorites
                        if (favorite.ToString ().Equals ("VolumesList")) {

                            // Get the custom favorites
                            NSMutableArray custom_favorites = (NSMutableArray) favorites.ValueForKey (favorite);

                            NSMutableDictionary properties = new NSMutableDictionary ();
                            properties.SetValueForKey (new NSString ("1935819892"), new NSString ("com.apple.LSSharedFileList.TemplateSystemSelector"));

                            NSMutableDictionary new_favorite = new NSMutableDictionary ();
							new_favorite.SetValueForKey (new NSString ("DataSpace Sync"),  new NSString ("Name"));

                            new_favorite.SetValueForKey (NSData.FromString ("ImgR SYSL fldr"),  new NSString ("Icon"));

							new_favorite.SetValueForKey (NSData.FromString (ConfigManager.CurrentConfig.FoldersPath),
                                new NSString ("Alias"));

                            new_favorite.SetValueForKey (properties, new NSString ("CustomItemProperties"));

                            // Add to the favorites
                            custom_favorites.Add (new_favorite);
                            favorites.SetValueForKey ((NSArray) custom_favorites, new NSString (favorite.ToString ()));
                            sidebar_plist.SetValueForKey (favorites, new NSString (sidebar_category.ToString ()));
                        }
                    }

                }
            }

            NSUserDefaults.StandardUserDefaults.SetPersistentDomain (sidebar_plist, "com.apple.sidebarlists");
            */
		}


		public override bool CreateCmisSyncFolder ()
		{

            if (!Directory.Exists (Program.Controller.FoldersPath)) {
                Directory.CreateDirectory (Program.Controller.FoldersPath);
                return true;

            } else {
                return false;
            }
		}

		public void OpenCmisSyncFolder (string reponame)
		{
			foreach(CmisSync.Lib.RepoBase repo in Program.Controller.Repositories)
			{
				if(repo.Name.Equals(reponame))
				{
					LocalFolderClicked(repo.LocalPath);
					break;
				}
			}
		}

		/// <summary>
		/// With the default web browser, open the remote folder of a CmisSync synchronized folder.
		/// </summary>
		/// <param name="name">Name of the synchronized folder</param>
		public void OpenRemoteFolder(string name)
		{
			Config.SyncConfig.Folder folder = ConfigManager.CurrentConfig.getFolder(name);
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

		public void ShowLog (string str)
		{
			System.Diagnostics.Process.Start("/usr/bin/open", "-a Console " + str);
		}

		public void LocalFolderClicked (string path)
		{
            notificationCenter.BeginInvokeOnMainThread (delegate
            {
                NSWorkspace.SharedWorkspace.OpenFile (path);
            });
		}
		

        public void OpenFile (string path)
        {
            path = Uri.UnescapeDataString (path);
            notificationCenter.BeginInvokeOnMainThread (delegate
            {
                NSWorkspace.SharedWorkspace.OpenFile (path);
            });
        }
	}
}
