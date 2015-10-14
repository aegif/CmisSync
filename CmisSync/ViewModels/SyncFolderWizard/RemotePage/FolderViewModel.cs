using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmisSync.Lib;

namespace CmisSync.ViewModels.SyncFolderWizard.RemotePage
{
    public class FolderViewModel : TreeViewItemViewModel
    {
        private Config.SyncConfig.RemoteFolder folder;

        public FolderViewModel(FolderViewModel parent, Config.SyncConfig.RemoteFolder folder)
            : base(parent, true)
        {
            this.folder = folder;
        }

        public string Name
        {
            get { return folder.Name; }
        }

        public string Path
        {
            get { return folder.Path; }
        }

        public Config.SyncConfig.RemoteRepository Repository { get { return folder.Repository; } }

        protected override void LoadChildren()
        {
            ObservableCollection<TreeViewItemViewModel> list = new ObservableCollection<TreeViewItemViewModel>();
            foreach (Config.SyncConfig.RemoteFolder f in folder.Subfolders)
                list.Add(new FolderViewModel(this, f));
            base.Childrens = list;
        }
    }
}
