using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Windows;
using System.Globalization;

namespace CmisSync
{
    /// <summary>
    /// CmisSync icon in the Windows status bar.
    /// </summary>
    public class StatusIcon : Form
    {
        /// <summary>
        /// MVC controller for the the status icon.
        /// </summary>
        public StatusIconController Controller = new StatusIconController();

        /// <summary>
        /// Context menu that appears when right-clicking on the CmisSync icon.
        /// </summary>
        private ContextMenuStrip traymenu = new ContextMenuStrip();

        /// <summary>
        /// Windows object for the status icon.
        /// </summary>
        private NotifyIcon trayicon = new NotifyIcon();

        /// <summary>
        /// Frames of the animation used when a download/upload is going on.
        /// The first frame is the static frame used when no activity is going on.
        /// </summary>
        private Icon[] animationFrames;

        /// <summary>
        /// Menu item that shows the state of CmisSync (up-to-date, etc).
        /// </summary>
        private ToolStripMenuItem stateItem;

        /// <summary>
        /// Menu item that allows the user to exit CmisSync.
        /// </summary>
        private ToolStripMenuItem exitItem;


        /// <summary>
        /// Constructor.
        /// </summary>
        public StatusIcon()
        {
            // Create the menu.
            CreateAnimationFrames();
            CreateMenu();

            // Setup the status icon.
            this.trayicon.Icon = animationFrames[0];
            this.trayicon.Text = "DataSpace Sync";
            this.trayicon.ContextMenuStrip = this.traymenu;
            this.trayicon.Visible = true;
        }


        /// <summary>
        /// When form is loaded, 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            // Set up the controller to create menu elements on update.
            CreateInvokeMethods();

            Visible = false; // Hide form window.
            ShowInTaskbar = false; // Remove from taskbar.
            base.OnLoad(e);
        }


