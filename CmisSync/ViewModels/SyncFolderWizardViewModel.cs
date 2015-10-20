using System;
using System.Windows;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvalonWizard;
using AvalonWizard.Mvvm;
using CmisSync.Lib;

namespace CmisSync.ViewModels
{
    public class SyncFolderWizardViewModel : ViewModelBase
    {
        private Config.SyncConfig.SyncFolder SyncFolderInfo;

        public SyncFolderWizardViewModel(Controller controller, Config.SyncConfig.SyncFolder repoInfo)
            : base(controller)
        {
            this.SyncFolderInfo = repoInfo;

            Pages = new List<Object>
            {
                new ViewModels.SyncFolderWizard.AccountPageViewModel(Controller, this),
                new ViewModels.SyncFolderWizard.RemotePageViewModel(Controller, this),
                new ViewModels.SyncFolderWizard.LocalPageViewModel(Controller, this)
            };
        }

        public IList<Object> Pages
        {
            get;
            private set;
        }

        public String DisplayName
        {
            get { return SyncFolderInfo.DisplayName; }
            set { SyncFolderInfo.DisplayName = value; NotifyOfPropertyChanged("DisplayName"); }
        }

        public CmisSync.Lib.Config.SyncConfig.Account Account
        {
            get { return SyncFolderInfo.Account; }
            set
            {
                SyncFolderInfo.Account = value;
                NotifyOfPropertyChanged("Account");
            }
        }

        public SyncFolderWizard.RemotePage.FolderViewModel RemoteFolder
        {
            set {
                SyncFolderInfo.RepositoryId = value.Repository.Id;
                SyncFolderInfo.RemotePath = value.Path;
                NotifyOfPropertyChanged("RemoteFolder");
            }

        }

        public string RepositoryId { get { return SyncFolderInfo.RepositoryId; } }

        public string RemotePath { get { return SyncFolderInfo.RemotePath; } }

        public string LocalPath {
            get { return SyncFolderInfo.LocalPath; }
            set { SyncFolderInfo.LocalPath = value; NotifyOfPropertyChanged("LocalPath"); }
        }


        protected override string validate(string columnName)
        {
            return String.Empty;          
        }

        internal void Finished(Window window)
        {
            Controller.AddAndStartNewSyncFolderSyncronization(SyncFolderInfo);
            window.Close();
        }
    }
}
