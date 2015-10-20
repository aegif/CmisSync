using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmisSync.ViewModels.SyncFolderWizard.RemotePage
{
    public class LoadingTreeViewItemViewModel : TreeViewItemViewModel
    {
        public LoadingTreeViewItemViewModel() : base(null,false) { }

        protected override void LoadChildren() { }
    }
}
