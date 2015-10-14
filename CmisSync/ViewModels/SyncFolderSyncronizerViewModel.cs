using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmisSync.Utils;
using CmisSync.Lib;
using System.Collections.ObjectModel;
using DotCMIS.Exceptions;
using CmisSync.Lib.Sync;
using System.Windows;

namespace CmisSync.ViewModels
{
    public class SyncFolderSyncronizerViewModel : ViewModelBase, Utils.MVVMWrapper.IItemWrapper<CmisSync.Lib.Sync.SyncFolderSyncronizer>
    {
        private readonly CmisSync.Lib.Sync.SyncFolderSyncronizer model;

        /// <summary>
        /// Only For DesignTime
        /// </summary>
        public SyncFolderSyncronizerViewModel()
            : base(null)
        {
        }

        public SyncFolderSyncronizerViewModel(Controller controller, CmisSync.Lib.Sync.SyncFolderSyncronizer model) : base(controller)
        {
            this.model = model;

            this.model.PropertyChanged += model_PropertyChanged;            
        }

        private void model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender == this.model)
            {
                switch (e.PropertyName)
                {
                    case "RepoInfo":
                        if (this.model.SyncFolderInfo != null) {
                            this.model.SyncFolderInfo.PropertyChanged += model_PropertyChanged;
                        }
                        NotifyOfPropertyChanged(e.PropertyName);
                        break;
                    case "Status":
                    case "Events":
                        NotifyOfPropertyChanged(e.PropertyName);
                        break;
                }
            }
            else if (sender == this.model.SyncFolderInfo) {
                switch (e.PropertyName)
                {
                    case "DisplayName":
                        NotifyOfPropertyChanged(e.PropertyName);
                        break;
                }
            }
        }       

        #region Properties

        public String DisplayName {
            get {
                return model.SyncFolderInfo.DisplayName;
            }
        }

        public SyncStatus Status
        {
            get { return model.Status; }
        }

        public EventsObservableCollection Events
        {
            get { return model.Events; }
        }

        #endregion

        #region Commands

        public RelayCommand OpenLocalFolderCommand { get { return new RelayCommand(openLocalFolder); } }
        public void openLocalFolder()
        {
            Files.OpenFolder(model.SyncFolderInfo.LocalPath);
        }

        public RelayCommand OpenRemoteFolderCommand { get { return new RelayCommand(openRemoteFolder); } }
        public void openRemoteFolder()
        {
            Files.OpenRemoteFolder(model.SyncFolderInfo);
        }

        public RelayCommand SuspendSyncCommand { get { return new RelayCommand(suspendSync, canSuspendSync); } }
        public void suspendSync()
        {
            model.Suspend(true);
        }
        public bool canSuspendSync()
        {
            return model.Status == SyncStatus.Idle || model.Status == SyncStatus.Syncing;
        }

        public RelayCommand ResumeSyncCommand { get { return new RelayCommand(resumeSync, canResumeSync); } }
        public void resumeSync()
        {
            model.Resume();
        }
        public bool canResumeSync()
        {
            return model.Status == SyncStatus.Idle_Suspended || model.Status == SyncStatus.Syncing_Suspended;
        }

        public RelayCommand RemoveCommand { get { return new RelayCommand(remove); } }
        public void remove()
        {
            Controller.StopAndRemoveSyncFolderSyncronization(this.model);
        }

        public RelayCommand SyncNowCommand { get { return new RelayCommand(syncNow); } }
        public void syncNow()
        {
            model.SyncInBackground();
        }

        public RelayCommand OpenSettingsCommand { get { return new RelayCommand(openSettings); } }
        public void openSettings()
        {
            throw new NotImplementedException();
        }

        public RelayCommand ShowEventsCommand { get { return new RelayCommand(showEvents); } }
        public void showEvents()
        {
            Controller.ShowEventsWindow(this);
        }

        public RelayCommand<SyncronizerEvent> CopyRowCommand { get { return new RelayCommand<SyncronizerEvent>((o) => copyRow(o)); } }
        public void copyRow(SyncronizerEvent e)
        {
            Clipboard.SetText(
                "Date:         " + e.Date + "\n" +
                "Level:        "+ e.Level + "\n" +
                "SyncFolder:   " + e.SyncFolderInfo.DisplayName + "\n" +
                "Account:      " + e.SyncFolderInfo.Account.DisplayName + "\n" + 
                "LocalFolder:  " + e.SyncFolderInfo.LocalPath + "\n" +
                "RemoteFolder: " + e.SyncFolderInfo.RemotePath + "\n" + 
                "Message:      " + e.Exception.Message + "\n" + 
                "Exception:    " + e.Exception);
        }

        #endregion 

        public bool IsItemWrapper(CmisSync.Lib.Sync.SyncFolderSyncronizer item)
        {
            return item == model;
        }
    }
}