        /// <summary>
        /// Set up the controller to create menu elements on update.
        /// </summary>
        private void CreateInvokeMethods()
        {
            // Icon.
            Controller.UpdateIconEvent += delegate(int icon_frame)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke((Action)delegate
                    {
                        if (icon_frame > -1)
                            this.trayicon.Icon = animationFrames[icon_frame];
                        else
                            this.trayicon.Icon = SystemIcons.Error;
                    });
                }
            };

            // Status item.
            Controller.UpdateStatusItemEvent += delegate(string state_text)
            {
                if (IsHandleCreated)
                {

                    BeginInvoke((Action)delegate
                    {
                        this.stateItem.Text = state_text;
                        this.trayicon.Text = "DataSpace Sync\n" + state_text;
                    });
                }
            };

            // Menu.
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

            // Repo Submenu.
            Controller.UpdateSuspendSyncFolderEvent += delegate(string reponame)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke((Action)delegate
                    {
                        ToolStripMenuItem repoitem = (ToolStripMenuItem)this.traymenu.Items["tsmi" + reponame];
                        ToolStripMenuItem syncitem = (ToolStripMenuItem)repoitem.DropDownItems[3];

                        if (syncitem.Tag == "pause")
                        {
                            syncitem.Text = CmisSync.Properties_Resources.ResourceManager.GetString("ResumeSync", CultureInfo.CurrentCulture);
                            syncitem.Tag = "resume";
                            syncitem.Image = UIHelpers.GetBitmap("media_playback_start");
                        }
                        else
                        {
                            syncitem.Text = CmisSync.Properties_Resources.ResourceManager.GetString("PauseSync", CultureInfo.CurrentCulture);
                            syncitem.Tag = "pause";
                            syncitem.Image = UIHelpers.GetBitmap("media_playback_pause");
                        }
                    });
                }

            };
        }


        /// <summary>
        /// Dispose of the status icon UI elements.
        /// </summary>
        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // Release the icon resource.
                this.trayicon.Dispose();
            }

            base.Dispose(isDisposing);
        }


        /// <summary>
        /// Create the UI elements of the menu.
        /// </summary>
        private void CreateMenu()
        {
            // Reset existing items.
            this.traymenu.Items.Clear();

            // Create the state menu item.
            this.stateItem = new ToolStripMenuItem()
            {
                Text = Controller.StateText,
                Enabled = false
            };
            this.traymenu.Items.Add(stateItem);
            this.trayicon.Text = "DataSpace Sync\n" + Controller.StateText;

            // Create a menu item per synchronized folder.
            if (Controller.Folders.Length > 0)
            {
                foreach (string folderName in Controller.Folders)
                {
                    // Main item.
                    ToolStripMenuItem subfolderItem = new ToolStripMenuItem()
                    {
                        Text = folderName,
                        Name = "tsmi" + folderName,
                        Image = UIHelpers.GetBitmap("folder")
                    };

                    // Sub-item: open locally.
                    ToolStripMenuItem openLocalFolderItem = new ToolStripMenuItem()
                    {
                        Text = CmisSync.Properties_Resources.ResourceManager.GetString("OpenLocalFolder", CultureInfo.CurrentCulture),
                        Image = UIHelpers.GetBitmap("folder")
                    };
                    openLocalFolderItem.Click += OpenLocalFolderDelegate(folderName);

                    // Sub-item: open remotely.
                    ToolStripMenuItem openRemoteFolderItem = new ToolStripMenuItem()
                    {
                        Text = CmisSync.Properties_Resources.ResourceManager.GetString("BrowseRemoteFolder", CultureInfo.CurrentCulture),
                        Image = UIHelpers.GetBitmap("classic_folder_web")
                    };
                    openRemoteFolderItem.Click += OpenRemoteFolderDelegate(folderName);

                    // Sub-item: suspend sync.
                    ToolStripMenuItem suspendFolderItem = new ToolStripMenuItem()
                    {
                        Text = CmisSync.Properties_Resources.ResourceManager.GetString("PauseSync", CultureInfo.CurrentCulture),
                        Tag="pause",
                        Image = UIHelpers.GetBitmap("media_playback_pause")
                    };
                    suspendFolderItem.Click += SuspendSyncFolderDelegate(folderName);

                    // Add the sub-items.
                    subfolderItem.DropDownItems.Add(openLocalFolderItem);
                    subfolderItem.DropDownItems.Add(openRemoteFolderItem);
                    subfolderItem.DropDownItems.Add(new ToolStripSeparator());
                    subfolderItem.DropDownItems.Add(suspendFolderItem);

                    // Add the main item.
                    this.traymenu.Items.Add(subfolderItem);
                }
            }
            this.traymenu.Items.Add(new ToolStripSeparator());

            // Create the menu item that lets the user add a new synchronized folder.
            ToolStripMenuItem addFolderItem = new ToolStripMenuItem()
            {
                Text = CmisSync.Properties_Resources.ResourceManager.GetString("AddARemoteFolder", CultureInfo.CurrentCulture)
            };
            addFolderItem.Click += delegate
            {
                Controller.AddRemoteFolderClicked();
            };
            this.traymenu.Items.Add(addFolderItem);
            this.traymenu.Items.Add(new ToolStripSeparator());

            // Create the menu item that lets the user view the log.
            ToolStripMenuItem log_item = new ToolStripMenuItem()
            {
                Text = CmisSync.Properties_Resources.ResourceManager.GetString("ViewLog", CultureInfo.CurrentCulture)
            };
            log_item.Click += delegate
            {
                Controller.LogClicked();
            };
            this.traymenu.Items.Add(log_item);

            // Create the About menu.
            ToolStripMenuItem about_item = new ToolStripMenuItem()
            {
                Text = CmisSync.Properties_Resources.ResourceManager.GetString("About", CultureInfo.CurrentCulture)
            };
            about_item.Click += delegate
            {
                Controller.AboutClicked();
            };
            this.traymenu.Items.Add(about_item);

            // Create the exit menu.
            this.exitItem = new ToolStripMenuItem()
            {
                Text = CmisSync.Properties_Resources.ResourceManager.GetString("Exit", CultureInfo.CurrentCulture)
            };
            this.exitItem.Click += delegate
            {
                this.trayicon.Dispose();
                Controller.QuitClicked();
            };
            this.traymenu.Items.Add(this.exitItem);
        }


        /// <summary>
        /// Create the animation frames from image files.
        /// </summary>
        private void CreateAnimationFrames()
        {
            this.animationFrames = new Icon[] {
	            UIHelpers.GetIcon ("process-syncing-i"),
	            UIHelpers.GetIcon ("process-syncing-ii"),
	            UIHelpers.GetIcon ("process-syncing-iii"),
	            UIHelpers.GetIcon ("process-syncing-iiii"),
	            UIHelpers.GetIcon ("process-syncing-iiiii")
			};
        }


        /// <summary>
        /// Delegate for opening the local folder.
        /// </summary>
        private EventHandler OpenLocalFolderDelegate(string reponame)
        {
            return delegate
            {
                Controller.LocalFolderClicked(reponame);
            };
        }


        /// <summary>
        /// Delegate for opening the remote folder.
        /// </summary>
        private EventHandler OpenRemoteFolderDelegate(string reponame)
        {
            return delegate
            {
                Controller.RemoteFolderClicked(reponame);
            };
        }


        /// <summary>
        /// Delegate for suspending sync.
        /// </summary>
        private EventHandler SuspendSyncFolderDelegate(string reponame)
        {
            return delegate
            {
                Controller.SuspendSyncClicked(reponame);
            };
        }
    }
}
