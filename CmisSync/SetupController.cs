//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;
using log4net;

namespace CmisSync
{

    /// <summary>
    /// Kind of pages that are used in the folder addition wizards.
    /// </summary>
    public enum PageType
    {
        None,
        Setup,
        Add1,
        Add2,
        Customize,
        Syncing,
        Finished,
        Tutorial // This particular one contains sub-steps that are tracked via a number.
    }

    /// <summary>
    /// MVC controller for the two wizards:
    /// - CmisSync tutorial that appears at firt run,
    /// - wizard to add a new remote folder.
    /// </summary>
    public class SetupController
    {
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(SetupController));

        // Delegates.

        public event Action ShowWindowEvent = delegate { };
        public event Action HideWindowEvent = delegate { };

        public event ChangePageEventHandler ChangePageEvent = delegate { };
        public delegate void ChangePageEventHandler(PageType page);

        public event UpdateProgressBarEventHandler UpdateProgressBarEvent = delegate { };
        public delegate void UpdateProgressBarEventHandler(double percentage);

        public event UpdateSetupContinueButtonEventHandler UpdateSetupContinueButtonEvent = delegate { };
        public delegate void UpdateSetupContinueButtonEventHandler(bool button_enabled);

        public event UpdateAddProjectButtonEventHandler UpdateAddProjectButtonEvent = delegate { };
        public delegate void UpdateAddProjectButtonEventHandler(bool button_enabled);

        public event ChangeAddressFieldEventHandler ChangeAddressFieldEvent = delegate { };
        public delegate void ChangeAddressFieldEventHandler(string text, string example_text);

        public event ChangeRepositoryFieldEventHandler ChangeRepositoryFieldEvent = delegate { };
        public delegate void ChangeRepositoryFieldEventHandler(string text, string example_text);

        public event ChangePathFieldEventHandler ChangePathFieldEvent = delegate { };
        public delegate void ChangePathFieldEventHandler(string text, string example_text);

        public event ChangeUserFieldEventHandler ChangeUserFieldEvent = delegate { };
        public delegate void ChangeUserFieldEventHandler(string text, string example_text);

        public event ChangePasswordFieldEventHandler ChangePasswordFieldEvent = delegate { };
        public delegate void ChangePasswordFieldEventHandler(string text, string example_text);

        /// <summary>
        /// Whether the window is currently open.
        /// </summary>
        public bool WindowIsOpen { get; private set; }

        /// <summary>
        /// Current step of the tutorial.
        /// </summary>
        public int TutorialCurrentPage { get; private set; }

        /// <summary>
        /// Current step of the remote folder addition wizard.
        /// </summary>
        private PageType FolderAdditionWizardCurrentPage;

        public string PreviousAddress { get; private set; }
        public string PreviousPath { get; private set; }
        public string PreviousRepository { get; private set; }
        public string SyncingReponame { get; private set; }
        public string DefaultRepoPath { get; private set; }
        public double ProgressBarPercentage { get; private set; }

        public string saved_address = "";
        public string saved_remote_path = "";
        public string saved_user = "";
        public string saved_password = "";
        public string saved_repository = "";

        /// <summary>
        /// List of the CMIS repositories at the chosen URL.
        /// </summary>
        public Dictionary<string, string> repositories;

        /// <summary>
        /// Whether CmisSync should be started automatically at login.
        /// </summary>
        private bool create_startup_item = true;


        /// <summary>
        /// Load repositories information from a CMIS endpoint.
        /// </summary>
        static public CmisServer GetRepositoriesFuzzy(string url, string user, string password)
        {
            return CmisUtils.GetRepositoriesFuzzy(new Uri(url), user, password);
        }


        /// <summary>
        /// Get the list of subfolders contained in a CMIS folder.
        /// </summary>
        static public string[] GetSubfolders(string repositoryId, string path,
            string address, string user, string password)
        {
            return CmisUtils.GetSubfolders(repositoryId, path, address, user, password);
        }


        /// <summary>
        /// Regex to check an HTTP/HTTPS URL.
        /// </summary>
        private Regex UrlRegex = new Regex(@"^" +
                    "(https?)://" +                                                 // protocol
                    "(([a-z\\d$_\\.\\+!\\*'\\(\\),;\\?&=-]|%[\\da-f]{2})+" +        // username
                    "(:([a-z\\d$_\\.\\+!\\*'\\(\\),;\\?&=-]|%[\\da-f]{2})+)?" +     // password
                    "@)?(?#" +                                                      // auth delimiter
                    ")((([a-z\\d]\\.|[a-z\\d][a-z\\d-]*[a-z\\d]\\.)*" +             // domain segments AND
                    "[a-z][a-z\\d-]*[a-z\\d]" +                                     // top level domain OR
                    "|((\\d|\\d\\d|1\\d{2}|2[0-4]\\d|25[0-5])\\.){3}" +             // IP address
                    "(\\d|[1-9]\\d|1\\d{2}|2[0-4]\\d|25[0-5])" +                    //
                    ")(:\\d+)?" +                                                   // port
                    ")((/+([a-z\\d$_\\.\\+!\\*'\\(\\),;:@&=-]|%[\\da-f]{2})*)*?)" + // path
                    "$", RegexOptions.IgnoreCase | RegexOptions.Compiled);


