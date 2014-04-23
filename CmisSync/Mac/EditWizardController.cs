using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using MonoMac.Foundation;
using MonoMac.AppKit;

using CmisSync.Lib.Cmis;
using CmisSync.Lib.Credentials;
using CmisSync.CmisTree;

namespace CmisSync
{
    public partial class EditWizardController : MonoMac.AppKit.NSWindowController
    {

        #region Constructors

        // Called when created from unmanaged code
        public EditWizardController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public EditWizardController (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Call to load from the XIB/NIB file
        public EditWizardController (CmisRepoCredentials credentials, string name, string remotePath, List<string> ignores, string localPath) : base ("EditWizard")
        {
            FolderName = name;
            this.credentials = credentials;
            this.remotePath = remotePath;
            this.Ignores = new List<string>(ignores);
            this.localPath = localPath;

            Initialize ();

            Controller.OpenWindowEvent += () =>
            {
                InvokeOnMainThread (() =>
                {
                    this.Window.OrderFrontRegardless ();
                });
            };
        }
        // Shared initialization code
        void Initialize ()
        {
        }

        #endregion

        protected override void Dispose (bool disposing)
        {
            base.Dispose (disposing);
            Console.WriteLine (this.GetType ().ToString () + " disposed " + disposing.ToString ());
        }

        public EditController Controller = new EditController();

        public string FolderName;
        public List<string> Ignores;

        private CmisRepoCredentials credentials;
        private string remotePath;
        private string localPath;

        RootFolder Repo;
        private CmisTreeDataSource DataSource;
        private OutlineViewDelegate DataDelegate;
        private AsyncNodeLoader Loader;

        public override void AwakeFromNib ()
        {
            base.AwakeFromNib ();

            this.SideSplashView.Image = new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "side-splash.png")) {
                Size = new SizeF (150, 482)
            };

            this.Header.StringValue = Properties_Resources.EditTitle;

            Repo = new RootFolder()
            {
                Name = FolderName,
                Id = credentials.RepoId,
                Address = credentials.Address.ToString()
            };
            Repo.Selected = true;
            IgnoredFolderLoader.AddIgnoredFolderToRootNode(Repo, Ignores);
            LocalFolderLoader.AddLocalFolderToRootNode(Repo, localPath);
            List<RootFolder> repos = new List<RootFolder>();
            repos.Add(Repo);

            Loader = new AsyncNodeLoader(Repo, credentials, PredefinedNodeLoader.LoadSubFolderDelegate, PredefinedNodeLoader.CheckSubFolderDelegate);

            CancelButton.Title = Properties_Resources.DiscardChanges;
            FinishButton.Title = Properties_Resources.SaveChanges;

            DataDelegate = new OutlineViewDelegate ();
            DataSource = new CmisTree.CmisTreeDataSource(repos);
            Outline.DataSource = DataSource;
            Outline.Delegate = DataDelegate;

            Controller.CloseWindowEvent += delegate
            {
                Loader.Cancel();
                this.Window.PerformClose (this);
                this.Dispose();
            };

            InsertEvent ();

            //  must be called after InsertEvent()
            Loader.Load(Repo);
        }

        void InsertEvent ()
        {
            DataSource.SelectedEvent += OutlineSelected;
            DataDelegate.ItemExpanded += OutlineItemExpanded;
            Loader.UpdateNodeEvent += AsyncNodeUpdate;
        }

        void RemoveEvent ()
        {
            DataSource.SelectedEvent -= OutlineSelected;
            DataDelegate.ItemExpanded -= OutlineItemExpanded;
            Loader.UpdateNodeEvent -= AsyncNodeUpdate;
        }

        void AsyncNodeUpdate ()
        {
            InvokeOnMainThread (delegate
            {
                DataSource.UpdateCmisTree (Repo);
                for (int i = 0; i < Outline.RowCount; ++i) {
                    Outline.ReloadItem (Outline.ItemAtRow (i));
                }
            });
        }

        void OutlineItemExpanded (NSNotification notification)
        {
            InvokeOnMainThread (delegate
            {
                NSCmisTree cmis = notification.UserInfo ["NSObject"] as NSCmisTree;
                if (cmis == null) {
                    Console.WriteLine ("ItemExpanded Error");
                    return;
                }

                NSCmisTree cmisRoot = cmis;
                while (cmisRoot.Parent != null) {
                    cmisRoot = cmisRoot.Parent;
                }
                if (Repo.Name != cmisRoot.Name) {
                    Console.WriteLine ("ItemExpanded find root Error");
                    return;
                }

                Node node = cmis.GetNode (Repo);
                if (node == null) {
                    Console.WriteLine ("ItemExpanded find node Error");
                    return;
                }
                Loader.Load (node);
            });
        }

        void OutlineSelected (NSCmisTree cmis, int selected)
        {
            InvokeOnMainThread (delegate
            {
                Node node = cmis.GetNode (Repo);
                if (node == null) {
                    Console.WriteLine ("SelectedEvent find node Error");
                }
                node.Selected = (selected != 0);
                DataSource.UpdateCmisTree (Repo);

                for (int i = 0; i < Outline.RowCount; ++i) {
                    try {
                        Outline.ReloadItem (Outline.ItemAtRow (i));
                    } catch (Exception e) {
                        Console.WriteLine (e);
                    }
                }
            });
        }

        partial void OnCancel (MonoMac.Foundation.NSObject sender)
        {
            Loader.Cancel ();
            RemoveEvent ();
            Controller.CloseWindow ();
        }

        partial void OnFinish (MonoMac.Foundation.NSObject sender)
        {
            Loader.Cancel ();
            RemoveEvent ();
            Ignores = NodeModelUtils.GetIgnoredFolder (Repo);
            Controller.SaveFolder ();
            Controller.CloseWindow ();
        }

        //strongly typed window accessor
        public new EditWizard Window {
            get {
                return (EditWizard)base.Window;
            }
        }
    }
}

