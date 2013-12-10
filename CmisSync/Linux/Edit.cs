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
            } private set{}
        }
    }
}
