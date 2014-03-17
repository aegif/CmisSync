using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;
using System.Globalization;


namespace CmisSync.CmisTree
{
    /// <summary>
    /// Converter of the ignore Status of a folder which returns a string, discribing the ignore status
    /// </summary>
    [ValueConversion(typeof(bool), typeof(string))]
    public class IgnoreStatusToTextConverter : IValueConverter
    {
        /// <summary>
        /// Converts the given ignore status into a text message
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((bool)value) ? Properties_Resources.DoNotIgnoreFolder : Properties_Resources.IgnoreFolder;
        }
        /// <summary>
        /// Is not supported
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        { throw new NotSupportedException(); }
    }

    /// <summary>
    /// Converter to convert the ignore status to a text decoration
    /// </summary>
    [ValueConversion(typeof(bool), typeof(TextDecorations))]
    public class IgnoreToTextDecorationConverter : IValueConverter
    {
        /// <summary>
        /// Converts the ignore status to a text decoration
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool ignored = (bool)value;
            if (ignored)
                return TextDecorations.Strikethrough;
            else
                return null;
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        { throw new NotSupportedException(); }
    }
}
