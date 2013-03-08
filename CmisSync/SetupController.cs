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
        Invite,
        Syncing,
        Error,
        Finished,
        Tutorial,
        CryptoSetup,
        CryptoPassword
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

        public event UpdateCryptoSetupContinueButtonEventHandler UpdateCryptoSetupContinueButtonEvent = delegate { };
        public delegate void UpdateCryptoSetupContinueButtonEventHandler(bool button_enabled);

        public event UpdateCryptoPasswordContinueButtonEventHandler UpdateCryptoPasswordContinueButtonEvent = delegate { };
        public delegate void UpdateCryptoPasswordContinueButtonEventHandler(bool button_enabled);

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

        public readonly List<Plugin> Plugins = new List<Plugin>();
        public Plugin SelectedPlugin;

        public bool WindowIsOpen { get; private set; }
        public Invite PendingInvite { get; private set; }
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

        public int SelectedPluginIndex
        {
            get
            {
                return Plugins.IndexOf(SelectedPlugin);
            }
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

            string local_plugins_path = Plugin.LocalPluginsPath;
            int local_plugins_count = 0;

            // Import all of the plugins
            if (Directory.Exists(local_plugins_path))
                // Local plugins go first...
                foreach (string xml_file_path in Directory.GetFiles(local_plugins_path, "*.xml"))
                {
                    Plugins.Add(new Plugin(xml_file_path));
                    local_plugins_count++;
                }

            // ...system plugins after that...
            if (Directory.Exists(Program.Controller.PluginsPath))
            {
                foreach (string xml_file_path in Directory.GetFiles(Program.Controller.PluginsPath, "*.xml"))
                {
                    // ...and "Own server" at the very top
                    if (xml_file_path.EndsWith("own-server.xml"))
                    {
                        Plugins.Insert(0, new Plugin(xml_file_path));

                    }
                    else if (xml_file_path.EndsWith("ssnet.xml"))
                    {
                        // Plugins.Insert ((local_plugins_count + 1), new Plugin (xml_file_path)); TODO: Skip this plugin for now

                    }
                    else
                    {
                        Plugins.Add(new Plugin(xml_file_path));
                    }
                }
            }

            SelectedPlugin = Plugins[0];

            Program.Controller.InviteReceived += delegate(Invite invite)
            {
                PendingInvite = invite;

                ChangePageEvent(PageType.Invite, null);
                ShowWindowEvent();
            };

            Program.Controller.ShowSetupWindowEvent += delegate(PageType page_type)
            {
                if (page_type == PageType.CryptoSetup || page_type == PageType.CryptoPassword)
                {
                    ChangePageEvent(page_type, null);
                    return;
                }

                if (PendingInvite != null)
                {
                    WindowIsOpen = true;
                    ShowWindowEvent();
                    return;
                }

                if (this.current_page == PageType.Syncing ||
                    this.current_page == PageType.Finished ||
                    this.current_page == PageType.CryptoSetup ||
                    this.current_page == PageType.CryptoPassword)
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
                    else if (!Program.Controller.FirstRun && TutorialPageNumber == 0)
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
        }


        public void PageCancelled()
        {
            PendingInvite = null;
            SelectedPlugin = Plugins[0];
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
            Program.Controller.CurrentUser = new User("Dummy", "dummy@example.org");

            /*
            new Thread (() => {
                string keys_path     = Path.GetDirectoryName (Config.DefaultConfig.FullPath);
                string key_file_name = DateTime.Now.ToString ("yyyy-MM-dd HH\\hmm");

                string [] key_pair = Keys.GenerateKeyPair (keys_path, key_file_name);
                Keys.ImportPrivateKey (key_pair [0]);

                string link_code_file_path = Path.Combine (Program.Controller.FoldersPath,
                    Program.Controller.CurrentUser.Name + "'s link code.txt");

                // Create an easily accessible copy of the public
                // key in the user's CmisSync folder
                File.Copy (key_pair [1], link_code_file_path, true);

            }).Start ();
            */

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


        public void SelectedPluginChanged(int plugin_index)
        {
            SelectedPlugin = Plugins[plugin_index];

            if (SelectedPlugin.Address != null)
            {
                ChangeAddressFieldEvent(SelectedPlugin.Address, "", FieldState.Disabled);

            }
            else if (SelectedPlugin.AddressExample != null)
            {
                ChangeAddressFieldEvent(this.saved_address, SelectedPlugin.AddressExample, FieldState.Enabled);

            }
            else
            {
                ChangeAddressFieldEvent(this.saved_address, "", FieldState.Enabled);
            }

            if (SelectedPlugin.Path != null)
            {
                ChangePathFieldEvent(SelectedPlugin.Path, "", FieldState.Disabled);

            }
            else if (SelectedPlugin.PathExample != null)
            {
                ChangePathFieldEvent(this.saved_remote_path, SelectedPlugin.PathExample, FieldState.Enabled);

            }
            else
            {
                ChangePathFieldEvent(this.saved_remote_path, "", FieldState.Enabled);
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

            if (SelectedPlugin.PathUsesLowerCase)
                remote_path = remote_path.ToLower();

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
                    Program.Controller.StartFetcher(PreviousAddress, SelectedPlugin.Fingerprint, PreviousPath, repoName,
                        SelectedPlugin.AnnouncementsUrl, this.fetch_prior_history,
                        PreviousRepository, PreviousPath, saved_user.TrimEnd(), saved_password.TrimEnd(), localrepopath, syncnow);

                }).Start();
            }
            catch (Exception ex)
            {
                Logger.Fatal("Fetcher | " + ex.ToString());
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
            //SyncingFolder = "";

            // Create a local plugin for succesfully added projects, so
            // so the user can easily use the same host again
            if (SelectedPluginIndex == 0)
            {
                Plugin new_plugin;
                Uri uri = new Uri(remote_url);

                try
                {
                    string address = remote_url.Replace(uri.AbsolutePath, "");

                    new_plugin = Plugin.Create(uri.Host, address, address, "", "", "", "", "/path/to/project", "", "", "", "");

                    if (new_plugin != null)
                    {
                        Plugins.Insert(1, new_plugin);
                        Logger.Info("Controller | Added plugin for " + uri.Host);
                    }

                }
                catch
                {
                    Logger.Info("Controller | Failed adding plugin for " + uri.Host);
                }
            }

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


        public void InvitePageCompleted()
        {
            /*SyncingFolder   = Path.GetFileNameWithoutExtension (PendingInvite.RemotePath);
            PreviousAddress = PendingInvite.Address;
            PreviousPath    = PendingInvite.RemotePath;

            ChangePageEvent (PageType.Syncing, null);

            new Thread (() => {
                if (!PendingInvite.Accept (Program.Controller.CurrentUser.PublicKey)) {
                    PreviousUrl = PendingInvite.Address +
                        PendingInvite.RemotePath.TrimStart ("/".ToCharArray ());

                    ChangePageEvent (PageType.Error, new string [] { "error: Failed to upload the public key" });
                    return;
                }

                Program.Controller.FolderFetched    += InvitePageFetchedDelegate;
                Program.Controller.FolderFetchError += InvitePageFetchErrorDelegate;
                Program.Controller.FolderFetching   += SyncingPageFetchingDelegate;

                Program.Controller.StartFetcher (PendingInvite.Address, PendingInvite.Fingerprint,
                    PendingInvite.RemotePath, PendingInvite.AnnouncementsUrl, false,
                    repository, remote_path, user, password); // TODO: checkbox on invite page

            }).Start ();*/
        }

        // The following private methods are
        // delegates used by the previous method

        private void InvitePageFetchedDelegate(string remote_url, string[] warnings)
        {
            SyncingReponame = "";
            PendingInvite = null;

            ChangePageEvent(PageType.Finished, warnings);

            Program.Controller.FolderFetched -= AddPageFetchedDelegate;
            Program.Controller.FolderFetchError -= AddPageFetchErrorDelegate;
            Program.Controller.FolderFetching -= SyncingPageFetchingDelegate;
        }

        private void InvitePageFetchErrorDelegate(string remote_url, string[] errors)
        {
            SyncingReponame = "";
            PreviousUrl = remote_url;

            ChangePageEvent(PageType.Error, errors);

            Program.Controller.FolderFetched -= AddPageFetchedDelegate;
            Program.Controller.FolderFetchError -= AddPageFetchErrorDelegate;
            Program.Controller.FolderFetching -= SyncingPageFetchingDelegate;
        }


        public void SyncingCancelled()
        {
            Program.Controller.StopFetcher();

            if (PendingInvite != null)
                ChangePageEvent(PageType.Invite, null);
            else
                ChangePageEvent(PageType.Add1, null);
        }


        public void ErrorPageCompleted()
        {
            if (PendingInvite != null)
                ChangePageEvent(PageType.Invite, null);
            else
                ChangePageEvent(PageType.Add1, null);
        }


        public void CheckCryptoSetupPage(string password)
        {
            bool valid_password = (password.Length > 0 && !password.Contains(" "));
            UpdateCryptoSetupContinueButtonEvent(valid_password);
        }


        public void CheckCryptoPasswordPage(string password)
        {
            bool password_correct = Program.Controller.CheckPassword(password);
            UpdateCryptoPasswordContinueButtonEvent(password_correct);
        }


        public void CryptoPageCancelled()
        {
            SyncingCancelled();
        }


        public void CryptoSetupPageCompleted(string password)
        {
            CryptoPasswordPageCompleted(password);
        }


        public void CryptoPasswordPageCompleted(string password)
        {
            ProgressBarPercentage = 100.0;
            ChangePageEvent(PageType.Syncing, null);

            new Thread(() =>
            {
                Thread.Sleep(1000);
                Program.Controller.FinishFetcher(password);

            }).Start();
        }


        public void OpenFolderClicked()
        {
            Program.Controller.OpenCmisSyncFolder(SyncingReponame);
            SyncingReponame = String.Empty;
            FinishPageCompleted();
        }


        public void FinishPageCompleted()
        {
            SelectedPlugin = Plugins[0];
            PreviousUrl = "";
            PreviousAddress = "";
            PreviousPath = "";
            this.fetch_prior_history = false;

            this.current_page = PageType.None;
            HideWindowEvent();
        }


        private bool IsValidEmail(string email)
        {
            return new Regex(@"^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,4}$", RegexOptions.IgnoreCase).IsMatch(email);
        }
    }
}
