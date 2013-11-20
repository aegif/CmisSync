using System;
using System.Collections.Generic;
using System.ComponentModel;
using CmisSync.Lib.Credentials;

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

            DeleteEvent += delegate
            {
                Controller.CloseWindow();
            };
        }


        /// <summary>
        /// Create the UI
        /// </summary>
        private void CreateEdit()
        {
            this.ShowAll();
        }

        /// <summary>
        /// Close the UI
        /// </summary>
        public void Close()
        {
            this.Destroy();
        }
    }
}
