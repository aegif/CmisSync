using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmisSync.Lib;

namespace CmisSync.ViewModels.SyncFolderWizard.RemotePage
{
    public class RepositoryViewModel : FolderViewModel
    {
        public RepositoryViewModel(Config.SyncConfig.RemoteRepository repo) : base(null, repo)
        {
        }
    }
}
