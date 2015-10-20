using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace ExtendedWindowsControls
{
    public class Animation : IDisposable
    {
        private DispatcherTimer animation_timer = new DispatcherTimer();
        private int animation_currentIndex = -1;
        private int animation_maxLoopCount = 0;
        private int animation_loopCount = 0;
        private Icon[] animation_Icons;

        public class AnimationTickEventArgs : EventArgs
        {
            public Animation sender { get; internal set; }
            public Icon CurrentFrame { get; internal set; }
        }
        public delegate void AnimationTickEventHandler(AnimationTickEventArgs e);
        public event AnimationTickEventHandler Tick;

        public Animation()
        {
            animation_timer.Interval = new TimeSpan(100 * 10000);
            animation_timer.Tick += animationTimer_Tick;
        }

        public Animation(Bitmap bitmapStrip)
            : this()
        {
            SetAnimationClip(bitmapStrip);
        }

        public Animation(System.IO.Stream animationStream)
            : this(new Bitmap(animationStream))
        { }

        private void animationTimer_Tick(object sender, EventArgs e)
        {
            animation_currentIndex++;
            if (animation_currentIndex >= animation_Icons.Length)
            {
                animation_currentIndex = 0;
                if (animation_loopCount > animation_maxLoopCount)
                {
                    Stop();
                    return;
                }

            }
            fireTick(animation_Icons[animation_currentIndex]);
        }

        private void fireTick(Icon currentFrame)
        {
            AnimationTickEventHandler handler = Tick;
            if (handler != null)
            {
                handler(new AnimationTickEventArgs() { sender = this, CurrentFrame = currentFrame });
            }
            else
            {
                Stop();
                throw new InvalidOperationException("Cannot start the animation if no AnimationTick event's handlers are set");
            }
        }

        /// <summary>
        /// Sets the animation clip that will be displayed in the system tray
        /// </summary>
        /// <param name="icons">The bitmap strip that contains the frames of animation.
        ///                     This can be created by creating a image of size 16*n by 16 pixels
        ///                     Where n is the number of frames. Then in the first 16x16 pixel put
        ///                     first image and then from 16 to 32 pixel put the second image and so on</param>
        public void SetAnimationClip(Bitmap bitmapStrip)
        {
            animation_Icons = new Icon[bitmapStrip.Width / 16];
            for (int i = 0; i < animation_Icons.Length; i++)
            {
                Rectangle rect = new Rectangle(i * 16, 0, 16, 16);
                Bitmap bmp = bitmapStrip.Clone(rect, bitmapStrip.PixelFormat);
                animation_Icons[i] = Icon.FromHandle(bmp.GetHicon());
            }
        }

        public int Interval
        {
            get { return Convert.ToInt32(animation_timer.Interval.TotalMilliseconds); }
            set { animation_timer.Interval = new TimeSpan(value * 10000); }
        }

        public void Start()
        {
            Start(true);
        }
        public void Resume()
        {
            Start(false);
        }
        public void Start(bool reset)
        {
            if (animation_Icons == null)
            {
                throw new InvalidOperationException("Cannot start the animation widouth the animation frames");
            }

            if (reset)
            {
                ResetAnimation();
            }
            animation_timer.Start();
        }

        public void Stop()
        {
            Stop(true);
        }
        public void Suspend()
        {
            Stop(false);
        }
        public void Stop(bool reset)
        {
            animation_timer.Stop();
            if (reset)
            {
                ResetAnimation();
            }
        }
        public bool Enabled
        {
            get { return animation_timer.IsEnabled; }
            set
            {
                if (value == true)
                {
                    Start();
                }
                else
                {
                    Stop();
                }
            }
        }

        public void ResetAnimation()
        {
            animation_loopCount = 0;
            animation_currentIndex = 0;
            if (animation_timer.IsEnabled == false)
            {
                //dont fire the reset if the timer is still running, simply reset for the next tick
                fireTick(null);
            }
        }

        public void Dispose()
        {
            animation_timer.Stop();
            animation_timer = null;
            foreach (Icon i in animation_Icons)
            {
                i.Dispose();
            }
        }
    }
}
