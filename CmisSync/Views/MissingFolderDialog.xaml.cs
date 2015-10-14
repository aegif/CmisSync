using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CmisSync.Lib;
using CmisSync.Utils;

namespace CmisSync.Views
{
    /// <summary>
    /// Interaction logic for MissingFolderDialog.xaml
    /// </summary>
    public partial class MissingFolderDialog : Window
    {
        public enum Action {
            MOVE,
            REMOVE,
            RECREATE
        }

        public RelayCommand selectNewCommand { get; internal set; }
        public RelayCommand removeFromSyncCommand { get; internal set; }
        public RelayCommand resyncCommand { get; internal set; }

        public Config.SyncConfig.SyncFolder repo { get; set; }

        public MissingFolderDialog(Config.SyncConfig.SyncFolder repo)
        {
            this.repo = repo;

            selectNewCommand = new RelayCommand(selectNew);
            removeFromSyncCommand = new RelayCommand(removeFromSync);
            resyncCommand = new RelayCommand(resync);
            
            InitializeComponent();
        }

        public void selectNew() {
            this.Result = Action.MOVE;
            Close();
        }

        public void removeFromSync()
        {
            this.Result = Action.REMOVE;
            Close();
        }

        public void resync()
        {
            this.Result = Action.RECREATE; 
            Close();
        }

        public Action Result { get; internal set; }
    }


}
