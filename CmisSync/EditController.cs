using System;

namespace CmisSync
{
    /// <summary>
    /// Controller for the Edit diaglog.
    /// </summary>
    public class EditController
    {
        //===== Actions =====
        /// <summary>
        /// Open Edit Window Action
        /// </summary>
        public event Action OpenWindowEvent = delegate { };
        /// <summary>
        /// Save Folder Action
        /// </summary>
        public event Action SaveFolderEvent = delegate { };
        /// <summary>
        /// Close Edit Window Action
        /// </summary>
        public event Action CloseWindowEvent = delegate { };

        /// <summary>
        /// Show Edit Window
        /// </summary>
        public void OpenWindow()
        {
            OpenWindowEvent();
        }

        /// <summary>
        /// Save Folder
        /// </summary>
        public void SaveFolder()
        {
            SaveFolderEvent();
        }

        /// <summary>
        /// Close Edit Window
        /// </summary>
        public void CloseWindow()
        {
            CloseWindowEvent();
        }
    }
}
