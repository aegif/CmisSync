using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using CmisSync.Lib;
using CmisSync.Lib.Auth;
using CmisSync.Utils;

namespace CmisSync.ViewModels
{
    public class AccountViewModel : ViewModelBase
    {

        private Config.SyncConfig.Account _account;

        public AccountViewModel(Controller controller)
            : base(controller)
        {
        }

        public AccountViewModel(Controller controller, Config.SyncConfig.Account account)
            : this(controller)
        {
            this._account = account;
        }

        #region properties

        public String DisplayName
        {
            get { return _account.DisplayName; }
            set { _account.DisplayName = value; NotifyOfPropertyChanged("DisplayName"); }
        }

        public String ServerUrl
        {
            get
            {
                return _account.RemoteUrl != null ? _account.RemoteUrl.ToString() : null;
            }
            set
            {
                try
                {
                    _account.RemoteUrl = new Uri(value);
                }
                catch (UriFormatException e)
                {
                    errors.Add("ServerUrl", e.Message);
                    throw;
                } 
                NotifyOfPropertyChanged("ServerUrl");
            }
        }

        public String UserName
        {
            get
            {
                if (_account.Credentials != null)
                {
                    return _account.Credentials.UserName;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (_account.Credentials == null)
                {
                    _account.Credentials = new CmisSync.Lib.Auth.UserCredentials();
                }
                _account.Credentials.UserName = value;
                NotifyOfPropertyChanged("UserName");
            }
        }

        public String Password
        {
            get
            {
                if (_account.Credentials != null)
                {
                    //return a mock password to the gui
                    return "".PadLeft(_account.Credentials.Password.ToString().Length, '*');
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (_account.Credentials == null)
                {
                    _account.Credentials = new CmisSync.Lib.Auth.UserCredentials();
                }
                _account.Credentials.Password = new CmisSync.Lib.Auth.Password(value);
                NotifyOfPropertyChanged("Password");
            }
        }

        #endregion properties

        #region commands

        public RelayCommand<Window> saveCommand { get { return new RelayCommand<Window>(param => this.save(param), param => isValid()); } }
        public RelayCommand<Window> cancelCommand { get { return new RelayCommand<Window>(param => this.cancel(param)); } }

        #endregion commands

        private void save(Window window)
        {
            try
            {
                CmisSync.Lib.Cmis.CmisUtils.TestAccount(_account);

                if (!ConfigManager.CurrentConfig.Accounts.Contains(_account))
                {
                    ConfigManager.CurrentConfig.Accounts.Add(_account);
                }
                ConfigManager.CurrentConfig.Save();
                window.Close();
            }catch(Exception){
                MessageBox.Show("Unable to obtain the corrct url or wrong credentials.", "Error");
            }
        }

        private void cancel(Window window)
        {
            window.Close();
        }

        protected override string validate(string columnName)
        {
            switch (columnName)
            {
                case "DisplayName":
                    return (this.DisplayName != null && this.DisplayName.Length >= 3) ? String.Empty : "Should be at least 3 characters";
                case "ServerUrl":
                    return this.ServerUrl != null ? String.Empty : "Insert a valid uri";
                case "UserName":
                case "Password":
                    return String.Empty;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
