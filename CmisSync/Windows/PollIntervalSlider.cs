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
using System.ComponentModel;

namespace CmisSync
{
    /// <summary>
    /// Slider to tune the poll interval.
    /// </summary>
    class PollIntervalSlider : Slider
    {
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(PollIntervalSlider));

        /// <summary>
        /// External label showing the current value.
        /// </summary>
        private TextBlock sliderLabel;

        /// <summary>
        /// Poll interval shown/tuned by the slider.
        /// </summary>
        public int PollInterval {
            get {
                return 1000 * LogScaleConverter.ConvertBack((int)Value);
            }
            set {
                this.Value = LogScaleConverter.Convert(value);
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public PollIntervalSlider(TextBlock sliderLabel, int defaultValue = 7)
        {
            this.sliderLabel = sliderLabel;

            this.IsSnapToTickEnabled = true;
            this.Minimum = LogScaleConverter.Convert(1000 * 5);
            this.Maximum = LogScaleConverter.Convert(1000 * 60 * 60 * 24);
            this.TickPlacement = TickPlacement.BottomRight;

            // Add ticks to the slider.
            DoubleCollection tickMarks = new DoubleCollection();
            tickMarks.Add(LogScaleConverter.Convert(1000 * 5)); // 5 seconds.
            tickMarks.Add(LogScaleConverter.Convert(1000 * 15));
            tickMarks.Add(LogScaleConverter.Convert(1000 * 30));
            tickMarks.Add(LogScaleConverter.Convert(1000 * 60));
            tickMarks.Add(LogScaleConverter.Convert(1000 * 60 * 3)); // 3 minutes.
            tickMarks.Add(LogScaleConverter.Convert(1000 * 60 * 10));
            tickMarks.Add(LogScaleConverter.Convert(1000 * 60 * 30));
            tickMarks.Add(LogScaleConverter.Convert(1000 * 60 * 60));
            tickMarks.Add(LogScaleConverter.Convert(1000 * 60 * 60 * 3)); // 3 hours.
            tickMarks.Add(LogScaleConverter.Convert(1000 * 60 * 60 * 8));
            tickMarks.Add(LogScaleConverter.Convert(1000 * 60 * 60 * 12));
            tickMarks.Add(LogScaleConverter.Convert(1000 * 60 * 60 * 24));
            this.Ticks = tickMarks;

            // Show current value in UI.
            this.PollInterval = defaultValue;
        }
        protected override void OnValueChanged(double oldValue, double newValue) {
            base.OnValueChanged(oldValue, newValue);

            this.ShowCurrentValue();
        }


        /// <summary></summary>
        /// <returns></returns>
        public string FormattedMaximum()
        {
            return FormatToolTip((int)this.Maximum);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string FormattedMinimum()
        {
            return FormatToolTip((int)this.Minimum);
        }

        private string FormatToolTip(int value)
        {
            value = LogScaleConverter.ConvertBack(value);
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

        private void ShowCurrentValue()
        {
            sliderLabel.Text = FormatToolTip((int)this.Value);
        }
    }

    /// <summary></summary>
    public class LogScaleConverter
    {
        /// <summary></summary>
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(LogScaleConverter));

        /// <summary></summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int Convert(int value)
        {
            return (int)Math.Log((double)value);
        }

        /// <summary></summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int ConvertBack(int value)
        {
            Logger.Debug("ConvertBack " + value + " type:" + value.GetType());
            value = (int)Math.Exp((double)value) / 1000;
            // Because of int rounding, approximation errors appear at exp/log conversion.
            // This fixes the error for our known initial values.
            switch (value)
            {
                case 2:
                    return 5;
                case 8:
                    return 15;
                case 22:
                    return 30;
                case 59:
                    return 60;
                case 162:
                    return 60 * 3;
                case 442:
                    return 60 * 10;
                case 1202:
                    return 60 * 30;
                case 3269:
                    return 60 * 60;
                case 8886:
                    return 60 * 60 * 3;
                case 24154:
                    return 60 * 60 * 8;
                case 65659:
                    return 60 * 60 * 24;
                default:
                    Logger.Error("Should not happen");
                    return 5;
            }
        }
    }
}