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
    public class SyncFolderWizardViewModel : SyncFolderViewModel
    {
        public SyncFolderWizardViewModel(Controller controller, Config.SyncConfig.SyncFolder model)
            : base(controller, model)
        {
            Pages = new List<Object>
            {
                new ViewModels.SyncFolderWizard.AccountPageViewModel(Controller, this),
                new ViewModels.SyncFolderWizard.RemotePageViewModel(Controller, this),
                new ViewModels.SyncFolderWizard.LocalPageViewModel(Controller, this)
            };
        }

        public IList<Object> Pages { get; private set; }
        
        internal void Finished(Window window)
        {
            Controller.AddAndStartNewSyncFolderSyncronization(Model);
            window.Close();
        }
    }
}