        /// <summary>
        /// Regex to check a CmisSync repository local folder name.
        /// Basically, it should be a valid local filesystem folder name.
        /// </summary>
        Regex RepositoryRegex = new Regex(@"^([a-zA-Z0-9][^*/><?\|:]*)$");


        /// <summary>
        /// Constructor.
        /// </summary>
        public SetupController()
        {
            Logger.Info("Entering constructor.");

            TutorialCurrentPage = 0;
            PreviousAddress = "";
            PreviousPath = "";
            SyncingReponame = "";
            DefaultRepoPath = Program.Controller.FoldersPath;

            // Actions.

            ChangePageEvent += delegate(PageType page)
            {
                this.FolderAdditionWizardCurrentPage = page;
            };

            Program.Controller.ShowSetupWindowEvent += delegate(PageType page)
            {
                if (this.FolderAdditionWizardCurrentPage == PageType.Syncing ||
                    this.FolderAdditionWizardCurrentPage == PageType.Finished)
                {
                    ShowWindowEvent();
                    return;
                }

                if (page == PageType.Add1)
                {
                    if (WindowIsOpen)
                    {
                        if (this.FolderAdditionWizardCurrentPage == PageType.Finished ||
                            this.FolderAdditionWizardCurrentPage == PageType.None)
                        {

                            ChangePageEvent(PageType.Add1);
                        }

                        ShowWindowEvent();

                    }
                    else if (TutorialCurrentPage == 0)
                    {
                        WindowIsOpen = true;
                        ChangePageEvent(PageType.Add1);
                        ShowWindowEvent();
                    }
                    return;
                }

                WindowIsOpen = true;
                ChangePageEvent(page);
                ShowWindowEvent();
            };
            Logger.Info("Exiting constructor.");
        }


        /// <summary>
        /// User pressed the "Cancel" button, hide window.
        /// </summary>
        public void PageCancelled()
        {
            PreviousAddress = "";
            PreviousRepository = "";
            PreviousPath = "";

            WindowIsOpen = false;
            HideWindowEvent();
        }


        public void CheckSetupPage()
        {
            UpdateSetupContinueButtonEvent(true);
        }


        /// <summary>
        /// First-time wizard has been cancelled, so quit CmisSync.
        /// </summary>
        public void SetupPageCancelled()
        {
            Program.Controller.Quit();
        }


        /// <summary>
        /// Move to second page of the tutorial.
        /// </summary>
        public void SetupPageCompleted()
        {
            TutorialCurrentPage = 1;
            ChangePageEvent(PageType.Tutorial);
        }


        /// <summary>
        /// Tutorial has been skipped, go to last step of wizard.
        /// </summary>
        public void TutorialSkipped()
        {
            TutorialCurrentPage = 4;
            ChangePageEvent(PageType.Tutorial);
        }


        /// <summary>
        /// Go to next step of the tutorial.
        /// </summary>
        public void TutorialPageCompleted()
        {
            TutorialCurrentPage++;

            // If last page reached, close tutorial.
            if (TutorialCurrentPage == 5)
            {
                TutorialCurrentPage = 0;
                this.FolderAdditionWizardCurrentPage = PageType.None;

                WindowIsOpen = false;
                HideWindowEvent();

                // If requested, add CmisSync to the list of programs to be started up when the user logs into Windows.
                if (this.create_startup_item)
                    new Thread(() => Program.Controller.CreateStartupItem()).Start();
            }
            else
            {
                // Go to next step of tutorial.
                ChangePageEvent(PageType.Tutorial);
            }
        }


        /// <summary>
        /// Checkbox to add CmisSync to the list of programs to be started up when the user logs into Windows.
        /// </summary>
        public void StartupItemChanged(bool create_startup_item)
        {
            this.create_startup_item = create_startup_item;
        }


        /// <summary>
        /// Check whether the address is syntaxically valid.
        /// If OK, enable button to next step.
        /// </summary>
        /// <param name="address">URL to check</param>
        /// <returns>validity error, or empty string if valid</returns>
        public string CheckAddPage(string address)
        {
            address = address.Trim();
            this.saved_address = address;

            // Check address validity.
            bool fields_valid = ((!string.IsNullOrEmpty(address)) && (this.UrlRegex.IsMatch(address)));

            // Enable button to next step.
            UpdateAddProjectButtonEvent(fields_valid);

            // Return validity error, or empty string if valid.
            if (String.IsNullOrEmpty(address)) {
                return "EmptyURLNotAllowed";
            }
            if (!this.UrlRegex.IsMatch(address)) {
                return "InvalidURL";
            }
            return String.Empty;
        }


