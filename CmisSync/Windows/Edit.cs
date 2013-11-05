using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
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
                Name = Name,
                Id = credentials.RepoId
            };
            BackgroundWorker loader = CmisRepoUtils.LoadingSubfolderAsync(repo, credentials);

            List<CmisSync.CmisTree.RootFolder> repos = new List<CmisSync.CmisTree.RootFolder>();
            repos.Add(repo);
            repo.Selected = true;

            treeView.DataContext = repos;

            ContentCanvas.Children.Add(treeView);
            Canvas.SetTop(treeView, 70);
            Canvas.SetLeft(treeView, 185);

            Controller.CloseWindowEvent += delegate
            {
                loader.CancelAsync();
            };


            Button finish_button = new Button()
            {
                Content = Properties_Resources.Finish,
                IsDefault = true
            };

            Button cancel_button = new Button()
            {
                Content = Properties_Resources.Cancel,
                IsDefault = false
            };

            Buttons.Add(finish_button);
            Buttons.Add(cancel_button);

            finish_button.Focus();

            finish_button.Click += delegate
            {
                Ignores = CmisRepoUtils.GetIgnoredFolder(repo);
                Controller.SaveFolder();
                Close();
            };

            cancel_button.Click += delegate
            {
                Close();
            };

            this.ShowAll();
        }
    }
}
