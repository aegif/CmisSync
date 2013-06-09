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

#if HAVE_APP_INDICATOR
using AppIndicator;
#endif
using Gtk;
using Mono.Unix;

namespace CmisSync {

    public class StatusIcon {

        public StatusIconController Controller = new StatusIconController ();

        private Gdk.Pixbuf [] animation_frames;

        private Menu menu;
        private MenuItem recent_events_item;
        private MenuItem quit_item;
        private MenuItem state_item;

        #if HAVE_APP_INDICATOR
        private ApplicationIndicator indicator;
        #else
        private Gtk.StatusIcon status_icon;
        #endif


        public StatusIcon ()
        {
            CreateAnimationFrames ();

            #if HAVE_APP_INDICATOR
            this.indicator = new ApplicationIndicator ("cmissync",
                "process-syncing-i", Category.ApplicationStatus);

            this.indicator.Status = Status.Active;
            #else
            this.status_icon        = new Gtk.StatusIcon ();
            this.status_icon.Pixbuf = this.animation_frames [0];

            this.status_icon.Activate  += ShowMenu; // Primary mouse button click
            this.status_icon.PopupMenu += ShowMenu; // Secondary mouse button click
            #endif

            CreateMenu ();


            Controller.UpdateIconEvent += delegate (int icon_frame) {
                Application.Invoke (delegate {
                    if (icon_frame > -1) {
                        #if HAVE_APP_INDICATOR
                        string icon_name = "process-syncing-";
                        for (int i = 0; i <= icon_frame; i++)
                            icon_name += "i";

                        this.indicator.IconName = icon_name;

                        // Force update of the icon
                        this.indicator.Status = Status.Attention;
                        this.indicator.Status = Status.Active;
                        #else
                        this.status_icon.Pixbuf = this.animation_frames [icon_frame];
                        #endif

                    } else {
                        #if HAVE_APP_INDICATOR
                        this.indicator.IconName = "process-syncing-error";

                        // Force update of the icon
                        this.indicator.Status = Status.Attention;
                        this.indicator.Status = Status.Active;
                        #else
						this.status_icon.Pixbuf = UIHelpers.GetIcon ("process-syncing-error", 24);
                        #endif
                    }
                });
            };

            Controller.UpdateStatusItemEvent += delegate (string state_text) {
                Application.Invoke (delegate {
                    (this.state_item.Child as Label).Text = state_text;
                    this.state_item.ShowAll ();
                });
            };

            Controller.UpdateQuitItemEvent += delegate (bool item_enabled) {
                Application.Invoke (delegate {
                    this.quit_item.Sensitive = item_enabled;
                    this.quit_item.ShowAll ();
                });
            };

            Controller.UpdateOpenRecentEventsItemEvent += delegate (bool item_enabled) {
                Application.Invoke (delegate {
                    this.recent_events_item.Sensitive = item_enabled;
                    this.recent_events_item.ShowAll ();
                });
            };

            Controller.UpdateMenuEvent += delegate (IconState state) {
                Application.Invoke (delegate {
                    CreateMenu ();
                });
            };
        }


