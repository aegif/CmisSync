using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using CmisSync.Lib;
using CmisSync.Lib.Sync;

namespace CmisSync.Views.Converters
{
    [ValueConversion(typeof(SyncStatus), typeof(string))]
    class SyncStatusToIconPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch ((SyncStatus)value)
            {
                case SyncStatus.Idle:
                    return "/CmisSync;component/Resources/idle.gif";
                case SyncStatus.Syncing:
                    return "/CmisSync;component/Resources/sync.gif";
                case SyncStatus.Idle_Suspended:
                case SyncStatus.Syncing_Suspended:
                    return "/CmisSync;component/Resources/pause.gif";
                default:
                    throw new ArgumentException();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

}