using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmisSync.ViewModels.SyncFolderWizard
{
    class RemotePageViewModel : WizardPageViewModelBase
    {
        private SyncFolderWizardViewModel model;
        private RemotePage.FolderViewModel _remoteFolder;

        public RemotePageViewModel(Controller controller, SyncFolderWizardViewModel model)
            : base(controller)
        {
            Header = "Remote";
            Subtitle = "Select the remote folder";

            this.model = model;
            updateNextStatus();
            this.model.PropertyChanged += model_PropertyChanged;

            CommitCommand = new Utils.RelayCommand(setDefaultDisplayName);
        }

        private void model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("RemoteFolder")) {
                updateNextStatus();
            }
        }

        private void updateNextStatus()
        {
            AllowNext = model.RepositoryId != null && model.RemotePath != null;
        }

        private void setDefaultDisplayName()
        {
            model.DisplayName = CmisSync.Lib.SyncUtils.SyncedFolderDefaultNameFromPath(_remoteFolder.Path);
        }

        public ReadOnlyCollection<RemotePage.RepositoryViewModel> RemoteFolders
        {
            get
            {
                //TODO: should not convert from model to viewmodel here
                List<RemotePage.RepositoryViewModel> list = new List<RemotePage.RepositoryViewModel>();
                foreach (CmisSync.Lib.Config.SyncConfig.RemoteRepository repo in model.Account.RemoteRepositories) {
                    list.Add(new RemotePage.RepositoryViewModel(repo));
                }
                //expand the repo if it's the only one
                if (list.Count == 1) {
                    list[0].IsExpanded = true;
                }
                return new ReadOnlyCollection<RemotePage.RepositoryViewModel>(list);
            }
        }

        public RemotePage.FolderViewModel RemoteFolder
        {
            set { model.RemoteFolder = value; _remoteFolder = value; }
        }
    }
}
