using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace CmisSync
{
    public partial class SetupSubTutorialEndController : MonoMac.AppKit.NSViewController
    {

        #region Constructors

        // Called when created from unmanaged code
        public SetupSubTutorialEndController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public SetupSubTutorialEndController (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Call to load from the XIB/NIB file
        public SetupSubTutorialEndController (SetupController controller) : base ("SetupSubTutorialEnd", NSBundle.MainBundle)
        {
            this.Controller = controller;
            Initialize ();
        }
        // Shared initialization code
        void Initialize ()
        {
        }

        #endregion

        SetupController Controller;

        public override void AwakeFromNib ()
        {
            base.AwakeFromNib ();

            this.StartCheck.Title = Properties_Resources.Startup;
            this.FinishButton.Title = Properties_Resources.Finish;
//            this.FinishButton.KeyEquivalent = "\r";

            NSImage image = new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "tutorial-slide-" + Controller.TutorialCurrentPage + ".png")) {
                Size = new SizeF (350, 200)
            };
            TutorialView.Image = image;

            switch (Controller.TutorialCurrentPage) {
            case 4:
                TutorialText.StringValue = Properties_Resources.YouCan;
                OnStart (this);
                break;
            }
        }

        partial void OnStart (MonoMac.Foundation.NSObject sender)
        {
            Controller.StartupItemChanged(StartCheck.State == NSCellStateValue.On);
        }

        partial void OnFinish (MonoMac.Foundation.NSObject sender)
        {
            Controller.TutorialPageCompleted();
        }

        //strongly typed view accessor
        public new SetupSubTutorialEnd View {
            get {
                return (SetupSubTutorialEnd)base.View;
            }
        }
    }
}

