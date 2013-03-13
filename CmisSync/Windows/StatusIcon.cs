using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Windows;
using System.Globalization;

namespace CmisSync
{
    public class StatusIcon : Form
    {
        public StatusIconController Controller = new StatusIconController();
        private ContextMenuStrip traymenu = new ContextMenuStrip();
        private NotifyIcon trayicon = new NotifyIcon();
        private Icon[] animation_frames;
        private ToolStripMenuItem exit_item;
        private ToolStripMenuItem state_item;

        public StatusIcon()
        {
            CreateAnimationFrames();
            CreateMenu();

            this.trayicon.Icon = animation_frames[0];
            this.trayicon.Text = "CmisSync";
            this.trayicon.ContextMenuStrip = this.traymenu;
            this.trayicon.Visible = true;
        }

        protected override void OnLoad(EventArgs e)
        {
            CreateInvokeMethod();

            Visible = false; // Hide form window.
            ShowInTaskbar = false; // Remove from taskbar.
            base.OnLoad(e);
        }

        private void CreateInvokeMethod()
        {
            Controller.UpdateIconEvent += delegate(int icon_frame)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke((Action)delegate
                    {
                        if (icon_frame > -1)
                            this.trayicon.Icon = animation_frames[icon_frame];
                        else
                            this.trayicon.Icon = SystemIcons.Error;
                    });
                }
            };

            Controller.UpdateStatusItemEvent += delegate(string state_text)
            {
                if (IsHandleCreated)
                {

                    BeginInvoke((Action)delegate
                    {
                        this.state_item.Text = state_text;
                        this.trayicon.Text = "CmisSync\n" + state_text;
                    });
                }
            };

            Controller.UpdateMenuEvent += delegate(IconState state)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke((Action)delegate
                    {
                        CreateMenu();
                    });
                }
            };

            Controller.UpdateQuitItemEvent += delegate(bool item_enabled)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke((Action)delegate
                    {
                        this.exit_item.Enabled = item_enabled;
                        // this.exit_item.UpdateLayout();
                    });
                }
            };

            Controller.UpdateSuspendSyncFolderEvent += delegate(string reponame)
            {
                //TODO - Yannick
                if (IsHandleCreated)
                {
                    BeginInvoke((Action)delegate
                    {
                        ToolStripMenuItem repoitem = (ToolStripMenuItem)this.traymenu.Items["tsmi" + reponame];
                        ToolStripMenuItem syncitem = (ToolStripMenuItem)repoitem.DropDownItems[3];

                        if (syncitem.Tag == "pause")
                        {
                            syncitem.Text = "Resume sync!";
                            syncitem.Tag = "resume";
                        }
                        else
                        {
                            syncitem.Text = "Pause sync!";
                            syncitem.Tag = "pause";
                        }
                    });
                }

            };
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // Release the icon resource.
                this.trayicon.Dispose();
            }

            base.Dispose(isDisposing);
        }

        private void CreateMenu()
        {
            this.traymenu.Items.Clear();

            // State Menu
            this.state_item = new ToolStripMenuItem()
            {
                Text = Controller.StateText,
                Enabled = false
            };
            this.traymenu.Items.Add(state_item);

            this.trayicon.Text = "CmisSync\n" + Controller.StateText;

            // Folders Menu
            if (Controller.Folders.Length > 0)
            {
                foreach (string folder_name in Controller.Folders)
                {
                    ToolStripMenuItem subfolder_item = new ToolStripMenuItem()
                    {
                        Text = folder_name,
                        Name = "tsmi" + folder_name,
                        Image = UIHelpers.GetBitmap("folder")
                    };

                    ToolStripMenuItem open_localfolder_item = new ToolStripMenuItem()
                    {
                        Text = "Open local folder",
                        Image = UIHelpers.GetBitmap("folder")
                    };
                    open_localfolder_item.Click += OpenFolderDelegate(folder_name);

                    ToolStripMenuItem open_remotefolder_item = new ToolStripMenuItem()
                    {
                        Text = "Open remote folder"
                    };
                    open_remotefolder_item.Click += OpenRemoteFolderDelegate(folder_name);

                    ToolStripMenuItem suspend_folder_item = new ToolStripMenuItem()
                    {
                        Text = "Pause sync!",
                        Tag="pause"
                    };
                    suspend_folder_item.Click += SuspendSyncFolderDelegate(folder_name);

                    subfolder_item.DropDownItems.Add(open_localfolder_item);
                    subfolder_item.DropDownItems.Add(open_remotefolder_item);
                    subfolder_item.DropDownItems.Add(new ToolStripSeparator());
                    subfolder_item.DropDownItems.Add(suspend_folder_item);
                    this.traymenu.Items.Add(subfolder_item);
                }
            }
            this.traymenu.Items.Add(new ToolStripSeparator());

            // Add Menu
            ToolStripMenuItem add_item = new ToolStripMenuItem()
            {
                Text = CmisSync.Properties.Resources.ResourceManager.GetString("AddARemoteFolder", CultureInfo.CurrentCulture)
            };

            add_item.Click += delegate
            {
                Controller.AddHostedProjectClicked();
            };
            this.traymenu.Items.Add(add_item);
            this.traymenu.Items.Add(new ToolStripSeparator());

            // About Menu
            ToolStripMenuItem about_item = new ToolStripMenuItem()
            {
                Text = "About CmisSync"
            };
            about_item.Click += delegate
            {
                Controller.AboutClicked();
            };
            this.traymenu.Items.Add(about_item);

            // Exit Menu
            this.exit_item = new ToolStripMenuItem()
            {
                Text = CmisSync.Properties.Resources.ResourceManager.GetString("Exit", CultureInfo.CurrentCulture)
            };

            this.exit_item.Click += delegate
            {
                this.trayicon.Dispose();
                Controller.QuitClicked();
            };
            this.traymenu.Items.Add(this.exit_item);
        }

        private void CreateAnimationFrames()
        {
            this.animation_frames = new Icon[] {
	            UIHelpers.GetIcon ("process-syncing-i"),
	            UIHelpers.GetIcon ("process-syncing-ii"),
	            UIHelpers.GetIcon ("process-syncing-iii"),
	            UIHelpers.GetIcon ("process-syncing-iiii"),
	            UIHelpers.GetIcon ("process-syncing-iiiii")
			};
        }

        public void ShowBalloon(string title, string subtext, ToolTipIcon tticon)
        {
            this.trayicon.ShowBalloonTip(5000, title, subtext, tticon);
        }

        private EventHandler OpenFolderDelegate(string reponame)
        {
            return delegate
            {
                Controller.LocalFolderClicked(reponame);
            };
        }

        private EventHandler OpenRemoteFolderDelegate(string reponame)
        {
            return delegate
            {
                Controller.RemoteFolderClicked(reponame);
            };
        }

        private EventHandler SuspendSyncFolderDelegate(string reponame)
        {
            return delegate
            {
                Controller.SuspendSyncClicked(reponame);
            };
        }
    }
}
