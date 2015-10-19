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
using CmisSync.Utils;

namespace CmisSync.ViewModels
{
    public class SyncFolderSettingsViewModel : BindingOriented.Adapters.EditableAdapter<SyncFolderViewModel>
    {
        public SyncFolderSettingsViewModel(Controller controller, Config.SyncConfig.SyncFolder model)
            : base(new SyncFolderViewModel(controller, model))
        {

        }

        public RelayCommand<Window> SaveCommand { get { return new RelayCommand<Window>(save, canSave); } }
        public void save(Window window)
        {
            this.EndEdit();
            ConfigManager.CurrentConfig.Save();
            window.Close();
        }
        public bool canSave(Window window) 
        {
            return this.HasChanges;
        }

        public RelayCommand<Window> CancelCommand { get { return new RelayCommand<Window>(cancel); } }
        public void cancel(Window window)
        {
            this.CancelEdit();
            window.Close();
        }
    }
}
