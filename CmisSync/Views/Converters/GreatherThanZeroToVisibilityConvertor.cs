using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace CmisSync.Views.Converters
{
    [ValueConversion(typeof(int), typeof(Visibility))]
    public class GreatherThanZeroToVisibilityConvertor : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value != null)
            {
                try
                {
                    if (((int)value) > 0)
                    {
                        return Visibility.Visible;
                    }
                }
                catch (InvalidCastException)
                {
                    return Visibility.Collapsed;
                }

            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
