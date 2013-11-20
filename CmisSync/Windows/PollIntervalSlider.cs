using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CmisSync
{
    class PollIntervalSlider : Slider
    {
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(PollIntervalSlider));

        private ToolTip _autoToolTip;

        public int PollInterval {
            get {
                return (int)this.Value * (60 * 1000);
            }
            set {
                this.Value = value / (60 * 1000);
            }
        }

        public PollIntervalSlider() 
        {
            this.IsSnapToTickEnabled = true;
            this.Minimum = 15;
            //this.Minimum = 1; //testing
            this.Maximum = 1440;
            this.TickPlacement = TickPlacement.BottomRight;
            this.AutoToolTipPlacement = AutoToolTipPlacement.BottomRight;

            // Manually add ticks to the slider.
            DoubleCollection tickMarks = new DoubleCollection();
            //tickMarks.Add(1); //testing
            //tickMarks.Add(2); //testing
            //tickMarks.Add(5); //testing
            tickMarks.Add(15);
            tickMarks.Add(30);
            tickMarks.Add(60);
            tickMarks.Add(120);
            tickMarks.Add(240);
            tickMarks.Add(480);
            tickMarks.Add(720);
            tickMarks.Add(1440);
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
            TimeSpan timeSpan = new TimeSpan(0, value, 0);
            if (value < 60)
            {
                return timeSpan.ToString("%m") + " " + Properties_Resources.Minutes;
            }
            else if (value == 60)
            {
                return timeSpan.ToString("%h") + " " + Properties_Resources.Hour;
            }
            else if (value == 1440)
            {
                return timeSpan.ToString("%d") + " " + Properties_Resources.Day;
            }
            else
            {
                return timeSpan.ToString("%h") + " " + Properties_Resources.Hours;
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
}
