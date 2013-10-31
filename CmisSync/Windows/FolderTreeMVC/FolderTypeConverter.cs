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
    /// Converter for FolderType to Color convertion
    /// </summary>
    [ValueConversion(typeof(Folder.FolderType), typeof(Brush))]
    public class FolderTypeToBrushConverter : IValueConverter
    {
        private Brush noneFolderBrush = Brushes.Red;
        /// <summary>
        /// Color of FolderType.NONE
        /// </summary>
        public Brush NocalFolderBrush { get { return noneFolderBrush; } set { localFolderBrush = value; } }
        private Brush localFolderBrush = Brushes.Gray;
        /// <summary>
        /// Color of FolderType.LOCAL
        /// </summary>
        public Brush LocalFolderBrush { get { return localFolderBrush; } set { localFolderBrush = value; } }
        private Brush remoteFolderBrush = Brushes.Blue;
        /// <summary>
        /// Color of FolderType.REMOTE
        /// </summary>
        public Brush RemoteFolderBrush { get { return remoteFolderBrush; } set { remoteFolderBrush = value; } }
        private Brush bothFolderBrush = Brushes.Green;
        /// <summary>
        /// Color of FolderType.BOTH
        /// </summary>
        public Brush BothFolderBrush { get { return bothFolderBrush; } set { bothFolderBrush = value; } }
        /// <summary>
        /// Converts the given FolderType to a Brush with the selected color
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Folder.FolderType type = (Folder.FolderType)value;
            switch (type)
            {
                case Folder.FolderType.NONE:
                    return noneFolderBrush;
                case Folder.FolderType.LOCAL:
                    return localFolderBrush;
                case Folder.FolderType.REMOTE:
                    return remoteFolderBrush;
                case Folder.FolderType.BOTH:
                    return bothFolderBrush;
                default:
                    return Brushes.White;
            }
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
