using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using CmisSync.Lib;
using CmisSync.Lib.Sync;
using System.Windows.Threading;
using CmisSync.Views;
using CmisSync.ViewModels;
using DotCMIS.Exceptions;
using ExtendedWindowsControls;
using System.Windows.Input;
using WPFGrowlNotification;
using System.Drawing;
using System.ComponentModel;

namespace CmisSync
{
    public class Controller : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Controller));

        /// <summary>
        /// Concurrency locks.
        /// </summary>
        private Object repo_lock = new Object();

        /// <summary>
        /// List of the CmisSync synchronized folders.
        /// </summary>
        public readonly SyncronizersCollection SyncFolders = new SyncronizersCollection();

        private Views.MainNotifyWindow mainWindow;
        private ExtendedNotifyIcon notifyIcon;
        private GrowlNotifiactions notifications = new GrowlNotifiactions();

        public Controller()
        {
            InitMainWindow();
            InitNofifyIcon();
            InitNotifications();
        }

        #region MainWindow

        private void InitMainWindow()
        {
            ViewModels.ControllerViewModel model = new ViewModels.ControllerViewModel(this);
            mainWindow = new Views.MainNotifyWindow(model);
        }

        #endregion

        #region NotifyIcon

        private void InitNofifyIcon()
        {
            notifyIcon = new ExtendedNotifyIcon(500);
            notifyIcon.MouseClick += new ExtendedNotifyIcon.MouseClickHandler(extendedNotifyIcon_ShowWindow);
            notifyIcon.MouseLeave += new ExtendedNotifyIcon.MouseLeaveHandler(extendedNotifyIcon_HideWindow);
            mainWindow.MouseMove += new MouseEventHandler(window_OnMouseMove);
            mainWindow.MouseLeave += new MouseEventHandler(window_OnMouseLeave);
            notifyIcon_SetNotifyIcon();

            ((INotifyPropertyChanged)SyncFolders).PropertyChanged += delegate(object sender, PropertyChangedEventArgs e)
            {
                if ("IsSyncing".Equals(e.PropertyName))
                {
                    notifyIcon.Animation.Enabled = SyncFolders.IsSyncing;                  
                }
            };

            //notifyIcon.Animation.Start();
        }

        /// <summary>
        /// Pulls an icon from the packed resource and applies it to the NotifyIcon control
        /// </summary>
        /// <param name="iconPrefix"></param>
        private void notifyIcon_SetNotifyIcon()
        {
            System.IO.Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,/Resources/tryIcon.ico")).Stream;
            notifyIcon.Icon = new System.Drawing.Icon(iconStream);
            
            System.IO.Stream animationStream = Application.GetResourceStream(new Uri("pack://application:,,/Resources/tryIconAnimation.png")).Stream;
            Bitmap animationBitmap = new Bitmap(animationStream);
            notifyIcon.Animation = new Animation(animationBitmap);
        }

        void extendedNotifyIcon_ShowWindow()
        {
            mainWindow.Show();
            mainWindow.Topmost = true; // Very rarely, the window seems to get "buried" behind others, this seems to resolve the problem
            notifyIcon.StartMouseLeaveTimer();
        }

        void extendedNotifyIcon_HideWindow()
        {
            mainWindow.Hide();
        }

        public void window_OnMouseMove(object sender, MouseEventArgs e)
        {
            this.notifyIcon.StopMouseLeaveEventFromFiring();
        }

        public void window_OnMouseLeave(object sender, MouseEventArgs e)
        {
            ;
            double x = Mouse.GetPosition(mainWindow).X;
            double y = Mouse.GetPosition(mainWindow).Y;

            if (10 < x && x < mainWindow.Width - 10 && 10 < y && y < mainWindow.Height - 10)
            {
                return;
            }
            this.notifyIcon.StartMouseLeaveTimer();
        }

        #endregion

        #region Notifications

        private void InitNotifications()
        {
            notifications = new GrowlNotifiactions();
            notifications.Top = SystemParameters.WorkArea.Top + 20;
            notifications.Left = SystemParameters.WorkArea.Left + SystemParameters.WorkArea.Width - 380;
        }

        #endregion

        internal void startBackgroundWork()
        {
            Thread t = new Thread(() =>
            {
                loadRepositoriesFromConfig();
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        /// <summary>
        /// Check the configured CmisSync synchronized folders.
        /// </summary>
        private void loadRepositoriesFromConfig()
        {
            lock (this.repo_lock)
            {
                Queue<Config.SyncConfig.SyncFolder> missingFolders = new Queue<Config.SyncConfig.SyncFolder>();
                foreach (Config.SyncConfig.SyncFolder r in ConfigManager.CurrentConfig.SyncFolders)
                {
                    string folder_path = r.LocalPath;

                    if (!Directory.Exists(folder_path))
                    {
                        // If folder has been deleted, ask the user what to do.
                        Logger.Info("ControllerBase | Found missing folder '" + r.DisplayName + "'");
                        missingFolders.Enqueue(r);
                    }
                    else
                    {
                        ConfigureAndStartSyncFolderSyncronization(r);
                    }
                }

                while (missingFolders.Count != 0)
                {
                    handleMissingSyncFolder(missingFolders.Dequeue());
                }

                ConfigManager.CurrentConfig.Save();
            }
        }

        /// <summary>
        /// Initialize (in the GUI and syncing mechanism) an existing CmisSync synchronized folder.
        /// </summary>
        /// <param name="repositoryInfo">Synchronized folder path</param>
        private void ConfigureAndStartSyncFolderSyncronization(Config.SyncConfig.SyncFolder repositoryInfo)
        {
            //create the local directory
            System.IO.Directory.CreateDirectory(repositoryInfo.LocalPath);

            CmisSync.Lib.Sync.SyncFolderSyncronizer syncronizer = new CmisSync.Lib.Sync.SyncFolderSyncronizer(repositoryInfo);
            syncronizer.Events.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(Syncronizer_Events_CollectionChanged);
            this.SyncFolders.Add(syncronizer);
            syncronizer.Initialize();
        }

        private void Syncronizer_Events_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (SyncronizerEvent item in e.NewItems)
                {
                    if (item.Exception is MissingRootSyncFolderException) {
                        handleMissingSyncFolder(item.SyncFolderInfo);
                    }else{
                        NotifyEvent(item);
                    }
                }
            }
        }

        private void NotifyEvent(SyncronizerEvent item)
        {
            string imgUrl = "";
            switch (item.Level)
            { 
                case EventLevel.ERROR:
                    imgUrl = "pack://application:,,,/Resources/error_67.png";
                    break;
                case EventLevel.WARN:
                    imgUrl = "pack://application:,,,/Resources/warn_67.png";
                    break;
                case EventLevel.INFO:
                    imgUrl = "pack://application:,,,/Resources/info_67.png";
                    break;
            }

            notifications.AddNotification(new Notification { 
                Title = item.SyncFolderInfo.DisplayName,
                ImageUrl = imgUrl, 
                Message = item.Exception.Message
            });
        }

        private void handleMissingSyncFolder(Config.SyncConfig.SyncFolder syncFolderInfo)
        {
            bool handled = false;

            while (handled == false)
            {
                Views.MissingFolderDialog dialog = new Views.MissingFolderDialog(syncFolderInfo);
                dialog.ShowDialog();
                if (dialog.Result == Views.MissingFolderDialog.Action.MOVE)
                {
                    String startPath = Directory.GetParent(syncFolderInfo.LocalPath).FullName;
                    System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
                    fbd.SelectedPath = startPath;
                    fbd.Description = "Select the folder you have moved or renamed";
                    fbd.ShowNewFolderButton = false;
                    System.Windows.Forms.DialogResult result = fbd.ShowDialog();

                    if (result == System.Windows.Forms.DialogResult.OK && fbd.SelectedPath.Length > 0)
                    {
                        if (!Directory.Exists(fbd.SelectedPath))
                        {
                            throw new InvalidDataException();
                        }
                        Logger.Info("ControllerBase | Folder '" + syncFolderInfo.DisplayName + "' ('" + syncFolderInfo.LocalPath + "') moved to '" + fbd.SelectedPath + "'");
                        syncFolderInfo.LocalPath = fbd.SelectedPath;

                        ConfigureAndStartSyncFolderSyncronization(syncFolderInfo);
                        handled = true;
                    }
                }
                else if (dialog.Result == Views.MissingFolderDialog.Action.REMOVE)
                {
                    StopAndRemoveSyncFolderSyncronization(syncFolderInfo);
                    ConfigManager.CurrentConfig.SyncFolders.Remove(syncFolderInfo);

                    Logger.Info("ControllerBase | Removed folder '" + syncFolderInfo.DisplayName + "' from config");
                    handled = true;
                }
                else if (dialog.Result == Views.MissingFolderDialog.Action.RECREATE)
                {
                    StopAndRemoveSyncFolderSyncronization(syncFolderInfo);
                    AddAndStartNewSyncFolderSyncronization(syncFolderInfo);
                    //TODO: also remove and recreate the config to ensure a fresh start
                    Logger.Info("ControllerBase | Folder '" + syncFolderInfo.DisplayName + "' recreated");
                    handled = true;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        private void StopAndRemoveSyncFolderSyncronization(Config.SyncConfig.SyncFolder syncFolderInfo)
        {
            bool found = false;
            //search and stop the syncher if already started
            foreach (CmisSync.Lib.Sync.SyncFolderSyncronizer syncher in this.SyncFolders)
            {
                //FIXME: can we identify a synced folder by it's localPath?
                if (syncFolderInfo.LocalPath.Equals(syncher.SyncFolderInfo.LocalPath))
                {
                    StopAndRemoveSyncFolderSyncronization(syncher);
                    found = true;
                    break;
                }
            }

            if (found == false) {
                Logger.Warn("StopAndRemoveSyncFolderSyncronization(Config.SyncConfig.SyncFolder) cant find a SyncFolderSyncronizer for the provided SyncFolder (" + syncFolderInfo + "). The configuration will be removed from the CurrentConfig, but might be some leftover (local files, database, ecc...)");
                ConfigManager.CurrentConfig.RemoveSyncFolder(syncFolderInfo);
            }
        }

        public void StopAndRemoveSyncFolderSyncronization(SyncFolderSyncronizer syncFolderSyncronizer)
        {
            bool keepLocalFiles;
            MessageBoxResult result = MessageBox.Show("Do you want to also remove local files?", "Local Files", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                keepLocalFiles = false;
            }else if(result == MessageBoxResult.No) {
                keepLocalFiles = true;
            }else{
                //abort
                return;
            }

            syncFolderSyncronizer.CancelSync();
            syncFolderSyncronizer.deleteResources(keepLocalFiles);
            syncFolderSyncronizer.Dispose();
            this.SyncFolders.Remove(syncFolderSyncronizer);
            Logger.Info("Removed Repository: " + syncFolderSyncronizer.SyncFolderInfo.DisplayName);
        }

        /// <summary>
        /// Create a new CmisSync synchronized folder.
        /// </summary>
        public void AddAndStartNewSyncFolderSyncronization(Config.SyncConfig.SyncFolder syncFolderInfo)
        {
            if (syncFolderInfo.CmisDatabasePath == null) { 
                syncFolderInfo.CmisDatabasePath = ConfigManager.CurrentConfig.ConfigurationDirectoryPath;
            }
            //TODO: check repoInfo data

            checkDefaultRepositoryStncFolder();

            // Check that the folder don't exists.
            if (Directory.Exists(syncFolderInfo.LocalPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Repository Folder {0} already exist", syncFolderInfo.LocalPath));
                throw new UnauthorizedAccessException("Repository folder already exists!");
            }

            Directory.CreateDirectory(syncFolderInfo.LocalPath);

            // Add folder to XML config file.
            ConfigureAndStartSyncFolderSyncronization(syncFolderInfo);

            ConfigManager.CurrentConfig.AddSyncFolder(syncFolderInfo);
        }

        private void checkDefaultRepositoryStncFolder()
        {
            // Check that the CmisSync root folder exists.
            if (!Directory.Exists(ConfigManager.CurrentConfig.DefaultSyncFolderRootFolderPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} does not exist", ConfigManager.CurrentConfig.DefaultSyncFolderRootFolderPath));
                throw new DirectoryNotFoundException("Root folder don't exist !");
            }
            // Check that the folder is writable.
            if (!CmisSync.Lib.SyncUtils.HasWritePermissionOnDir(ConfigManager.CurrentConfig.DefaultSyncFolderRootFolderPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} is not writable", ConfigManager.CurrentConfig.DefaultSyncFolderRootFolderPath));
                throw new UnauthorizedAccessException("Root folder is not writable!");
            }
        }

        internal void ShutdownApplication()
        {
            Application.Current.Shutdown();
        }

        internal void createAndShowNewSyncFolder()
        {
            Config.SyncConfig.SyncFolder repoInfo = new Config.SyncConfig.SyncFolder();
            Views.SyncFolderWizardWindow w = new Views.SyncFolderWizardWindow(new ViewModels.SyncFolderWizardViewModel(this, repoInfo));
            w.Show();
        }

        internal CmisSync.Lib.Config.SyncConfig.Account createNewAccount()
        {
            CmisSync.Lib.Config.SyncConfig.Account account = new Config.SyncConfig.Account();
            Views.AccountWindow w = new Views.AccountWindow(new ViewModels.AccountViewModel(this, account));
            w.ShowDialog();
            return account;
        }

        internal void editAccount(CmisSync.Lib.Config.SyncConfig.Account account)
        {
            if (account == null) {
                return;
            }

            Views.AccountWindow w = new Views.AccountWindow(new ViewModels.AccountViewModel(this, account));
            w.ShowDialog();
        }

        internal bool deleteAccount(Config.SyncConfig.Account account)
        {
            if (account == null)
            {
                return false;
            }

            if (account.SyncFolders.Count != 0) {
                MessageBox.Show("There are " + account.SyncFolders.Count + " synced folders commected to this account, remove them first.", "Unable to delete active Account");
                return false;
            }

            MessageBoxResult result = MessageBox.Show("Are you sure you want to remove the account '" + account.DisplayName + "' (pointing to '"+(account.RemoteUrl!=null?account.RemoteUrl.ToString():"")+"')", "Delete Account", MessageBoxButton.YesNoCancel);
            if (result == MessageBoxResult.Yes)
            {
                ConfigManager.CurrentConfig.Accounts.Remove(account);
                ConfigManager.CurrentConfig.Save();
                return true;
            }
            else {
                return false;
            }
        }

        internal string browseLocalPath(string LocalPath)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            String path = dialog.SelectedPath;
            dialog.Dispose();
            return path;
        }

        internal void SyncNow(SyncFolderSyncronizer syncFolderSyncronizer)
        {
            Thread t = new Thread(() =>
            {
                syncFolderSyncronizer.Sync();
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        internal void ShowEventsWindow(SyncFolderSyncronizerViewModel model)
        {
            SyncronizerEventsWindow w = new SyncronizerEventsWindow();
            w.DataContext = model;
            w.Show();
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                notifyIcon.Dispose();
            }
        }

        #endregion
    }
}
