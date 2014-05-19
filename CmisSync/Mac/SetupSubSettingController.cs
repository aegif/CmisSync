using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MonoMac.Foundation;
using MonoMac.AppKit;

using CmisSync.Auth;
using CmisSync.Lib.Cmis;

namespace CmisSync
{
    public partial class SetupSubSettingController : MonoMac.AppKit.NSViewController
    {
        #region Constructors

        // Called when created from unmanaged code
        public SetupSubSettingController(IntPtr handle) : base(handle)
        {
            Initialize();
        }
        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
        public SetupSubSettingController(NSCoder coder) : base(coder)
        {
            Initialize();
        }
        // Call to load from the XIB/NIB file
        public SetupSubSettingController(SetupController controller) : base("SetupSubSetting", NSBundle.MainBundle)
        {
            this.Controller = controller;
            Initialize();
        }
        // Shared initialization code
        void Initialize()
        {
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Console.WriteLine (this.GetType ().ToString () + " disposed " + disposing.ToString ());
        }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();

            // Initialize UI components
            // Repository address 
            this.AddressLabel.StringValue = Properties_Resources.WebAddress + ":";
            this.AddressText.StringValue = Controller.saved_address.ToString();
            this.AddressText.Enabled = false;

            // User
            this.UserLabel.StringValue = Properties_Resources.User + ":";
            this.UserText.StringValue = Controller.saved_user;
            this.UserText.Enabled = false;

            // Password
            this.PasswordLabel.StringValue = Properties_Resources.Password + ":";

            // SyncAtStartup option
            this.StartupCheckbox.Title = Properties_Resources.SyncAtStartup;
            this.StartupCheckbox.State = Controller.saved_syncatstartup ? NSCellStateValue.On : NSCellStateValue.Off;

            // Synchronize interval
            this.SliderLabel.StringValue = Properties_Resources.SyncInterval + ":";
            this.SliderValueLabel.StringValue = FormattedIntervalString(Controller.saved_sync_interval);
            this.SliderValueLabel.Alignment = NSTextAlignment.Right;

            this.Slider.TickMarkPosition = NSTickMarkPosition.Below;
            this.Slider.TickMarksCount = IntervalValues.Count();
            this.Slider.AllowsTickMarkValuesOnly = true;
            this.Slider.MinValue = 0;
            this.Slider.MaxValue = IntervalValues.Count() - 1;
            this.Slider.IntegerValue = PollingIntervalToSliderIndex(Controller.saved_sync_interval);
            this.Slider.Activated += (object sender, EventArgs e) =>
            {
                SliderValueLabel.StringValue = FormattedIntervalString(SliderIndexToPollingInterval(Slider.IntValue));
            };
                
            this.SliderMinLabel.StringValue = FormattedIntervalString(IntervalValues[0]);
            this.SliderMaxLabel.StringValue = FormattedIntervalString(IntervalValues[IntervalValues.Count() - 1]);
            this.SliderMaxLabel.Alignment = NSTextAlignment.Right;

            // CancelButton
            this.CancelButton.Title = Properties_Resources.Cancel;

            // SaveButton
            this.SaveButton.Title = Properties_Resources.Save;

            InsertEvent();

        }

        void InsertEvent()
        {
            // this.AddressDelegate.StringValueChanged += CheckAddressTextField;
            // Controller.UpdateSetupContinueButtonEvent += SetContinueButton;
            // Controller.UpdateAddProjectButtonEvent += SetContinueButton;
        }

        void RemoveEvent()
        {
            // this.AddressDelegate.StringValueChanged -= CheckAddressTextField;
            // Controller.UpdateSetupContinueButtonEvent -= SetContinueButton;
            // Controller.UpdateAddProjectButtonEvent -= SetContinueButton;
        }


        SetupController Controller;

        partial void OnCancel(NSObject sender)
        {
            RemoveEvent();
            Controller.PageCancelled();
        }

