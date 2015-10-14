using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmisSync.Utils;

namespace CmisSync.ViewModels.SyncFolderWizard
{
    class AccountPageViewModel : WizardPageViewModelBase
    {
        private SyncFolderWizardViewModel model;

        public AccountPageViewModel(Controller controller, SyncFolderWizardViewModel model) : base(controller)
        {

            this.model = model;
            Header = "Account";
            Subtitle = "Select the account";
            
            model.PropertyChanged += model_PropertyChanged;
            updateNextStatus();
        }

        private void model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("Account")) {
                updateNextStatus();
            }
        }

        private void updateNextStatus()
        {
            this.AllowNext = (model.Account != null);
        }

        public ObservableCollection<CmisSync.Lib.Config.SyncConfig.Account> Accounts
        {
            get
            {
                return CmisSync.Lib.ConfigManager.CurrentConfig.Accounts;
            }
        }

        public Utils.RelayCommand newAccountCommand { get { return new Utils.RelayCommand(newAccount); } }
        public Utils.RelayCommand editAccountCommand { get { return new Utils.RelayCommand(editAccount); } }
        public Utils.RelayCommand deleteAccountCommand { get { return new Utils.RelayCommand(deleteAccount); } }

        public void newAccount()
        {
            model.Account = Controller.createNewAccount();
        }

        public void editAccount()
        {
            Controller.editAccount(model.Account);
        }

        public void deleteAccount()
        {
            if (Controller.deleteAccount(model.Account))
            {
                model.Account = null;
            }
        }
    }
}
