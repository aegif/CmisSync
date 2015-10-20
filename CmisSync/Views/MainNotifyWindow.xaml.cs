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
using ExtendedWindowsControls;
using System.Collections.ObjectModel;

namespace CmisSync.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainNotifyWindow : Window
    {
        public MainNotifyWindow(ViewModels.ControllerViewModel cmisSyncViewModel) : base()
        {
            this.DataContext = cmisSyncViewModel;

            InitializeComponent();

            // Set the startup position and the startup state to "not visible"
            SetWindowToBottomRightOfScreen();
            this.Hide();
        }

        /// <summary>
        /// Does what it says on the tin - ensures the popup window appears at the bottom right of the screen, just above the task bar
        /// </summary>
        private void SetWindowToBottomRightOfScreen()
        {
            Left = SystemParameters.WorkArea.Width - Width - 1;
            Top = SystemParameters.WorkArea.Height - Height;
        }

        private void menuButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            ContextMenu contextMenu = button.ContextMenu;
            contextMenu.PlacementTarget = button;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            contextMenu.IsOpen = !contextMenu.IsOpen;
        }
    }
}
