using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CmisSync.Views.Converters
{
    [ValueConversion(typeof(int), typeof(string))]
    class IntToPollIntervalConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int) {
                int seconds = ((int)value) / 1000;

                int h = seconds / (60*60);
                if (h > 0) {
                    return h + " hours";
                }
                int m = seconds / (60);
                if (m > 0)
                {
                    return m + " minutes";
                }
                return seconds + "seconds";
            }
            else 
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string interval = (string)value;
            Regex regex = new Regex("(\\d+) (hour|minute|second)s?");
            Match match = regex.Match(interval);
            if (match != null)
            {
                if(match.Groups[2].Value.Equals("hour"))
                {
                    return Int32.Parse(match.Groups[1].Value) * 60 * 60 * 1000;
                        
                }
                else if (match.Groups[2].Value.Equals("minute"))
                {
                    return Int32.Parse(match.Groups[1].Value) * 60 * 1000;
                }
                else if (match.Groups[2].Value.Equals("second"))
                {
                    return Int32.Parse(match.Groups[1].Value) * 1000;
                }
                else {
                    return new ValidationResult("Invalid format");
                }
            }
            else
            {
                return new ValidationResult("Invalid format");
            }           
        }

        #endregion
    }
}
