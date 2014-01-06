using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;

namespace CmisSync
{
    /// <summary>
    /// Slider to tune the poll interval.
    /// </summary>
    class PollIntervalSlider : Slider
    {
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(PollIntervalSlider));


        /// <summary>
        /// Tooltip that shows poll interval  when mouse goes over the slider.
        /// </summary>
        private ToolTip _autoToolTip;


        /// <summary>
        /// Poll interval shown/tuned by the slider.
        /// </summary>
        public int PollInterval {
            get {
                return (int)this.Value * 1000;
            }
            set {
                this.Value = value / 1000;
            }
        }


        /// <summary>
        /// Constructor.
        /// </summary>
        public PollIntervalSlider()
        {
            this.IsSnapToTickEnabled = true;
            this.Minimum = 5;
            this.Maximum = 60 * 60 * 24;
            this.TickPlacement = TickPlacement.BottomRight;
            this.AutoToolTipPlacement = AutoToolTipPlacement.BottomRight;

            // Add ticks to the slider.
            DoubleCollection tickMarks = new DoubleCollection();
            tickMarks.Add(5); // 5 seconds
            tickMarks.Add(60);
            tickMarks.Add(60 * 2); // 2 minutes
            tickMarks.Add(60 * 3);
            tickMarks.Add(60 * 5);
            tickMarks.Add(60 * 10);
            tickMarks.Add(60 * 15);
            tickMarks.Add(60 * 30);
            tickMarks.Add(60 * 60);
            tickMarks.Add(60 * 60 * 2); // 2 hours
            tickMarks.Add(60 * 60 * 4);
            tickMarks.Add(60 * 60 * 8);
            tickMarks.Add(60 * 60 * 12);
            tickMarks.Add(60 * 60 * 24);
            this.Ticks = tickMarks;
        }



        protected override void OnThumbDragStarted(DragStartedEventArgs e)
        {
            base.OnThumbDragStarted(e);
            this.FormatAutoToolTipContent();
        }


        protected override void OnThumbDragDelta(DragDeltaEventArgs e)
        {
            base.OnThumbDragDelta(e);
            this.FormatAutoToolTipContent();
        }


        public string FormattedMaximum()
        {
            return FormatToolTip((int)this.Maximum);
        }


        public string FormattedMinimum()
        {
            return FormatToolTip((int)this.Minimum);
        }


        private string FormatToolTip(int value)
        {
            TimeSpan timeSpan = new TimeSpan(0, 0, value);
            if (value < 60)
            {
                return timeSpan.ToString("%s") + " " + Properties_Resources.Seconds;
            }
            else if (value == 60)
            {
                return timeSpan.ToString("%m") + " " + Properties_Resources.Minute;
            }
            else if (value < 60 * 60)
            {
                return timeSpan.ToString("%m") + " " + Properties_Resources.Minutes;
            }
            else if (value == 60 * 60)
            {
                return timeSpan.ToString("%h") + " " + Properties_Resources.Hour;
            }
            else if (value < 60 * 60 * 24)
            {
                return timeSpan.ToString("%h") + " " + Properties_Resources.Hours;
            }
            else
            {
                return timeSpan.ToString("%d") + " " + Properties_Resources.Day;
            }
        }


        private void FormatAutoToolTipContent()
        {
            this.AutoToolTip.Content = FormatToolTip((int)this.Value);
        }


        private ToolTip AutoToolTip
        {
            get
            {
                if (_autoToolTip == null)
                {
                    FieldInfo field = typeof(Slider).GetField(
                        "_autoToolTip", BindingFlags.NonPublic | BindingFlags.Instance);
                    _autoToolTip = field.GetValue(this) as ToolTip;
                }
                return _autoToolTip;
            }
        }
    }


    public class LogScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double x = (int)value;
            return Math.Log(x);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double x = (double)value;
            return (int)Math.Exp(x);
        }
    }
}
