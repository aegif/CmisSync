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
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using System.Globalization;

namespace CmisSync
{

    public class StatusIcon : Control
    {

        public StatusIconController Controller = new StatusIconController();

        private Drawing.Bitmap[] animation_frames;
        private Drawing.Bitmap error_icon;

        private ContextMenu context_menu;


        private CmisMenuItem state_item;
        private CmisMenuItem exit_item;

        private NotifyIcon notify_icon = new NotifyIcon();



        public StatusIcon()
        {
            CreateAnimationFrames();

            this.notify_icon.Icon = animation_frames[0];
            this.notify_icon.HeaderText = "CmisSync";

            CreateMenu();


            Controller.UpdateIconEvent += delegate(int icon_frame)
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    if (icon_frame > -1)
                        this.notify_icon.Icon = animation_frames[icon_frame];
                    else
                        this.notify_icon.Icon = this.error_icon;
                });
            };

            Controller.UpdateStatusItemEvent += delegate(string state_text)
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    this.state_item.Header = state_text;
                    this.state_item.UpdateLayout();
                    this.notify_icon.HeaderText = "CmisSync\n" + state_text;
                });
            };

            Controller.UpdateMenuEvent += delegate(IconState state)
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    CreateMenu();
                });
            };

            Controller.UpdateQuitItemEvent += delegate(bool item_enabled)
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    this.exit_item.IsEnabled = item_enabled;
                    this.exit_item.UpdateLayout();
                });
            };
        }


        public void CreateMenu()
        {
            this.context_menu = new ContextMenu();

            this.state_item = new CmisMenuItem()
            {
                Header = Controller.StateText,
                IsEnabled = false
            };

            this.notify_icon.HeaderText = "CmisSync\n" + Controller.StateText;

            Image folder_image = new Image()
            {
                Source = UIHelpers.GetImageSource("CmisSync-folder"),
                Width = 16,
                Height = 16
            };

            CmisMenuItem folder_item = new CmisMenuItem()
            {
                Header = "CmisSync",
                Icon = folder_image
            };

            folder_item.Click += delegate
            {
                Controller.CmisSyncClicked();
            };

            CmisMenuItem add_item = new CmisMenuItem()
            {
                Header = CmisSync.Properties.Resources.ResourceManager.GetString("AddARemoteFolder", CultureInfo.CurrentCulture)
            };

            add_item.Click += delegate
            {
                Controller.AddHostedProjectClicked();
            };

            CmisMenuItem notify_item = new CmisMenuItem()
            {
                Header = "Notifications"
            };

            CheckBox notify_check_box = new CheckBox()
            {
                Margin = new Thickness(6, 0, 0, 0),
                IsChecked = (Controller.Folders.Length > 0 && Program.Controller.NotificationsEnabled)
            };

            notify_item.Icon = notify_check_box;

            notify_check_box.Click += delegate
            {
                this.context_menu.IsOpen = false;
                Program.Controller.ToggleNotifications();
                notify_check_box.IsChecked = Program.Controller.NotificationsEnabled;
            };

            notify_item.Click += delegate
            {
                Program.Controller.ToggleNotifications();
                notify_check_box.IsChecked = Program.Controller.NotificationsEnabled;
            };

            CmisMenuItem about_item = new CmisMenuItem()
            {
                Header = "About CmisSync"
            };

            about_item.Click += delegate
            {
                Controller.AboutClicked();
            };

            this.exit_item = new CmisMenuItem()
            {
                Header = CmisSync.Properties.Resources.ResourceManager.GetString("Exit", CultureInfo.CurrentCulture)
            };

            this.exit_item.Click += delegate
            {
                this.notify_icon.Dispose();
                Controller.QuitClicked();
            };


            this.context_menu.Items.Add(this.state_item);
            //this.context_menu.Items.Add (new Separator ());
            //this.context_menu.Items.Add (folder_item);

            if (Controller.Folders.Length > 0)
            {
                foreach (string folder_name in Controller.Folders)
                {
                    CmisMenuItem subfolder_item = new CmisMenuItem()
                    {
                        Header = folder_name
                    };

                    subfolder_item.Click += OpenFolderDelegate(folder_name);

                    // Open Subfolder
                    //MenuItem open_subfolder = new MenuItem() {
                    //    Header = "Open " + folder_name
                    //};
                    // open_subfolder.Click += OpenFolderDelegate(folder_name);

                    // Open remotefolder

                    Image subfolder_image = new Image()
                    {
                        Source = UIHelpers.GetImageSource("folder"),
                        Width = 16,
                        Height = 16
                    };

                    if (Program.Controller.UnsyncedFolders.Contains(folder_name))
                    {
                        subfolder_item.Icon = new Image()
                        {
                            Source = (BitmapSource)Imaging.CreateBitmapSourceFromHIcon(
                                System.Drawing.SystemIcons.Exclamation.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromWidthAndHeight(16, 16)
                            )
                        };

                    }
                    else
                    {
                        subfolder_item.Icon = subfolder_image;
                    }

                    /// subfolder_item.Items.Add(open_subfolder);
                    this.context_menu.Items.Add(subfolder_item);
                }

                MenuItem more_item = new MenuItem()
                {
                    Header = "More projects"
                };

                foreach (string folder_name in Controller.OverflowFolders)
                {
                    MenuItem subfolder_item = new MenuItem()
                    {
                        Header = folder_name
                    };

                    subfolder_item.Click += OpenFolderDelegate(folder_name);

                    Image subfolder_image = new Image()
                    {
                        Source = UIHelpers.GetImageSource("folder"),
                        Width = 16,
                        Height = 16
                    };

                    if (Program.Controller.UnsyncedFolders.Contains(folder_name))
                    {
                        subfolder_item.Icon = new Image()
                        {
                            Source = (BitmapSource)Imaging.CreateBitmapSourceFromHIcon(
                                System.Drawing.SystemIcons.Exclamation.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromWidthAndHeight(16, 16)
                            )
                        };

                    }
                    else
                    {
                        subfolder_item.Icon = subfolder_image;
                    }

                    more_item.Items.Add(subfolder_item);
                }

                if (more_item.Items.Count > 0)
                {
                    this.context_menu.Items.Add(new Separator());
                    this.context_menu.Items.Add(more_item);
                }

            }

            this.context_menu.Items.Add(new Separator());
            this.context_menu.Items.Add(add_item);
            this.context_menu.Items.Add(new Separator());
            //this.context_menu.Items.Add (notify_item);
            //this.context_menu.Items.Add (new Separator ());
            //this.context_menu.Items.Add (about_item);
            //this.context_menu.Items.Add (new Separator ());
            this.context_menu.Items.Add(this.exit_item);

            this.notify_icon.ContextMenu = this.context_menu;
        }


        public void ShowBalloon(string title, string subtext, string image_path)
        {
            this.notify_icon.ShowBalloonTip(title, subtext, image_path);
        }


        public void Dispose()
        {
            this.notify_icon.Dispose();
        }


        private void CreateAnimationFrames()
        {
            this.animation_frames = new Drawing.Bitmap[] {
	            UIHelpers.GetBitmap ("process-syncing-i"),
	            UIHelpers.GetBitmap ("process-syncing-ii"),
	            UIHelpers.GetBitmap ("process-syncing-iii"),
	            UIHelpers.GetBitmap ("process-syncing-iiii"),
	            UIHelpers.GetBitmap ("process-syncing-iiiii")
			};

            this.error_icon = UIHelpers.GetBitmap("process-syncing-error");
        }


        // A method reference that makes sure that opening the
        // event log for each repository works correctly
        private RoutedEventHandler OpenFolderDelegate(string folder_name)
        {
            return delegate
            {
                Controller.SubfolderClicked(folder_name);
            };
        }
    }


    public class CmisMenuItem : MenuItem
    {

        public CmisMenuItem()
            : base()
        {
            Padding = new Thickness(6, 3, 4, 0);
        }
    }
}
