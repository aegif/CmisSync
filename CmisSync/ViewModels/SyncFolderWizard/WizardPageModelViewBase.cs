using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmisSync.ViewModels.SyncFolderWizard
{
    class WizardPageViewModelBase : AvalonWizard.Mvvm.WizardPageViewModelBase
    {
        private Controller _controller;
        protected Controller Controller { get{ return _controller;} }

        public WizardPageViewModelBase(Controller controller)
        {
            if (controller == null) {
                throw new System.InvalidOperationException();
            }
            this._controller = controller;
        }
    }
}
