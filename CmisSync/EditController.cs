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
        /// Show Edit Window Action
        /// </summary>
        public event Action ShowWindowEvent = delegate { };
        /// <summary>
        /// Save Folder Action
        /// </summary>
        public event Action SaveFolderEvent = delegate { };
        /// <summary>
        /// Hide Edit Window Action
        /// </summary>
        public event Action HideWindowEvent = delegate { };

        /// <summary>
        /// Show Edit Window
        /// </summary>
        public void ShowWindow()
        {
            ShowWindowEvent();
        }

        /// <summary>
        /// Save Folder
        /// </summary>
        public void SaveFolder()
        {
            SaveFolderEvent();
        }

        /// <summary>
        /// Hide Window
        /// </summary>
        public void HideWindow()
        {
            HideWindowEvent();
        }
    }
}
