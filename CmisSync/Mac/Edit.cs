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

        private CmisOutlineController OutlineController;

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
            RootFolder repo = new RootFolder()
            {
                Name = FolderName,
                Id = credentials.RepoId,
                Address = credentials.Address.ToString()
            };
            repo.Selected = true;
            IgnoredFolderLoader.AddIgnoredFolderToRootNode(repo, Ignores);
            LocalFolderLoader.AddLocalFolderToRootNode(repo, localPath);
            List<RootFolder> repos = new List<RootFolder>();
            repos.Add(repo);

            OutlineViewDelegate DataDelegate = new OutlineViewDelegate ();
            CmisTree.CmisTreeDataSource DataSource = new CmisTree.CmisTreeDataSource(repos);

            CmisOutlineController OutlineController = new CmisOutlineController (DataSource, DataDelegate);

            AsyncNodeLoader asyncLoader = new AsyncNodeLoader(repo, credentials, PredefinedNodeLoader.LoadSubFolderDelegate, PredefinedNodeLoader.CheckSubFolderDelegate);
            asyncLoader.UpdateNodeEvent += delegate {
                InvokeOnMainThread(delegate {
                    DataSource.UpdateCmisTree(repo);
                    NSOutlineView view = OutlineController.OutlineView();
                    for (int i = 0; i < view.RowCount; ++i) {
                        view.ReloadItem(view.ItemAtRow(i));
                    }
                });
            };
            asyncLoader.Load(repo);

            DataDelegate.ItemExpanded += delegate(NSNotification notification)
            {
                InvokeOnMainThread(delegate {
                    NSCmisTree cmis = notification.UserInfo["NSObject"] as NSCmisTree;
                    if (cmis == null) {
                        Console.WriteLine("ItemExpanded Error");
                        return;
                    }

                    NSCmisTree cmisRoot = cmis;
                    while (cmisRoot.Parent != null) {
                        cmisRoot = cmisRoot.Parent;
                    }
                    if (repo.Name != cmisRoot.Name) {
                        Console.WriteLine("ItemExpanded find root Error");
                        return;
                    }

                    Node node = cmis.GetNode(repo);
                    if (node == null) {
                        Console.WriteLine("ItemExpanded find node Error");
                        return;
                    }
                    asyncLoader.Load(node);
                });
            };
            DataSource.SelectedEvent += delegate (NSCmisTree cmis, int selected) {
                InvokeOnMainThread(delegate {
                    Node node = cmis.GetNode(repo);
                    if (node == null) {
                        Console.WriteLine("SelectedEvent find node Error");
                    }
                    node.Selected = (selected != 0);
                    DataSource.UpdateCmisTree(repo);

                    NSOutlineView view = OutlineController.OutlineView();
                    for (int i = 0; i < view.RowCount; ++i) {
                        try{
                            view.ReloadItem(view.ItemAtRow(i));
                        }catch(Exception e) {
                            Console.WriteLine(e);
                        }
                    }
                });
            };

            OutlineController.View.Frame = new RectangleF (190, 60, 400, 240);
            ContentView.AddSubview (OutlineController.View);

            Controller.CloseWindowEvent += delegate
            {
                asyncLoader.Cancel();
            };

            Controller.OpenWindowEvent += delegate
            {
                InvokeOnMainThread (delegate {
                    OrderFrontRegardless ();
                });
            };


            NSButton finish_button = new NSButton()
            {
                Title = Properties_Resources.SaveChanges,
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

            this.Header = Properties_Resources.EditTitle;
            this.Description = "";
            this.ShowAll();
        }

        public override void OrderFrontRegardless ()
        {
            NSApplication.SharedApplication.ActivateIgnoringOtherApps (true);
            MakeKeyAndOrderFront (this);

            if (Program.UI != null)
                Program.UI.UpdateDockIconVisibility ();

            base.OrderFrontRegardless ();
        }


        public override void PerformClose (NSObject sender)
        {
            base.OrderOut (this);

            if (Program.UI != null)
                Program.UI.UpdateDockIconVisibility ();

            Controller.CloseWindow ();

            return;
        }
    }
}
