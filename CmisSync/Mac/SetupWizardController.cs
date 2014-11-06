using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace CmisSync
{
    public partial class SetupWizardController : MonoMac.AppKit.NSWindowController
    {

        #region Constructors

        // Called when created from unmanaged code
        public SetupWizardController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public SetupWizardController (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Call to load from the XIB/NIB file
        public SetupWizardController () : base ("SetupWizard")
        {
            Initialize ();
        }
        // Shared initialization code
        void Initialize ()
        {
            Controller = new SetupController ();

            Controller.ShowWindowEvent += delegate {
                InvokeOnMainThread (delegate {
                    Window.OrderFrontRegardless();
                });
            };

            Controller.HideWindowEvent += delegate {
                InvokeOnMainThread (delegate {
                    Window.PerformClose (this);
                });
            };

            Controller.ChangePageEvent += delegate (PageType type) {
                using (var a = new NSAutoreleasePool ())
                {
                    InvokeOnMainThread (delegate {
                        if (!IsWindowLoaded) {
                            LoadWindow();
                        }
                        switch (type)
                        {
                        case PageType.Setup:
                            ShowWelcomePage();
                            break;
                        case PageType.Tutorial:
                            ShowTutorialPage();
                            break;
                        case PageType.Add1:
                            ShowLoginPage();
                            break;
                        case PageType.Add2:
                            ShowRepoSelectPage();
                            break;
                        case PageType.Customize:
                            ShowCustomizePage();
                            break;
                        // case PageType.Syncing:
                        //    ShowSyncingPage();
                        //    break;
                        case PageType.Finished:
                            ShowFinishedPage();
                            break;
                        case PageType.Settings:
                            ShowSettingsPage();
                            break;
                        }
                    });
                }
            };
        }

        public override void AwakeFromNib ()
        {
            base.AwakeFromNib ();

            this.SideSplashView.Image = new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "side-splash.png")) {
                Size = new SizeF (150, 482)
            };
        }

        #endregion

        //strongly typed window accessor
        public new SetupWizard Window {
            get {
                return (SetupWizard)base.Window;
            }
        }

        public SetupController Controller;

        private NSViewController SubController_ = null;
        private NSViewController SubController {
            get { return SubController_; }
            set {
                if (SubController_ != null) {
                    SubController_.Dispose ();
                    SubController_ = null;
                }
                SubController_ = value;
            }
        }

        void ShowLoginPage()
        {
            Header.StringValue = Properties_Resources.Where;
            Description.StringValue = "";
            SubController = new SetupSubLoginController (Controller);
            Content.ContentView = SubController.View;
        }

        void ShowRepoSelectPage()
        {
            Header.StringValue = Properties_Resources.Which;
            Description.StringValue = "";
            SubController = new SetupSubRepoSelectController (Controller);
            Content.ContentView = SubController.View;
        }

        void ShowCustomizePage()
        {
            Header.StringValue = Properties_Resources.Customize;
            Description.StringValue = "";
            SubController = new SetupSubCustomizeController (Controller);
            Content.ContentView = SubController.View;
        }

        void ShowSyncingPage()
        {
            Header.StringValue = Properties_Resources.AddingFolder + " ‘" + Controller.SyncingReponame + "’…";
            Description.StringValue = Properties_Resources.MayTakeTime;
            NSProgressIndicator progress = new NSProgressIndicator() {
                Frame = new RectangleF(0, 140, 300, 20),
                Style = NSProgressIndicatorStyle.Bar,
                MinValue = 0.0,
                MaxValue = 100.0,
                Indeterminate = false,
                DoubleValue = Controller.ProgressBarPercentage
            };
            progress.StartAnimation(this);
            Content.ContentView = progress;
        }

        void ShowFinishedPage()
        {
            Header.StringValue = Properties_Resources.Ready;
            Description.StringValue = Properties_Resources.YouCanFind;
            SubController = new SetupSubFinishedController (Controller);
            Content.ContentView = SubController.View;
        }

        void ShowWelcomePage()
        {
            Header.StringValue = Properties_Resources.Welcome;
            Description.StringValue = "";
            SubController = new SetupSubWelcomeController (Controller);
            Content.ContentView = SubController.View;
        }

        void ShowTutorialPage()
        {
            SubController = new SetupSubTutorialController (Controller);
            switch (Controller.TutorialCurrentPage) {
            case 1:
                Header.StringValue = Properties_Resources.WhatsNext;
                SubController = new SetupSubTutorialBeginController (Controller);
                break;
            case 2:
                Header.StringValue = Properties_Resources.Synchronization;
                SubController = new SetupSubTutorialController (Controller);
                break;
            case 3:
                Header.StringValue = Properties_Resources.StatusIcon;
                SubController = new SetupSubTutorialController (Controller);
                break;
            case 4:
                Header.StringValue = Properties_Resources.AddFolders;
                SubController = new SetupSubTutorialEndController (Controller);
                break;
            }
            Description.StringValue = "";
            Content.ContentView = SubController.View;
        }

        void ShowSettingsPage()
        {
            Header.StringValue = Properties_Resources.Settings;
            Description.StringValue = "";
            SubController = new SetupSubSettingController(Controller);
            Content.ContentView = SubController.View;
        }

    }
}

