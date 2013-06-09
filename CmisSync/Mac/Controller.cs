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

using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;

using CmisSync.Lib;

namespace CmisSync {

	public class Controller : ControllerBase {

        public override string PluginsPath {
            get {
                return Path.Combine (NSBundle.MainBundle.ResourcePath, "Plugins");
            }
        }

        // We have to use our own custom made folder watcher, as
        // System.IO.FileSystemWatcher fails watching subfolders on Mac
        private MacWatcher watcher;

        
        public Controller () : base ()
        {
            using (var a = new NSAutoreleasePool ())
            {
                string content_path = Directory.GetParent (System.AppDomain.CurrentDomain.BaseDirectory).ToString ();
    
                string app_path   = Directory.GetParent (content_path).ToString ();
                string growl_path = Path.Combine (app_path, "Frameworks", "Growl.framework", "Growl");
    
                // Needed for Growl
                Dlfcn.dlopen (growl_path, 0);
                NSApplication.Init ();
            }
		}


        public override void Initialize ()
        {
            base.Initialize ();

            this.watcher.Changed += delegate (string path) {
                // Don't even bother with paths in .git/
                if (path.Contains (".git"))
                    return;

                string repo_name;

                if (path.Contains ("/"))
                    repo_name = path.Substring (0, path.IndexOf ("/"));
                else
                    repo_name = path;

                // Ignore changes in the root of each subfolder, these
                // are already handled by the repository
                if (Path.GetFileNameWithoutExtension (path).Equals (repo_name))
                    return;

                repo_name = repo_name.Trim ("/".ToCharArray ());
                FileSystemEventArgs fse_args = new FileSystemEventArgs (WatcherChangeTypes.Changed,
                    Path.Combine (SparkleConfig.DefaultConfig.FoldersPath, path), Path.GetFileName (path));

                foreach (SparkleRepoBase repo in Repositories) {
                    if (repo.Name.Equals (repo_name))
                        repo.OnFileActivity (fse_args);
                }
            };
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

            SparkleLogger.LogInfo ("Controller", "Added " + NSBundle.MainBundle.BundlePath + " to login items");
		}


        public override void InstallProtocolHandler ()
        {
             // We ship CmisSyncInviteHandler.app in the bundle
        }


		// Adds the CmisSync folder to the user's
		// list of bookmarked places
		public override void AddToBookmarks ()
        {/*
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
                            new_favorite.SetValueForKey (new NSString ("CmisSync"),  new NSString ("Name"));

                            new_favorite.SetValueForKey (NSData.FromString ("ImgR SYSL fldr"),  new NSString ("Icon"));

                            new_favorite.SetValueForKey (NSData.FromString (SparkleConfig.DefaultConfig.FoldersPath),
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

            NSUserDefaults.StandardUserDefaults.SetPersistentDomain (sidebar_plist, "com.apple.sidebarlists");*/
		}


		public override bool CreateCmisSyncFolder ()
		{
            this.watcher = new MacWatcher (Program.Controller.FoldersPath);

            if (!Directory.Exists (Program.Controller.FoldersPath)) {
                Directory.CreateDirectory (Program.Controller.FoldersPath);
                return true;

            } else {
                return false;
            }
		}


		public override void OpenFolder (string path)
		{
			NSWorkspace.SharedWorkspace.OpenFile (path);
		}
		

        public override void OpenFile (string path)
        {
            path = Uri.UnescapeDataString (path);
            NSWorkspace.SharedWorkspace.OpenFile (path);
        }


        private string event_log_html;
		public override string EventLogHTML
		{
			get {
                if (string.IsNullOrEmpty (this.event_log_html)) {
                    string html_file_path   = Path.Combine (NSBundle.MainBundle.ResourcePath, "HTML", "event-log.html");
                    string jquery_file_path = Path.Combine (NSBundle.MainBundle.ResourcePath, "HTML", "jquery.js");
                    string html             = File.ReadAllText (html_file_path);
                    string jquery           = File.ReadAllText (jquery_file_path);
                    this.event_log_html     = html.Replace ("<!-- $jquery -->", jquery);
                }

                return this.event_log_html;
			}
		}


        private string day_entry_html;
		public override string DayEntryHTML
		{
			get {
                if (string.IsNullOrEmpty (this.day_entry_html)) {
                    string html_file_path = Path.Combine (NSBundle.MainBundle.ResourcePath, "HTML", "day-entry.html");
                    this.day_entry_html   = File.ReadAllText (html_file_path);
                }

                return this.day_entry_html;
			}
		}
		

        private string event_entry_html;
        public override string EventEntryHTML
        {
            get {
               if (string.IsNullOrEmpty (this.event_entry_html)) {
                   string html_file_path = Path.Combine (NSBundle.MainBundle.ResourcePath, "HTML", "event-entry.html");
                   this.event_entry_html = File.ReadAllText (html_file_path);
               }

               return this.event_entry_html;
            }
        }
	}
}
