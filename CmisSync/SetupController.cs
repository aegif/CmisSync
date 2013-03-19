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

    public enum PageType
    {
        None,
        Setup,
        Add1,
        Add2,
        Customize,
        Syncing,
        Error,
        Finished,
        Tutorial
    }

    public enum FieldState
    {
        Enabled,
        Disabled
    }


    public class SetupController
    {
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(SetupController));

        public event Action ShowWindowEvent = delegate { };
        public event Action HideWindowEvent = delegate { };

        public event ChangePageEventHandler ChangePageEvent = delegate { };
        public delegate void ChangePageEventHandler(PageType page, string[] warnings);

        public event UpdateProgressBarEventHandler UpdateProgressBarEvent = delegate { };
        public delegate void UpdateProgressBarEventHandler(double percentage);

        public event UpdateSetupContinueButtonEventHandler UpdateSetupContinueButtonEvent = delegate { };
        public delegate void UpdateSetupContinueButtonEventHandler(bool button_enabled);

        public event UpdateAddProjectButtonEventHandler UpdateAddProjectButtonEvent = delegate { };
        public delegate void UpdateAddProjectButtonEventHandler(bool button_enabled);

        public event ChangeAddressFieldEventHandler ChangeAddressFieldEvent = delegate { };
        public delegate void ChangeAddressFieldEventHandler(string text, string example_text, FieldState state);

        public event ChangeRepositoryFieldEventHandler ChangeRepositoryFieldEvent = delegate { };
        public delegate void ChangeRepositoryFieldEventHandler(string text, string example_text, FieldState state);

        public event ChangePathFieldEventHandler ChangePathFieldEvent = delegate { };
        public delegate void ChangePathFieldEventHandler(string text, string example_text, FieldState state);

        public event ChangeUserFieldEventHandler ChangeUserFieldEvent = delegate { };
        public delegate void ChangeUserFieldEventHandler(string text, string example_text, FieldState state);

        public event ChangePasswordFieldEventHandler ChangePasswordFieldEvent = delegate { };
        public delegate void ChangePasswordFieldEventHandler(string text, string example_text, FieldState state);

        public bool WindowIsOpen { get; private set; }
        public int TutorialPageNumber { get; private set; }
        public string PreviousUrl { get; private set; }
        public string PreviousAddress { get; private set; }
        public string PreviousPath { get; private set; }
        public string PreviousRepository { get; private set; }
        public string SyncingReponame { get; private set; }
        public string DefaultRepoPath { get; private set; }
        public double ProgressBarPercentage { get; private set; }


        static public CmisServer GetRepositoriesFuzzy(string url, string user, string password)
        {
            return CmisUtils.GetRepositoriesFuzzy(url, user, password);
        }

        static public string[] GetSubfolders(string repositoryId, string path,
            string address, string user, string password)
        {
            return CmisUtils.GetSubfolders(repositoryId, path, address, user, password);
        }

        public bool FetchPriorHistory
        {
            get
            {
                return this.fetch_prior_history;
            }
        }

        private PageType current_page;
        public string saved_address = "";
        public string saved_remote_path = "";
        public string saved_user = "";
        public string saved_password = "";
        public string saved_repository = "";
        public Dictionary<string, string> repositories;
        private bool create_startup_item = true;
        private bool fetch_prior_history = false;


        public SetupController()
        {
            Logger.Info("Entering constructor.");
            ChangePageEvent += delegate(PageType page_type, string[] warnings)
            {
                this.current_page = page_type;
            };

            TutorialPageNumber = 0;
            PreviousAddress = "";
            PreviousPath = "";
            PreviousUrl = "";
            SyncingReponame = "";
            DefaultRepoPath = Program.Controller.FoldersPath;

            Program.Controller.ShowSetupWindowEvent += delegate(PageType page_type)
            {
                if (this.current_page == PageType.Syncing ||
                    this.current_page == PageType.Finished)
                {
                    ShowWindowEvent();
                    return;
                }

                if (page_type == PageType.Add1)
                {
                    if (WindowIsOpen)
                    {
                        if (this.current_page == PageType.Error ||
                            this.current_page == PageType.Finished ||
                            this.current_page == PageType.None)
                        {

                            ChangePageEvent(PageType.Add1, null);
                        }

                        ShowWindowEvent();

                    }
                    else if (TutorialPageNumber == 0)
                    {
                        WindowIsOpen = true;
                        ChangePageEvent(PageType.Add1, null);
                        ShowWindowEvent();
                    }

                    return;
                }

                WindowIsOpen = true;
                ChangePageEvent(page_type, null);
                ShowWindowEvent();
            };
            Logger.Info("Exiting constructor.");
        }


        public void PageCancelled()
        {
            PreviousAddress = "";
            PreviousRepository = "";
            PreviousPath = "";
            PreviousUrl = "";

            this.fetch_prior_history = false;

            WindowIsOpen = false;
            HideWindowEvent();
        }


        public void CheckSetupPage()
        {
            UpdateSetupContinueButtonEvent(true);
        }


        public void SetupPageCancelled()
        {
            Program.Controller.Quit();
        }


        public void SetupPageCompleted()
        {
            TutorialPageNumber = 1;
            ChangePageEvent(PageType.Tutorial, null);
        }


        public void TutorialSkipped()
        {
            TutorialPageNumber = 4;
            ChangePageEvent(PageType.Tutorial, null);
        }


        public void HistoryItemChanged(bool fetch_prior_history)
        {
            this.fetch_prior_history = fetch_prior_history;
        }


        public void TutorialPageCompleted()
        {
            TutorialPageNumber++;

            if (TutorialPageNumber == 5)
            {
                TutorialPageNumber = 0;
                this.current_page = PageType.None;

                WindowIsOpen = false;
                HideWindowEvent();

                if (this.create_startup_item)
                    new Thread(() => Program.Controller.CreateStartupItem()).Start();

            }
            else
            {
                ChangePageEvent(PageType.Tutorial, null);
            }
        }


        public void StartupItemChanged(bool create_startup_item)
        {
            this.create_startup_item = create_startup_item;
        }


        public string CheckAddPage(string address)
        {
            address = address.Trim();

            // Check that the first part of the URL (protocol and server name) are valid.
            Regex regx = new Regex(@"(http|https)://[a-zA-Z0-9].*");

            this.saved_address = address;

            bool fields_valid = ((!string.IsNullOrEmpty(address)) && (regx.IsMatch(address)));

            UpdateAddProjectButtonEvent(fields_valid);

            if (String.IsNullOrEmpty(address)) return "EmptyURLNotAllowed";
            if (!regx.IsMatch(address)) return "InvalidURL";
            return String.Empty;
        }

        public string CheckRepoName(string reponame)
        {
            // Check if foldername do not contains invalid car
            Regex regx = new Regex(@"^([a-zA-Z0-9][^*/><?\|:]*)$");

            // Check if foldername is already in use
            bool folder_already_exist = (Program.Controller.Folders.FindIndex(x => x.Equals(reponame, StringComparison.OrdinalIgnoreCase)) != -1);

            bool fields_valid = (regx.IsMatch(reponame) && (!folder_already_exist));

            UpdateAddProjectButtonEvent(fields_valid);

            if (folder_already_exist) return "FolderAlreadyExist";
            if (!regx.IsMatch(reponame)) return "InvalidFolderName";
            return String.Empty;
        }

        public string CheckRepoPath(string localpath)
        {
            bool folder_exist = Directory.Exists(localpath);

            bool fields_valid = !folder_exist;
            UpdateAddProjectButtonEvent(fields_valid);

            if (folder_exist) return "LocalDirectoryExist";
            return String.Empty;

        }

        public void Add1PageCompleted(string address, string user, string password)
        {
            saved_address = address;
            saved_user = user;
            saved_password = password;

            ChangePageEvent(PageType.Add2, null);
        }

        public void BackToPage1()
        {
            PreviousAddress = saved_address;
            PreviousPath = saved_user;
            ChangePageEvent(PageType.Add1, null);
        }

        public void Add2PageCompleted(string repository, string remote_path)
        {
            SyncingReponame = Path.GetFileName(remote_path);
            ProgressBarPercentage = 1.0;

            ChangePageEvent(PageType.Customize, null);

            String address = saved_address.Trim();
            repository = repository.Trim();
            remote_path = remote_path.Trim();

            PreviousAddress = address;
            PreviousRepository = repository;
            PreviousPath = remote_path;
        }

        public void CustomizePageCompleted(String repoName, String localrepopath, bool syncnow)
        {
            SyncingReponame = repoName;

            ChangePageEvent(PageType.Syncing, null);

            Program.Controller.FolderFetched += AddPageFetchedDelegate;
            Program.Controller.FolderFetchError += AddPageFetchErrorDelegate;
            Program.Controller.FolderFetching += SyncingPageFetchingDelegate;

            try
            {
                new Thread(() =>
                {
                    Program.Controller.StartFetcher(PreviousAddress, "SelectedPlugin.Fingerprint", PreviousPath, repoName,
                        "SelectedPlugin.AnnouncementsUrl", this.fetch_prior_history,
                        PreviousRepository, PreviousPath, saved_user.TrimEnd(), saved_password.TrimEnd(), localrepopath, syncnow);

                }).Start();
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex.ToString());
                System.Windows.Forms.MessageBox.Show("An error occur during first sync, see debug log for details!");
            }

        }

        public void BackToPage2()
        {
            ChangePageEvent(PageType.Add2, null);
        }

        // The following private methods are
        // delegates used by the previous method

        private void AddPageFetchedDelegate(string remote_url, string[] warnings)
        {
            ChangePageEvent(PageType.Finished, warnings);

            Program.Controller.FolderFetched -= AddPageFetchedDelegate;
            Program.Controller.FolderFetchError -= AddPageFetchErrorDelegate;
            Program.Controller.FolderFetching -= SyncingPageFetchingDelegate;
        }

        private void AddPageFetchErrorDelegate(string remote_url, string[] errors)
        {
            SyncingReponame = "";
            PreviousUrl = remote_url;

            ChangePageEvent(PageType.Error, errors);

            Program.Controller.FolderFetched -= AddPageFetchedDelegate;
            Program.Controller.FolderFetchError -= AddPageFetchErrorDelegate;
            Program.Controller.FolderFetching -= SyncingPageFetchingDelegate;
        }

        private void SyncingPageFetchingDelegate(double percentage)
        {
            ProgressBarPercentage = percentage;
            UpdateProgressBarEvent(ProgressBarPercentage);
        }


        // The following private methods are
        // delegates used by the previous method

        public void SyncingCancelled()
        {
            Program.Controller.StopFetcher();
            ChangePageEvent(PageType.Add1, null);
        }


        public void ErrorPageCompleted()
        {
            ChangePageEvent(PageType.Add1, null);
        }


        public void OpenFolderClicked()
        {
            Program.Controller.OpenCmisSyncFolder(SyncingReponame);
            SyncingReponame = String.Empty;
            FinishPageCompleted();
        }


        public void FinishPageCompleted()
        {
            PreviousUrl = "";
            PreviousAddress = "";
            PreviousPath = "";
            this.fetch_prior_history = false;

            this.current_page = PageType.None;
            HideWindowEvent();
        }
    }
}
