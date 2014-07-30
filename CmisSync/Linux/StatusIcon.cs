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
using System.Globalization;

using CmisSync.Lib;
namespace CmisSync {

    public class StatusIcon {
		public void NotifyUser (string message)
		{

			not.Body=message;
			not.Summary="CmisSync";

			not.Show ();

		}

        public StatusIconController Controller = new StatusIconController ();

        private Gdk.Pixbuf [] animation_frames;

		private Notifications.Notification not = new Notifications.Notification ();
        private Menu menu;
        private MenuItem quit_item;
        private MenuItem state_item;
        private bool IsHandleCreated = false;
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
                if(!IsHandleCreated) return;
                Application.Invoke (delegate {
                        (this.state_item.Child as Label).Text = state_text;
                        this.state_item.ShowAll ();
                        });
            };

            Controller.UpdateMenuEvent += delegate (IconState state) {
                Application.Invoke (delegate {
                        CreateMenu ();
                        });
            };

            Controller.UpdateSuspendSyncFolderEvent += delegate (string reponame) {
                if(!IsHandleCreated) return;
                Application.Invoke(delegate
                    {
                        foreach (var menuItem in this.menu.Children) 
                        {
                            if(menuItem is CmisSyncMenuItem && reponame.Equals(((CmisSyncMenuItem)menuItem).RepoName))
                            {
                                foreach (RepoBase aRepo in Program.Controller.Repositories)
                                {
                                    if (aRepo.Name.Equals(reponame))
                                    {
                                        Menu submenu = (Menu)((CmisSyncMenuItem)menuItem).Submenu;
                                        CmisSyncMenuItem pauseItem = (CmisSyncMenuItem)submenu.Children[1];
                                        setSyncItemState(pauseItem, aRepo.Status);
                                        break;
                                    }
                                }
                                break;
                            }
                        }
                    });
            };
        }

        private void setSyncItemState(ImageMenuItem syncitem, SyncStatus status)
        {
            switch (status)
            {
                case SyncStatus.Idle:
                    (syncitem.Child as Label).Text = CmisSync.Properties_Resources.PauseSync;
                    syncitem.Image = new Image (UIHelpers.GetIcon ("media_playback_pause", 12));
                    break;
                case SyncStatus.Suspend:
                    (syncitem.Child as Label).Text = CmisSync.Properties_Resources.ResumeSync;
                    syncitem.Image = new Image (UIHelpers.GetIcon ("media_playback_start", 12));
                    break;
            }
        }

        public void CreateMenu ()
        {
            this.menu = new Menu ();

            // State Menu
            this.state_item = new MenuItem (Controller.StateText) {
                Sensitive = false
            };
            this.menu.Add (this.state_item);

            this.menu.Add (new SeparatorMenuItem ());

            // Folders Menu
            if (Controller.Folders.Length > 0) {
                foreach (string folder_name in Controller.Folders) {
                    Menu submenu = new Menu();
                    ImageMenuItem subfolder_item = new CmisSyncMenuItem (folder_name) {
                        Image = new Image (UIHelpers.GetIcon ("folder-cmissync", 16)),
                        Submenu = submenu,
                        RepoName = folder_name
                    };

                    ImageMenuItem open_localfolder_item = new CmisSyncMenuItem(
                            CmisSync.Properties_Resources.OpenLocalFolder) {
                        Image = new Image (UIHelpers.GetIcon ("folder-cmissync", 16))
                    };
                    open_localfolder_item.Activated += OpenFolderDelegate(folder_name);
/*
                    ImageMenuItem browse_remotefolder_item = new CmisSyncMenuItem(
                            CmisSync.Properties_Resources.BrowseRemoteFolder) {
                        Image = new Image (UIHelpers.GetIcon ("folder-cmissync", 16))
                    };
                    browse_remotefolder_item.Activated += OpenRemoteFolderDelegate(folder_name);
*/

                    ImageMenuItem edit_folder_item = new CmisSyncMenuItem (
                        CmisSync.Properties_Resources.EditTitle);
                    edit_folder_item.Activated += EditFolderDelegate(folder_name);

                    ImageMenuItem suspend_folder_item = new CmisSyncMenuItem(
                            CmisSync.Properties_Resources.PauseSync) {
                        RepoName = folder_name
                    };
                    foreach (RepoBase aRepo in Program.Controller.Repositories)
                    {
                        if (aRepo.Name.Equals(folder_name))
                        {
                            setSyncItemState(suspend_folder_item, aRepo.Status);
                            break;
                        }
                    }
                    suspend_folder_item.Activated += SuspendSyncFolderDelegate(folder_name);

                    ImageMenuItem remove_folder_from_sync_item = new CmisSyncMenuItem(
                            CmisSync.Properties_Resources.RemoveFolderFromSync) {
                        Image = new Image (UIHelpers.GetIcon ("document-deleted", 12))
                    };
                    remove_folder_from_sync_item.Activated += RemoveFolderFromSyncDelegate(folder_name);

                    submenu.Add(open_localfolder_item);
                    //submenu.Add(browse_remotefolder_item);
                    submenu.Add(suspend_folder_item);
                    submenu.Add(edit_folder_item);
                    submenu.Add(new SeparatorMenuItem());
                    submenu.Add(remove_folder_from_sync_item);

                    this.menu.Add (subfolder_item);
                }

                this.menu.Add (new SeparatorMenuItem ());
            }

            // Add Menu
            MenuItem add_item = new MenuItem (
                    CmisSync.Properties_Resources.AddARemoteFolder);
            add_item.Activated += delegate {
                Controller.AddRemoteFolderClicked ();
            };
            this.menu.Add(add_item);

            this.menu.Add (new SeparatorMenuItem ());

            // Log Menu
            MenuItem log_item = new MenuItem(
                    CmisSync.Properties_Resources.ViewLog);
            log_item.Activated += delegate
            {
                Controller.LogClicked();
            };
            this.menu.Add(log_item);

            // About Menu
            MenuItem about_item = new MenuItem (
                    CmisSync.Properties_Resources.About);
            about_item.Activated += delegate {
                Controller.AboutClicked ();
            };
            this.menu.Add (about_item);

            this.quit_item = new MenuItem (
                    CmisSync.Properties_Resources.Exit) {
                Sensitive = true
            };

            this.quit_item.Activated += delegate {
                Controller.QuitClicked ();
            };

            this.menu.Add (this.quit_item);
            this.menu.ShowAll ();

#if HAVE_APP_INDICATOR
            this.indicator.Menu = this.menu;
#endif
            this.IsHandleCreated = true;
        }


        // A method reference that makes sure that opening the
        // event log for each repository works correctly
        private EventHandler OpenFolderDelegate (string name)
        {
            return delegate {
                Controller.LocalFolderClicked (name);
            };
        }

        private EventHandler EditFolderDelegate (string name)
        {
            return delegate {
                Controller.EditFolderClicked (name);
            };
        }

        private EventHandler SuspendSyncFolderDelegate(string reponame)
        {
            return delegate
            {
                Controller.SuspendSyncClicked(reponame);
            };
        }

        private EventHandler RemoveFolderFromSyncDelegate(string reponame)
        {
            return delegate
            {
                using( Dialog dialog = new Dialog
                    (String.Format(CmisSync.Properties_Resources.RemoveSyncTitle), null, Gtk.DialogFlags.DestroyWithParent))
                {
                    dialog.Modal = true;
                    using (var noButton = dialog.AddButton ("No, please continue synchronizing", ResponseType.No))
                    using (var yesButton = dialog.AddButton ("Yes, stop synchronizing permanently", ResponseType.Yes))
                    {
                        dialog.Response += delegate (object obj, ResponseArgs args){
                            if(args.ResponseId == ResponseType.Yes)
                                Controller.RemoveFolderFromSyncClicked(reponame);
                        };
                        dialog.Run();
                        dialog.Destroy();
                    }
                }
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
        public string RepoName {get;set;}
        public CmisSyncMenuItem (string text) : base (text)
        {
            SetProperty ("always-show-image", new GLib.Value (true));
        }
    }


}
