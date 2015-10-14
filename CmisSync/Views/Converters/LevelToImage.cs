using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CmisSync.Lib.Sync;

namespace CmisSync.Views.Converters
{
    [ValueConversion(typeof(EventLevel), typeof(string))]
    public class LevelToImagePath : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            EventLevel level = (EventLevel)value;
            switch (level)
            {
                case EventLevel.ERROR:
                    return "/CmisSync;component/Resources/error_16.png";
                case EventLevel.WARN:
                    return "/CmisSync;component/Resources/warn_16.png";
                case EventLevel.INFO:
                    return "/CmisSync;component/Resources/info_16.png";
                default:
                    throw new ArgumentException();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
