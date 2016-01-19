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
using System.ComponentModel;
using CmisSync.Auth;

namespace CmisSync
{
    /// <summary>
    /// Kind of pages that are used in the folder addition wizards.
    /// </summary>
    public enum PageType
    {
        /// <summary>
        /// No page.
        /// </summary>
        None,
        /// <summary>
        /// Setup page (add to startup items).
        /// </summary>
        Setup,
        /// <summary>
        /// Add repository page.
        /// </summary>
        Add1,
        /// <summary>
        /// Select remote folder.
        /// </summary>
        Add2,
        /// <summary>
        /// Select name/local folder.
        /// </summary>
        Customize,
        /// <summary>
        /// Add complete.
        /// </summary>
        Finished,
        /// <summary>
        /// Tutorial - contains sub-steps that are tracked via a number.
        /// </summary>
        Tutorial,
        /// <summary>
        /// Settings page.
        /// </summary>
        Settings,
        /// <summary>
        /// 
        /// </summary>
		Syncing
    }

    /// <summary>
    /// MVC controller for the two wizards:
    /// - CmisSync tutorial that appears at firt run,
    /// - wizard to add a new remote folder.
    /// </summary>
    public class SetupController
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SetupController));

        /// <summary>
        /// Show window event.
        /// </summary>
        public event Action ShowWindowEvent = delegate { };

        /// <summary>
        /// Hide window event.
        /// </summary>
        public event Action HideWindowEvent = delegate { };

        /// <summary>
        /// Change page event.
        /// </summary>
        public event ChangePageEventHandler ChangePageEvent = delegate { };

        /// <summary>
        /// Change page event.
        /// </summary>
        public delegate void ChangePageEventHandler(PageType page);

        /// <summary>
        /// Update progress bar event.
        /// </summary>
        public event UpdateProgressBarEventHandler UpdateProgressBarEvent = delegate { };

        /// <summary>
        /// Update progress bar event.
        /// </summary>
        public delegate void UpdateProgressBarEventHandler(double percentage);

        /// <summary>
        /// Update setup continue button.
        /// </summary>
        public event UpdateSetupContinueButtonEventHandler UpdateSetupContinueButtonEvent = delegate { };

        /// <summary>
        /// Update setup continue button.
        /// </summary>
        public delegate void UpdateSetupContinueButtonEventHandler(bool button_enabled);

        /// <summary>
        /// Update add project button event.
        /// </summary>
        public event UpdateAddProjectButtonEventHandler UpdateAddProjectButtonEvent = delegate { };

        /// <summary>
        /// Update add project button event.
        /// </summary>
        public delegate void UpdateAddProjectButtonEventHandler(bool button_enabled);

        /// <summary>
        /// Change address field event.
        /// </summary>
        public event ChangeAddressFieldEventHandler ChangeAddressFieldEvent = delegate { };

        /// <summary>
        /// Change address field event.
        /// </summary>
        public delegate void ChangeAddressFieldEventHandler(string text, string example_text);

        /// <summary>
        /// Change repository field event.
        /// </summary>
        public event ChangeRepositoryFieldEventHandler ChangeRepositoryFieldEvent = delegate { };

        /// <summary>
        /// Change repository field event.
        /// </summary>
        public delegate void ChangeRepositoryFieldEventHandler(string text, string example_text);

        /// <summary>
        /// Change path field event.
        /// </summary>
        public event ChangePathFieldEventHandler ChangePathFieldEvent = delegate { };

        /// <summary>
        /// Change path field event.
        /// </summary>
        public delegate void ChangePathFieldEventHandler(string text, string example_text);

        /// <summary>
        /// Change user field.
        /// </summary>
        public event ChangeUserFieldEventHandler ChangeUserFieldEvent = delegate { };

        /// <summary>
        /// Change user field.
        /// </summary>
        public delegate void ChangeUserFieldEventHandler(string text, string example_text);

        /// <summary>
        /// Change password event.
        /// </summary>
        public event ChangePasswordFieldEventHandler ChangePasswordFieldEvent = delegate { };

        /// <summary>
        /// Change password event.
        /// </summary>
        public delegate void ChangePasswordFieldEventHandler(string text, string example_text);

        /// <summary>
        /// 
        /// </summary>
        public event LocalPathExistsEventHandler LocalPathExists;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public delegate bool LocalPathExistsEventHandler(string path);

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

        /// <summary>
        /// Previous address.
        /// </summary>
        public Uri PreviousAddress { get; private set; }

        /// <summary>
        /// Event.
        /// </summary>
        public string PreviousPath { get; private set; }

        /// <summary>
        /// Previous repository.
        /// </summary>
        public string PreviousRepository { get; private set; }

        /// <summary>
        /// Syncing repository name.
        /// </summary>
        public string SyncingReponame { get; private set; }

        /// <summary>
        /// Default repository path..
        /// </summary>
        public string DefaultRepoPath { get; private set; }

        /// <summary>
        /// Progress bar percentage.
        /// </summary>
        public double ProgressBarPercentage { get; private set; }

        /// <summary>
        /// Saved address.
        /// </summary>
        public Uri saved_address = new Uri(@"https://cloud.nemakiware.com/");

        /// <summary>
        /// Saved remote path.
        /// </summary>
        public string saved_remote_path = "";

        /// <summary>
        /// Saved user.
        /// </summary>
        public string saved_user = "";

        /// <summary>
        /// Saved password.
        /// </summary>
        public string saved_password = "";

        /// <summary>
        /// Saved repository.
        /// </summary>
        public string saved_repository = "";

        /// <summary>
        /// Saved sync interval.
        /// </summary>
        public int saved_sync_interval = 15;

        /// <summary>
        /// Saved sync at startup
        /// </summary>
        public bool saved_syncatstartup = true;

        /// <summary>
        /// Ignored paths.
        /// </summary>
        public List<string> ignoredPaths = new List<string>();

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
        static public Tuple<CmisServer, Exception> GetRepositoriesFuzzy(ServerCredentials credentials)
        {
            try
            {
                return CmisUtils.GetRepositoriesFuzzy(credentials);
            }
            catch (Exception e)
            {
                return new Tuple<CmisServer, Exception>(null, e);
            }

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
        private Regex UrlRegex = new Regex(@"^" + // FIXME use http://stackoverflow.com/questions/161738/what-is-the-best-regular-expression-to-check-if-a-string-is-a-valid-url
                    "(https?)://" +                                                 // protocol
                    "(([a-z\\d$_\\.\\+!\\*'\\(\\),;\\?&=-]|%[\\da-f]{2})+" +        // username
                    "(:([a-z\\d$_\\.\\+!\\*'\\(\\),;\\?&=-]|%[\\da-f]{2})+)?" +     // password
                    "@)?(?#" +                                                      // auth delimiter
                    ")((([a-z0-9\\d]\\.|[a-z0-9\\d][a-z0-9\\d-]*[a-z0-9\\d]\\.)*" +             // domain segments AND
                    "[a-z0-9][a-z0-9\\d-]*[a-z0-9\\d]" +                                     // top level domain OR
                    "|((\\d|\\d\\d|1\\d{2}|2[0-4]\\d|25[0-5])\\.){3}" +             // IP address
                    "(\\d|[1-9]\\d|1\\d{2}|2[0-4]\\d|25[0-5])" +                    //
                    ")(:\\d+)?" +                                                   // port
                    ")(.*)" +                                                       // path
                    "$", RegexOptions.IgnoreCase | RegexOptions.Compiled);


        /// <summary>
        /// Regex to check a CmisSync repository local folder name.
        /// Basically, it should be a valid local filesystem folder name.
        /// </summary>
        Regex RepositoryRegex = new Regex(@"^([a-zA-Z0-9][^*/><?\|:]*)$");
        Regex RepositoryRegexLinux = new Regex(@"^([a-zA-Z0-9][^*\\><?\|:]*)$");

        /// <summary>
        /// Constructor.
        /// </summary>
        public SetupController()
        {
            Logger.Debug("SetupController - Entering constructor.");

            TutorialCurrentPage = 0;
            PreviousAddress = null;
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
                if (this.FolderAdditionWizardCurrentPage == PageType.Finished)
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
            Logger.Debug("SetupController - Exiting constructor.");
        }

        /// <summary>
        /// User pressed the "Cancel" button, hide window.
        /// </summary>
        public void PageCancelled()
        {
            PreviousAddress = null;
            PreviousRepository = "";
            PreviousPath = "";
            ignoredPaths.Clear();

            WindowIsOpen = false;
            HideWindowEvent();
        }

        /// <summary>
        /// Check setup page.
        /// </summary>
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

                // ASaas Drive is forced to start up by installer
                /*
                // If requested, add CmisSync to the list of programs to be started up when the user logs into Windows.
                if (this.create_startup_item)
                    new Thread(() => Program.Controller.CreateStartupItem()).Start();
                */
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


            bool emptyAddress = string.IsNullOrEmpty(address);
            bool regexMatch = this.UrlRegex.IsMatch(address);
            // Check address validity.
            if (!emptyAddress && regexMatch)
            {
                try
                {
                    this.saved_address = new Uri(address);
                }
                catch (Exception ex)
                {
                    Logger.Debug("Error creating URI: " + ex.Message, ex);
                    regexMatch = false;
                }
            }
            // Enable button to next step.
            UpdateAddProjectButtonEvent(!emptyAddress && regexMatch);

            // Return validity error, or empty string if valid.
            if (emptyAddress)
            {
                return "EmptyURLNotAllowed";
            }
            if (!regexMatch)
            {
                return "InvalidURL";
            }
            return String.Empty;
        }

        /// <summary>
        /// Check local repository path and repo name.
        /// </summary>
        /// <param name="localpath"></param>
        /// <param name="reponame"></param>
        /// <returns>validity error, or empty string if valid</returns>
        public string CheckRepoPathAndName(string localpath, string reponame)
        {
            try
            {
                // Check whether foldername is already in use
                int index = Program.Controller.Folders.FindIndex(x => x.Equals(reponame, StringComparison.OrdinalIgnoreCase));
                if ( index != -1)
                    throw new ArgumentException(String.Format(Properties_Resources.FolderAlreadyInUse, localpath, Program.Controller.Folders[index]));

                // Check whether folder name contains invalid characters.
                Regex regexRepoName = (Path.DirectorySeparatorChar.Equals('\\')) ? RepositoryRegex : RepositoryRegexLinux;
                if (!regexRepoName.IsMatch(reponame)||CmisSync.Lib.Utils.IsInvalidFolderName(reponame.Replace(Path.DirectorySeparatorChar, ' ')))
                    throw new ArgumentException(String.Format(Properties_Resources.InvalidRepoName, reponame));
                // Validate localpath
                if(localpath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    localpath = localpath.Substring(0,localpath.Length-1);
                if (CmisSync.Lib.Utils.IsInvalidFolderName(Path.GetFileName(localpath)))
                    throw new ArgumentException(String.Format(Properties_Resources.InvalidFolderName, Path.GetFileName(localpath)));
                // If no warning handler is registered, handle warning as error
                if (LocalPathExists == null)
                    CheckRepoPathExists(localpath);
                UpdateAddProjectButtonEvent(true);
                return String.Empty;
            }
            catch (Exception e)
            {
                UpdateAddProjectButtonEvent(false);
                return e.Message;
            }
        }

        /// <summary></summary>
        /// <param name="localpath"></param>
        public void CheckRepoPathExists(string localpath)
        {
            if (Directory.Exists(localpath))
                throw new ArgumentException(String.Format(Properties_Resources.LocalDirectoryExist));
        }

        /// <summary>
        /// First step of remote folder addition wizard is complete, switch to second step
        /// </summary>
        public void Add1PageCompleted(Uri address, string user, string password)
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
        public void Add2PageCompleted(string repository, string remote_path, string[] ignoredPaths, string[] selectedFolder)
        {
            SyncingReponame = Path.GetFileName(remote_path);
            ProgressBarPercentage = 1.0;

            ChangePageEvent(PageType.Customize);

            Uri address = saved_address;
            repository = repository.Trim();
            remote_path = remote_path.Trim();

            PreviousAddress = address;
            PreviousRepository = repository;
            PreviousPath = remote_path;

            this.ignoredPaths.Clear();
            foreach (string ignore in ignoredPaths)
                this.ignoredPaths.Add(ignore);
        }

        /// <summary>
        /// Second step of remote folder addition wizard is complete, switch to customization step.
        /// </summary>
        public void Add2PageCompleted(string repository, string remote_path)
        {
            Add2PageCompleted(repository, remote_path, new string[] { }, new string[] { });
        }

        /// <summary>
        /// Customization step of remote folder addition wizard is complete, start CmisSync.
        /// </summary>
        public void CustomizePageCompleted(String repoName, String localrepopath)
        {
            try
            {
                CheckRepoPathExists(localrepopath);
            }
            catch (ArgumentException)
            {
                if (LocalPathExists != null && ! LocalPathExists(localrepopath))
                {
                    return;
                }
            }
            SyncingReponame = repoName;


            // Add the remote folder to the configuration and start syncing.
            try
            {
                Program.Controller.CreateRepository(
                    repoName,
                    saved_address,
                    saved_user.TrimEnd(),
                    saved_password.TrimEnd(),
                    PreviousRepository,
                    PreviousPath,
                    localrepopath,
                    ignoredPaths,
                    saved_syncatstartup);
            }
            catch (Exception e)
            {
                Logger.Fatal("Could not create repository.", e);
                Program.Controller.ShowAlert(Properties_Resources.Error, String.Format(Properties_Resources.SyncError, repoName, e.Message));
                FinishPageCompleted();
                return;
            }

            ChangePageEvent(PageType.Finished);
        }

        /// <summary>
        /// Switch back from customization to step 2 of the remote folder addition wizard.
        /// </summary>
        public void BackToPage2()
        {
            ignoredPaths.Clear();
            ChangePageEvent(PageType.Add2);
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
            PreviousAddress = null;
            PreviousPath = "";

            this.FolderAdditionWizardCurrentPage = PageType.None;
            HideWindowEvent();
        }

        /// <summary>
        /// Repository settings page.
        /// </summary>
        public void SettingsPageCompleted(string password, int pollInterval, bool syncAtStartup)
        {
            //Run this in background so as not to free the GUI...
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(
                delegate(Object o, DoWorkEventArgs args)
                {
                    Program.Controller.UpdateRepositorySettings(saved_repository, password, pollInterval, syncAtStartup);
                }
            );
            worker.RunWorkerAsync();

            FinishPageCompleted();
        }
    }
}
