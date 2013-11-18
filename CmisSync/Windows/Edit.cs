using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using CmisSync.Lib.Credentials;
using CmisSync.CmisTree;
using System.Collections.ObjectModel;

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


        protected override void Close(object sender, CancelEventArgs args)
        {
            Controller.CloseWindow();
        }


        /// <summary>
        /// Create the UI
        /// </summary>
        private void CreateEdit()
        {
            System.Uri resourceLocater = new System.Uri("/DataSpaceSync;component/FolderTreeMVC/TreeView.xaml", System.UriKind.Relative);
            TreeView treeView = Application.LoadComponent(resourceLocater) as TreeView;

            CmisSync.CmisTree.RootFolder repo = new CmisSync.CmisTree.RootFolder()
            {
                Name = FolderName,
                Id = credentials.RepoId,
                Address = credentials.Address.ToString()
            };
            AsyncNodeLoader asyncLoader = new AsyncNodeLoader(repo, credentials, PredefinedNodeLoader.LoadSubFolderDelegate);
            IgnoredFolderLoader.AddIgnoredFolderToRootNode(repo, Ignores);
            LocalFolderLoader.AddLocalFolderToRootNode(repo, localPath);

            asyncLoader.Load(repo);

            ObservableCollection<RootFolder> repos = new ObservableCollection<RootFolder>();
            repos.Add(repo);
            repo.Selected = true;

            treeView.DataContext = repos;

            treeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(delegate(object sender, RoutedEventArgs e)
            {
                TreeViewItem expandedItem = e.OriginalSource as TreeViewItem;
                Node expandedNode = expandedItem.Header as Folder;
                if (expandedNode != null)
                {
                    asyncLoader.Load(expandedNode);
                }
            }));


            ContentCanvas.Children.Add(treeView);
            Canvas.SetTop(treeView, 70);
            Canvas.SetLeft(treeView, 185);

            Controller.CloseWindowEvent += delegate
            {
                asyncLoader.Cancel();
            };


            Button finish_button = new Button()
            {
                Content = Properties_Resources.SaveChanges,
                IsDefault = true
            };

            Button cancel_button = new Button()
            {
                Content = Properties_Resources.DiscardChanges,
                IsDefault = false
            };

            Buttons.Add(finish_button);
            Buttons.Add(cancel_button);

            finish_button.Focus();

            finish_button.Click += delegate
            {
                Ignores = NodeModelUtils.GetIgnoredFolder(repo);
                Controller.SaveFolder();
                Close();
            };

            cancel_button.Click += delegate
            {
                Close();
            };
            this.Title = Properties_Resources.EditTitle;
            this.ShowAll();
        }
    }
}