        /// <summary>
        /// Check the repository name.
        /// </summary>
        /// <param name="reponame"></param>
        /// <returns>validity error, or empty string if valid</returns>
        public string CheckRepoName(string reponame)
        {
            // Check whether foldername is already in use
            bool folderAlreadyExists = (Program.Controller.Folders.FindIndex(x => x.Equals(reponame, StringComparison.OrdinalIgnoreCase)) != -1);

            // Check whether folde rname contains invalid characters.
            bool valid = (RepositoryRegex.IsMatch(reponame) && (!folderAlreadyExists));

            // Enable button to next step.
            UpdateAddProjectButtonEvent(valid);

            // Return validity error, or empty string if valid.
            if (folderAlreadyExists) return "FolderAlreadyExist";
            if (!RepositoryRegex.IsMatch(reponame)) return "InvalidFolderName";
            return String.Empty;
        }


        /// <summary>
        /// Check local repository path.
        /// </summary>
        /// <param name="localpath"></param>
        /// <returns>validity error, or empty string if valid</returns>
        public string CheckRepoPath(string localpath)
        {
            bool folderAlreadyExists = Directory.Exists(localpath);
            bool valid = !folderAlreadyExists;

            // Enable button to next step.
            UpdateAddProjectButtonEvent(valid);

            // Return validity error, or empty string if valid.
            if (folderAlreadyExists) return "LocalDirectoryExist";
            return String.Empty;

        }


        /// <summary>
        /// First step of remote folder addition wizard is complete, switch to second step
        /// </summary>
        public void Add1PageCompleted(string address, string user, string password)
        {
            saved_address = address;
            saved_user = user;
            saved_password = password;

            ChangePageEvent(PageType.Add2);
        }


        /// <summary>
        /// Switch back from second to first step, presumably to change server or user.
        /// </summary>
        public void BackToPage1()
        {
            PreviousAddress = saved_address;
            PreviousPath = saved_user;
            ChangePageEvent(PageType.Add1);
        }


        /// <summary>
        /// Second step of remote folder addition wizard is complete, switch to customization step.
        /// </summary>
        public void Add2PageCompleted(string repository, string remote_path)
        {
            SyncingReponame = Path.GetFileName(remote_path);
            ProgressBarPercentage = 1.0;

            ChangePageEvent(PageType.Customize);

            String address = saved_address.Trim();
            repository = repository.Trim();
            remote_path = remote_path.Trim();

            PreviousAddress = address;
            PreviousRepository = repository;
            PreviousPath = remote_path;
        }


        /// <summary>
        /// Customization step of remote folder addition wizard is complete, start CmisSync.
        /// </summary>
        public void CustomizePageCompleted(String repoName, String localrepopath)
        {
            SyncingReponame = repoName;

            ChangePageEvent(PageType.Syncing);

            Program.Controller.FolderFetched += AddPageFetchedDelegate;

            // Add the remote folder to the configuration and start syncing.
            try
            {
                new Thread(() =>
                {
                    Program.Controller.StartFetcher(PreviousAddress, PreviousPath, repoName,
                        PreviousRepository, PreviousPath, saved_user.TrimEnd(), saved_password.TrimEnd(), localrepopath);

                }).Start();
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex.ToString());
                System.Windows.Forms.MessageBox.Show("An error occur during first sync, see debug log for details!");
            }

        }


        /// <summary>
        /// Switch back from customization to step 2 of the remote folder addition wizard.
        /// </summary>
        public void BackToPage2()
        {
            ChangePageEvent(PageType.Add2);
        }

        /// <summary>
        /// Remote folder has been added, switch to the final step of the wizard.
        /// </summary>
        private void AddPageFetchedDelegate(string remote_url)
        {
            ChangePageEvent(PageType.Finished);

            Program.Controller.FolderFetched -= AddPageFetchedDelegate;
        }


        /// <summary>
        /// User clicked on the button to open the newly-created synchronized folder in the local file explorer.
        /// </summary>
        public void OpenFolderClicked()
        {
            Program.Controller.OpenCmisSyncFolder(SyncingReponame);
            SyncingReponame = String.Empty;
            FinishPageCompleted();
        }


        /// <summary>
        /// Folder addition wizard is over, reset it for next use.
        /// </summary>
        public void FinishPageCompleted()
        {
            PreviousAddress = "";
            PreviousPath = "";

            this.FolderAdditionWizardCurrentPage = PageType.None;
            HideWindowEvent();
        }
    }
}
