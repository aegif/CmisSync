using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Globalization;

using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;
using MonoMac.WebKit;

using Mono.Unix;

using CmisSync.Lib.Cmis;
using CmisSync.Lib.Credentials;
using CmisSync.CmisTree;

namespace CmisSync
{
    /// <summary>
    /// Edit folder diaglog
    /// It allows user to edit the selected and ignored folders
    /// </summary>
    class Edit : SetupWindow
    {
        /// <summary>
        /// Controller
        /// </summary>
        public EditController Controller = new EditController();


        /// <summary>
        /// Synchronized folder name
        /// </summary>
        public string FolderName;

        /// <summary>
        /// Ignore folder list
        /// </summary>
        public List<string> Ignores;

        private CmisRepoCredentials credentials;
        private string remotePath;
        private string localPath;


        /// <summary>
        /// Constructor
        /// </summary>
        public Edit(CmisRepoCredentials credentials, string name, string remotePath, List<string> ignores, string localPath)
        {
            FolderName = name;
            this.credentials = credentials;
            this.remotePath = remotePath;
            this.Ignores = new List<string>(ignores);
            this.localPath = localPath;

            CreateEdit();
        }


        /// <summary>
        /// Create the UI
        /// </summary>
        private void CreateEdit()
        {
            NSOutlineView outlineView = new NSOutlineView () {
                Frame            = new RectangleF (0, 0, 0, 0),
                RowHeight        = 34,
                IntercellSpacing = new SizeF (8, 12),
                HeaderView       = null,
                Delegate         = new OutlineViewDelegate ()
            };

            outlineView.AddColumn(new NSTableColumn(){
                Identifier = "colName",
                Editable = false
            });

            NSScrollView ScrollView = new NSScrollView () {
                Frame               = new RectangleF (190, Frame.Height - 340, 408, 255),
                DocumentView        = outlineView,
                HasVerticalScroller = true,
                BorderType          = NSBorderType.BezelBorder
            };

            RootFolder repo = new RootFolder()
            {
                Name = FolderName,
                Id = credentials.RepoId,
                Address = credentials.Address.ToString()
            };
            //AsyncNodeLoader asyncLoader = new AsyncNodeLoader(repo, credentials, PredefinedNodeLoader.LoadSubFolderDelegate);
            IgnoredFolderLoader.AddIgnoredFolderToRootNode(repo, Ignores);
            LocalFolderLoader.AddLocalFolderToRootNode(repo, localPath);
            repo.Selected = true;
            //asyncLoader.Load(repo);
            List<RootFolder> repos = new List<RootFolder>();
            repos.Add(repo);
            CmisTree.CmisTreeDataSource DataSource = new CmisTree.CmisTreeDataSource(repos);
            outlineView.DataSource = DataSource;
            outlineView.ReloadData ();

            // Add selection handler
            /*treeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(delegate(object sender, RoutedEventArgs e)
            {
                TreeViewItem expandedItem = e.OriginalSource as TreeViewItem;
                Node expandedNode = expandedItem.Header as Folder;
                if (expandedNode != null)
                {
                    asyncLoader.Load(expandedNode);
                }
            }));*/

            ContentView.AddSubview(ScrollView);

            Controller.CloseWindowEvent += delegate
            {
                //asyncLoader.Cancel();
            };

            Controller.OpenWindowEvent += delegate
            {
                InvokeOnMainThread (delegate {
                    OrderFrontRegardless ();
                });
            };


            NSButton finish_button = new NSButton()
            {
                Title = Properties_Resources.SaveChanges
            };

            NSButton cancel_button = new NSButton()
            {
                Title = Properties_Resources.DiscardChanges
            };

            Buttons.Add(finish_button);
            Buttons.Add(cancel_button);

            finish_button.Activated += delegate
            {
                Ignores = NodeModelUtils.GetIgnoredFolder(repo);
                Controller.SaveFolder();
                InvokeOnMainThread (delegate {
                    PerformClose (this);
                });
            };

            cancel_button.Activated += delegate
            {
                InvokeOnMainThread (delegate {
                    PerformClose (this);
                });
            };


            (outlineView.Delegate as OutlineViewDelegate).SelectionChanged += delegate {
                Node selectedNode = ((NSNodeObject)outlineView.ItemAtRow(outlineView.SelectedRow)).Node;
                while(selectedNode.Parent != null)
                    selectedNode = selectedNode.Parent;
                RootFolder root = selectedNode as RootFolder;
                if(root != null){
                    finish_button.Enabled = true;
                }
            };

            this.Header = Properties_Resources.EditTitle;
            this.Description = "";
            this.ShowAll();
        }
    }
}