        public void CreateMenu ()
        {
            this.menu = new Menu ();

                this.state_item = new MenuItem (Controller.StateText) {
                    Sensitive = false
                };

            this.menu.Add (this.state_item);
            this.menu.Add (new SeparatorMenuItem ());

                ImageMenuItem folder_item = new CmisSyncMenuItem ("CmisSync"){
                    Image = new Image (UIHelpers.GetIcon ("folder-cmissync", 16))
                };

                folder_item.Activated += delegate {
                    Controller.CmisSyncClicked ();
                };
                
            this.menu.Add (folder_item);

                if (Program.Controller.Folders.Count > 0) {
                    foreach (string folder_name in Controller.Folders) {
                        Gdk.Pixbuf folder_icon;

                        if (Program.Controller.UnsyncedFolders.Contains (folder_name)) {
                            folder_icon = IconTheme.Default.LoadIcon ("dialog-error", 16,
                                IconLookupFlags.GenericFallback);

                        } else {
                            folder_icon = IconTheme.Default.LoadIcon ("folder", 16,
                                IconLookupFlags.GenericFallback);
                        }

                        ImageMenuItem subfolder_item = new CmisSyncMenuItem (folder_name) {
                            Image = new Image (folder_icon)
                        };

                        subfolder_item.Activated += OpenFolderDelegate (folder_name);
                        this.menu.Add (subfolder_item);
                    }

                    Menu submenu = new Menu ();

                    foreach (string folder_name in Controller.OverflowFolders) {
                        Gdk.Pixbuf folder_icon;

                        if (Program.Controller.UnsyncedFolders.Contains (folder_name)) {
                            folder_icon = IconTheme.Default.LoadIcon ("dialog-error", 16,
                                IconLookupFlags.GenericFallback);

                        } else {
                            folder_icon = IconTheme.Default.LoadIcon ("folder", 16,
                                IconLookupFlags.GenericFallback);
                        }

                        ImageMenuItem subfolder_item = new CmisSyncMenuItem (folder_name) {
                            Image = new Image (folder_icon)
                        };

                        subfolder_item.Activated += OpenFolderDelegate (folder_name);
                        submenu.Add (subfolder_item);
                    }

                    if (submenu.Children.Length > 0) {
                        CmisSyncMenuItem more_item = new CmisSyncMenuItem ("More Projects") {
                            Submenu = submenu
                        };

                        this.menu.Add (new SeparatorMenuItem ());
                        this.menu.Add (more_item);
                    }

                }

                this.menu.Add (new SeparatorMenuItem ());

                MenuItem sync_item = new MenuItem ("Add Hosted Project…");

                sync_item.Activated += delegate {
                    Controller.AddHostedProjectClicked ();
                };

            this.menu.Add (sync_item);

            this.recent_events_item = new MenuItem ("Recent Changes…");

                this.recent_events_item.Sensitive = Controller.OpenRecentEventsItemEnabled;

                this.recent_events_item.Activated += delegate {
                    Controller.OpenRecentEventsClicked ();
                };

            this.menu.Add (this.recent_events_item);
            this.menu.Add (new SeparatorMenuItem ());

            
            MenuItem notify_item;
                                                             
                if (Program.Controller.NotificationsEnabled)
                    notify_item = new MenuItem ("Turn Notifications Off");
                else
                    notify_item = new MenuItem ("Turn Notifications On");

                notify_item.Activated += delegate {
					Application.Invoke (delegate {
	                    Program.Controller.ToggleNotifications ();
					
					    if (Program.Controller.NotificationsEnabled)
	                    	(notify_item.Child as Label).Text = "Turn Notifications Off";
	                	else
	                    	(notify_item.Child as Label).Text = "Turn Notifications On";
					});
                };

            this.menu.Add (notify_item);
            this.menu.Add (new SeparatorMenuItem ());


                MenuItem about_item = new MenuItem ("About CmisSync");

                about_item.Activated += delegate {
                    Controller.AboutClicked ();
                };

            this.menu.Add (about_item);
            this.menu.Add (new SeparatorMenuItem ());


                this.quit_item = new MenuItem ("Quit") {
                    Sensitive = Controller.QuitItemEnabled
                };

                this.quit_item.Activated += delegate {
                    Controller.QuitClicked ();
                };

            this.menu.Add (this.quit_item);
            this.menu.ShowAll ();

            #if HAVE_APP_INDICATOR
            this.indicator.Menu = this.menu;
            #endif
        }


        // A method reference that makes sure that opening the
        // event log for each repository works correctly
        private EventHandler OpenFolderDelegate (string name)
        {
            return delegate {
                Controller.LocalFolderClicked (name);
            };
        }


        private void CreateAnimationFrames ()
        {
            this.animation_frames = new Gdk.Pixbuf [] {
                UIHelpers.GetIcon ("process-syncing-i", 24),
                UIHelpers.GetIcon ("process-syncing-ii", 24),
                UIHelpers.GetIcon ("process-syncing-iii", 24),
                UIHelpers.GetIcon ("process-syncing-iiii", 24),
                UIHelpers.GetIcon ("process-syncing-iiiii", 24)
            };
        }


        #if !HAVE_APP_INDICATOR
        // Makes the menu visible
        private void ShowMenu (object o, EventArgs args)
        {
            this.menu.Popup (null, null, SetPosition, 0, Global.CurrentEventTime);
        }


        // Makes sure the menu pops up in the right position
        private void SetPosition (Menu menu, out int x, out int y, out bool push_in)
        {
            Gtk.StatusIcon.PositionMenu (menu, out x, out y, out push_in, this.status_icon.Handle);
        }
        #endif
    }

    
    public class CmisSyncMenuItem : ImageMenuItem {

        public CmisSyncMenuItem (string text) : base (text)
        {
            SetProperty ("always-show-image", new GLib.Value (true));
        }
    }
}
