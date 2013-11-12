using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace CmisSync.CmisTree
{
    /// <summary>
    /// Converter to convert the invalid Character as italic font style
    /// </summary>
    [ValueConversion(typeof(bool), typeof(FontStyle))]
    public class InvalidPatternConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool ignored = (bool)value;
            if (ignored)
                return FontStyles.Italic;
            else
                return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