        partial void OnSave(NSObject sender)
        {
            if (!String.IsNullOrEmpty(PasswordText.StringValue))
            {
                // Try to find the CMIS server and Check credentials
                var credentials = new ServerCredentials()
                {
                    UserName = UserText.StringValue,
                    Password = PasswordText.StringValue,
                    Address = new Uri(AddressText.StringValue)
                };
                PasswordText.Enabled = false;
                CancelButton.Enabled = false;
                SaveButton.Enabled = false;

                Thread check = new Thread(() => {
                    var fuzzyResult = CmisUtils.GetRepositoriesFuzzy(credentials);
                    var cmisServer = fuzzyResult.Item1;
                    Controller.repositories = (cmisServer != null)? cmisServer.Repositories: null;

                    InvokeOnMainThread(() => {
                        if (Controller.repositories == null)
                        {
                            string warning = "";
                            string message = fuzzyResult.Item2.Message;
                            Exception e = fuzzyResult.Item2;
                            if (e is PermissionDeniedException)
                            {
                                warning = Properties_Resources.LoginFailedForbidden;
                            }
                            else if (e is ServerNotFoundException)
                            {
                                warning = Properties_Resources.ConnectFailure;
                            }
                            else if (e.Message == "SendFailure" && cmisServer.Url.Scheme.StartsWith("https"))
                            {
                                warning = Properties_Resources.SendFailureHttps;
                            }
                            else if (e.Message == "TrustFailure")
                            {
                                warning = Properties_Resources.TrustFailure;
                            }
                            else if (e.Message == "Unauthorized")
                            {
                                message = Properties_Resources.LoginFailedForbidden;
                            }
                            else
                            {
                                warning = Properties_Resources.Sorry;
                            }
                                
                            NSAlert alert = NSAlert.WithMessage(message, "OK", null, null, warning);
                            alert.Icon = new NSImage (System.IO.Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "process-syncing-error.icns"));
                            alert.Window.OrderFrontRegardless();
                            alert.RunModal();

                            PasswordText.Enabled = true;
                            CancelButton.Enabled = true;
                            SaveButton.Enabled = true;
                        }
                        else
                        {
                            RemoveEvent();
                            Controller.SettingsPageCompleted(PasswordText.StringValue, SliderIndexToPollingInterval(Slider.IntValue), (StartupCheckbox.State == NSCellStateValue.On));
                        }
                    });
                });
                check.Start();
            }
            else
            {
                Controller.SettingsPageCompleted(null, SliderIndexToPollingInterval(Slider.IntValue), (StartupCheckbox.State == NSCellStateValue.On));
            }
        }

        #region Slider utils methods
        /// <summary>
        /// The synchronize interval values in milliseconds.
        /// </summary>
        private static int[] IntervalValues = new [] {
            1000 * 5,               // 5 seconds.
            1000 * 15,
            1000 * 30,
            1000 * 60,
            1000 * 60 * 3,          // 3 minutes.
            1000 * 60 * 10,
            1000 * 60 * 30,
            1000 * 60 * 60,
            1000 * 60 * 60 * 3,     // 3 hours.
            1000 * 60 * 60 * 8,
            1000 * 60 * 60 * 12,
            1000 * 60 * 60 * 24,
        };

        /// <summary>
        /// Convert from index to synchronize interval
        /// </summary>
        /// <returns>Synchronize interval in milliseconds.</returns>
        /// <param name="index">Index.</param>
        private int SliderIndexToPollingInterval(int index)
        {
            if (index < 0)
                return IntervalValues [0];

            if (index >= IntervalValues.Count())
                return IntervalValues [IntervalValues.Count() - 1];

            return IntervalValues [index];
        }

        /// <summary>
        /// Convert from polling interval to index
        /// </summary>
        /// <returns>index.</returns>
        /// <param name="interval">Interval.</param>
        private int PollingIntervalToSliderIndex(int interval)
        {
            int index = 0;
            for (; index < IntervalValues.Count(); index++)
            {
                if (interval <= IntervalValues[index])
                {
                    break;
                }
            }

            return index;
        }

        /// <summary>
        /// Make formatted interval string.
        /// </summary>
        /// <returns>The formatted interval string.</returns>
        /// <param name="value">polling interval in milliseconds</param>
        private string FormattedIntervalString(int value)
        {
            value = value / 1000;
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

        #endregion

        //strongly typed view accessor
        public new SetupSubSetting View
        {
            get
            {
                return (SetupSubSetting)base.View;
            }
        }
    }
}

