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
			NSOutlineView outlineView = new NSOutlineView();

            RootFolder repo = new RootFolder()
            {
                Name = FolderName,
                Id = credentials.RepoId,
                Address = credentials.Address.ToString()
            };
            AsyncNodeLoader asyncLoader = new AsyncNodeLoader(repo, credentials, PredefinedNodeLoader.LoadSubFolderDelegate);
            IgnoredFolderLoader.AddIgnoredFolderToRootNode(repo, Ignores);
            LocalFolderLoader.AddLocalFolderToRootNode(repo, localPath);

            asyncLoader.Load(repo);

			/*
            ObservableCollection<RootFolder> repos = new ObservableCollection<RootFolder>();
            repos.Add(repo);
            repo.Selected = true;

			outlineView.DataSource = repos;*/
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


			ContentView.AddSubview(outlineView);

            Controller.CloseWindowEvent += delegate
            {
                asyncLoader.Cancel();
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
                Close();
            };

			cancel_button.Activated += delegate
            {
                Close();
            };

            this.Title = Properties_Resources.EditTitle;
            this.ShowAll();
        }
    }
}
