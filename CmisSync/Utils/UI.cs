using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CmisSync.Utils
{
    class UI
    {
        public static void runInUiThread(Action action) {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(action));
        }
    }
}
