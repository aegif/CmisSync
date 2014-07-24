using System;
using System.Collections.Generic;
using System.ComponentModel;

using Gtk;

using CmisSync.Auth;
using CmisSync.CmisTree;
using DotCMIS.Exceptions;
namespace CmisSync
{
    /// <summary>
    /// Edit folder diaglog
    /// It allows user to edit the selected and ignored folders
    /// </summary>
    public class Edit : SetupWindow
    {
        /// <summary>
        /// Controller
        /// </summary>
        public EditController Controller = new EditController();


        /// <summary>
        /// Synchronized folder name
        /// </summary>
        public string Name;

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
            Name = name;
            this.credentials = credentials;
            this.remotePath = remotePath;
            this.Ignores = ignores;
            this.localPath = localPath;

            CreateEdit();

            Deletable      = true;

            DeleteEvent += delegate (object sender, DeleteEventArgs args) {
                args.RetVal = false;
                Controller.CloseWindow();
            };

            Controller.OpenWindowEvent += delegate
            {
                ShowAll ();
                Activate ();
            };
        }


        /// <summary>
        /// Create the UI
        /// </summary>
        private void CreateEdit()
        {
            CmisTreeStore cmisStore = new CmisTreeStore ();
            Gtk.TreeView treeView = new Gtk.TreeView (cmisStore.CmisStore);

            RootFolder root = new RootFolder () {
                Name = this.Name,
                Id = credentials.RepoId,
                Address = credentials.Address.ToString()
            };
            IgnoredFolderLoader.AddIgnoredFolderToRootNode(root, Ignores);
            LocalFolderLoader.AddLocalFolderToRootNode(root, localPath);

            AsyncNodeLoader asyncLoader = new AsyncNodeLoader (root, credentials, PredefinedNodeLoader.LoadSubFolderDelegate, PredefinedNodeLoader.CheckSubFolderDelegate);
            asyncLoader.UpdateNodeEvent += delegate {
                cmisStore.UpdateCmisTree(root);
            };
            cmisStore.UpdateCmisTree (root);
            asyncLoader.Load (root);

            Header = CmisSync.Properties_Resources.EditTitle;

            VBox layout_vertical   = new VBox (false, 12);

            Controller.CloseWindowEvent += delegate
            {
                asyncLoader.Cancel();
            };

            Button cancel_button = new Button (CmisSync.Properties_Resources.Cancel);
            cancel_button.Clicked += delegate {
                Close();
            };

            Button finish_button = new Button (CmisSync.Properties_Resources.SaveChanges);
            finish_button.Clicked += delegate {
                Ignores = NodeModelUtils.GetIgnoredFolder(root);
                Controller.SaveFolder();
                Close();
            };

            Gtk.TreeIter iter;
            treeView.HeadersVisible = false;
            treeView.Selection.Mode = SelectionMode.Single;

            TreeViewColumn column = new TreeViewColumn ();
            column.Title = "Name";
            CellRendererToggle renderToggle = new CellRendererToggle ();
            column.PackStart (renderToggle, false);
            renderToggle.Activatable = true;
            column.AddAttribute (renderToggle, "active", (int)CmisTreeStore.Column.ColumnSelected);
            column.AddAttribute (renderToggle, "inconsistent", (int)CmisTreeStore.Column.ColumnSelectedThreeState);
            column.AddAttribute (renderToggle, "radio", (int)CmisTreeStore.Column.ColumnRoot);
            renderToggle.Toggled += delegate (object render, ToggledArgs args) {
                TreeIter iterToggled;
                if (! cmisStore.CmisStore.GetIterFromString (out iterToggled, args.Path))
                {
                    Console.WriteLine("Toggled GetIter Error " + args.Path);
                    return;
                }

                Node node = cmisStore.CmisStore.GetValue(iterToggled,(int)CmisTreeStore.Column.ColumnNode) as Node;
                if (node == null)
                {
                    Console.WriteLine("Toggled GetValue Error " + args.Path);
                    return;
                }

                if (node.Parent == null)
                {
                    node.Selected = true;
                }
                else
                {
                    if (node.Selected == false)
                    {
                        node.Selected = true;
                    }
                    else
                    {
                        node.Selected = false;
                    }
                }
                cmisStore.UpdateCmisTree(root);
            };
            CellRendererText renderText = new CellRendererText ();
            column.PackStart (renderText, false);
            column.SetAttributes (renderText, "text", (int)CmisTreeStore.Column.ColumnName);
            column.Expand = true;
            treeView.AppendColumn (column);

            treeView.AppendColumn ("Status", new StatusCellRenderer (), "text", (int)CmisTreeStore.Column.ColumnStatus);

            treeView.RowExpanded += delegate (object o, RowExpandedArgs args) {
                Node node = cmisStore.CmisStore.GetValue(args.Iter, (int)CmisTreeStore.Column.ColumnNode) as Node;
                asyncLoader.Load(node);
            };

            ScrolledWindow sw = new ScrolledWindow() {
                ShadowType = Gtk.ShadowType.In
            };
            sw.Add(treeView);

            layout_vertical.PackStart (new Label(""), false, false, 0);
            layout_vertical.PackStart (sw, true, true, 0);
            Add(layout_vertical);
            AddButton(cancel_button);
            AddButton(finish_button);

            finish_button.GrabDefault ();

            this.ShowAll();
        }

        /// <summary>
        /// Close the UI
        /// </summary>
        public void Close()
        {
            Controller.CloseWindow();
            this.Destroy();
        }

        /// <summary>
        /// Gets a value indicating whether this window is visible.
        /// TODO Should be implemented with the correct Windows property,
        /// at the moment, it always returns false
        /// </summary>
        /// <value>
        /// <c>true</c> if this window is visible; otherwise, <c>false</c>.
        /// </value>
        public bool IsVisible {
            get {
                // TODO Please change it to the correct Window property if this method is needed
                return false;
            }
            private set{}
        }
    }
}
