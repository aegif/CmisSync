using System;
using System.Collections.Generic;
using System.Linq;
using MonoMac.Foundation;
using MonoMac.AppKit;

using CmisSync.Lib.Cmis;
using CmisSync.Lib.Credentials;
using CmisSync.CmisTree;

namespace CmisSync
{
    public partial class SetupSubRepoSelectController : MonoMac.AppKit.NSViewController
    {

        #region Constructors

        // Called when created from unmanaged code
        public SetupSubRepoSelectController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public SetupSubRepoSelectController (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Call to load from the XIB/NIB file
        public SetupSubRepoSelectController (SetupController controller) : base ("SetupSubRepoSelect", NSBundle.MainBundle)
        {
            this.Controller = controller;
            Initialize ();
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

        private List<RootFolder> Repositories;
        private CmisTreeDataSource DataSource;
        private OutlineViewDelegate DataDelegate;
        Dictionary<string,AsyncNodeLoader> Loader;

        public override void AwakeFromNib ()
        {
            base.AwakeFromNib ();

            bool firstRepo = true;
            Repositories = new List<RootFolder>();
            Loader = new Dictionary<string,AsyncNodeLoader> ();
            foreach (KeyValuePair<String, String> repository in Controller.repositories)
            {
                RootFolder repo = new RootFolder() {
                    Name = repository.Value,
                    Id = repository.Key,
                    Address = Controller.saved_address.ToString()
                };
                Repositories.Add(repo);
                if (firstRepo)
                {
                    repo.Selected = true;
                    firstRepo = false;
                }
                else
                {
                    repo.Selected = false;
                }
                CmisRepoCredentials cred = new CmisRepoCredentials()
                {
                    UserName = Controller.saved_user,
                    Password = Controller.saved_password,
                    Address = Controller.saved_address,
                    RepoId = repository.Key
                };
                AsyncNodeLoader asyncLoader = new AsyncNodeLoader(repo, cred, PredefinedNodeLoader.LoadSubFolderDelegate, PredefinedNodeLoader.CheckSubFolderDelegate);
                Loader.Add(repo.Id, asyncLoader);
            }

            DataSource = new CmisTree.CmisTreeDataSource(Repositories);
            DataDelegate = new OutlineViewDelegate ();
            Outline.DataSource = DataSource;
            Outline.Delegate = DataDelegate;

            ContinueButton.Enabled = (Repositories.Count > 0);
//            ContinueButton.KeyEquivalent = "\r";

            this.BackButton.Title = Properties_Resources.Back;
            this.CancelButton.Title = Properties_Resources.Cancel;
            this.ContinueButton.Title = Properties_Resources.Continue;

            InsertEvent ();

            //  must be called after InsertEvent()
            foreach (RootFolder repo in Repositories) {
                Loader [repo.Id].Load (repo);
            }
        }

        SetupController Controller;

        void OutlineUpdate()
        {
            InvokeOnMainThread (delegate
            {
                foreach (RootFolder root in Repositories) {
                    DataSource.UpdateCmisTree (root);
                }
                for (int i = 0; i < Outline.RowCount; ++i) {
                    Outline.ReloadItem (Outline.ItemAtRow (i));
                }
            });
        }

        void OutlineSelected (NSCmisTree cmis, int selected)
        {
            InvokeOnMainThread (delegate
            {
                RootFolder selectedRoot = null;
                foreach (RootFolder root in Repositories) {
                    Node node = cmis.GetNode (root);
                    if (node != null) {
                        if (node.Parent == null && node.Selected == false) {
                            selectedRoot = root;
                        }
                        node.Selected = (selected != 0);
                        DataSource.UpdateCmisTree (root);
                    }
                }

                if (selectedRoot != null) {
                    foreach (RootFolder root in Repositories) {
                        if (root != selectedRoot) {
                            root.Selected = false;
                            DataSource.UpdateCmisTree (root);
                        }
                    }
                    Outline.ReloadData ();
                } else {
                    for (int i = 0; i < Outline.RowCount; ++i) {
                        Outline.ReloadItem (Outline.ItemAtRow (i));
                    }
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
                RootFolder root = Repositories.Find (x => x.Name.Equals (cmisRoot.Name));
                if (root == null) {
                    Console.WriteLine ("ItemExpanded find root Error");
                    return;
                }

                Node node = cmis.GetNode (root);
                if (node == null) {
                    Console.WriteLine ("ItemExpanded find node Error");
                    return;
                }
                Loader [root.Id].Load (node);
            });
        }

        void InsertEvent ()
        {
            DataSource.SelectedEvent += OutlineSelected;
            DataDelegate.ItemExpanded += OutlineItemExpanded;
            foreach (AsyncNodeLoader task in Loader.Values)
                task.UpdateNodeEvent += OutlineUpdate;
        }

        void RemoveEvent ()
        {
            DataSource.SelectedEvent -= OutlineSelected;
            DataDelegate.ItemExpanded -= OutlineItemExpanded;
            foreach (AsyncNodeLoader task in Loader.Values)
                task.UpdateNodeEvent -= OutlineUpdate;
        }

        partial void OnBack (MonoMac.Foundation.NSObject sender)
        {
            RemoveEvent();
            foreach (AsyncNodeLoader task in Loader.Values)
                task.Cancel();
            Controller.BackToPage1();
        }

        partial void OnCancel (MonoMac.Foundation.NSObject sender)
        {
            RemoveEvent();
            foreach (AsyncNodeLoader task in Loader.Values)
                task.Cancel();
            Controller.PageCancelled();
        }

        partial void OnContinue (MonoMac.Foundation.NSObject sender)
        {
            RootFolder root = Repositories.Find(x=>(x.Selected != false));
            if (root != null)
            {
                RemoveEvent();
                foreach (AsyncNodeLoader task in Loader.Values)
                    task.Cancel();
                Controller.saved_repository = root.Id;
                List<string> ignored = NodeModelUtils.GetIgnoredFolder(root);
                List<string> selected = NodeModelUtils.GetSelectedFolder(root);
                Controller.Add2PageCompleted(root.Id, root.Path, ignored.ToArray(), selected.ToArray());
            }
        }

        //strongly typed view accessor
        public new SetupSubRepoSelect View {
            get {
                return (SetupSubRepoSelect)base.View;
            }
        }
    }
}

