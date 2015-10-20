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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CmisSync.Views.RepositoryWizard
{
    /// <summary>
    /// Interaction logic for RemotePage.xaml
    /// </summary>
    public partial class RemotePage : UserControl
    {
        public RemotePage()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ((ViewModels.SyncFolderWizard.RemotePageViewModel)DataContext).RemoteFolder = (ViewModels.SyncFolderWizard.RemotePage.FolderViewModel)e.NewValue;
        }
    }
}
