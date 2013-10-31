using System;
using System.Collections.Generic;
using System.ComponentModel;

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

        private string username;
        private string password;
        private string address;
        private string id;
        private string remotePath;
        private string localPath;


        /// <summary>
        /// Constructor
        /// </summary>
        public Edit(string name, string username, string password, string address, string id, string remotePath, List<string> ignores, string localPath)
        {
            Name = name;
            this.username = username;
            this.password = password;
            this.address = address;
            this.id = id;
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
