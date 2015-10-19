using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmisSync.Utils;
using System.IO;

namespace CmisSync.ViewModels.SyncFolderWizard
{
    class LocalPageViewModel : WizardPageViewModelBase
    {
        private SyncFolderWizardViewModel model;

        public LocalPageViewModel(Controller controller, SyncFolderWizardViewModel model)
            : base(controller)
        {
            Header = "Local";
            Subtitle = "Select the local folder";

            this.model = model;
            updateNextStatus();
            PropertyChanged += model_PropertyChanged;

            InitializeCommand = new Utils.RelayCommand(init);

        }

        private void updateNextStatus()
        {
            validate();
            AllowFinish = String.IsNullOrEmpty(Error);
        }

        void model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("LocalPath"))
            {
                updateNextStatus();
            }
        }

        private void init()
        {
            LocalPath = Path.Combine(
                    CmisSync.Lib.ConfigManager.CurrentConfig.DefaultSyncFolderRootFolderPath,
                    model.DisplayName);
        }

        internal void validate()
        {
            Error = ValidateLocalPath();
        }

        internal string ValidateLocalPath()
        {
            if (String.IsNullOrEmpty(LocalPath))
            {
                return "The local path should not be empty";
            }
            else if (System.IO.Directory.Exists(LocalPath))
            {
                return "The selected folder already exist, plese delete it or select a new one";
            }
            else
            {
                return String.Empty;
            }
        }

        public String DisplayName
        {
            get { return model.DisplayName; }
            set
            {

                if (LocalPath != null && LocalPath.EndsWith(model.DisplayName))
                {
                    String dir = LocalPath.Substring(0, LocalPath.Length - model.DisplayName.Length - 1);
                    LocalPath = Path.Combine(dir, value);
                }
                model.DisplayName = value;
                NotifyOfPropertyChanged("DisplayName");
            }
        }

        public string LocalPath
        {
            get { return model.LocalPath; }
            set
            {
                model.LocalPath = value;
                NotifyOfPropertyChanged("LocalPath");
            }
        }

        private string _error;
        public String Error { get { return _error; } internal set { _error = value; NotifyOfPropertyChanged("Error"); } }

        public RelayCommand browseLocalPathCommand { get { return new RelayCommand(browseLocalPath); } }
        private void browseLocalPath()
        {
            string path = Controller.browseLocalPath(LocalPath);
            if (!string.IsNullOrEmpty(path))
            {
                LocalPath = Path.Combine(path, DisplayName);
            }
        }

    }
}
