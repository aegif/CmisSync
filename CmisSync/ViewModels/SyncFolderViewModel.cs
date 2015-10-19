using CmisSync.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmisSync.ViewModels
{
    public class SyncFolderViewModel : ViewModelBase
    {
        private Config.SyncConfig.SyncFolder model;

        public SyncFolderViewModel(Controller controller, Config.SyncConfig.SyncFolder model)
            : base(controller)
        {
            this.model = model;
        }

        protected Config.SyncConfig.SyncFolder Model { get { return model; } }

        public String DisplayName
        {
            get { return model.DisplayName; }
            set { model.DisplayName = value; OnPropertyChanged("DisplayName"); }
        }

        public CmisSync.Lib.Config.SyncConfig.Account Account
        {
            get { return model.Account; }
            set { model.Account = value; OnPropertyChanged("Account"); }
        }

        public SyncFolderWizard.RemotePage.FolderViewModel RemoteFolder
        {
            set {
                model.RepositoryId = value.Repository.Id;
                model.RemotePath = value.Path;
                OnPropertyChanged("RemoteFolder");
            }
        }

        public string RepositoryId { get { return model.RepositoryId; } }

        public string RemotePath { get { return model.RemotePath; } }

        public string LocalPath {
            get { return model.LocalPath; }
            set { model.LocalPath = value; OnPropertyChanged("LocalPath"); }
        }

        public DateTime LastSuccessedSync
        {
            get { return model.LastSuccessedSync; }
        }

        public bool SyncAtStartup
        {
            get { return model.SyncAtStartup; }
            set { model.SyncAtStartup = value; }
        }

        public int PollInterval
        {
            get { return model.PollInterval; }
            set 
            {
                if(model.PollInterval != value)
                {
                    model.PollInterval = value;
                    OnPropertyChanged("PollInterval");
                }
            }
        }

        protected override string validate(string columnName)
        {
            return String.Empty;          
        }
    }
}
