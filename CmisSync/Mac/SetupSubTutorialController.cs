using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace CmisSync
{
    public partial class SetupSubTutorialController : MonoMac.AppKit.NSViewController
    {

        #region Constructors

        // Called when created from unmanaged code
        public SetupSubTutorialController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public SetupSubTutorialController (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Call to load from the XIB/NIB file
        public SetupSubTutorialController (SetupController controller) : base ("SetupSubTutorial", NSBundle.MainBundle)
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

            this.ContinueButton.Title = Properties_Resources.Continue;
//            this.ContinueButton.KeyEquivalent = "\r";

            NSImage image = new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "tutorial-slide-" + Controller.TutorialCurrentPage + ".png")) {
                Size = new SizeF (350, 200)
            };
            TutorialView.Image = image;

            switch (Controller.TutorialCurrentPage) {
            case 2:
                TutorialText.StringValue = Properties_Resources.DocumentsAre;
                break;
            case 3:
                TutorialText.StringValue = Properties_Resources.StatusIconShows;
                break;
            }
        }

        partial void OnContinue (MonoMac.Foundation.NSObject sender)
        {
            Controller.TutorialPageCompleted();
        }

        //strongly typed view accessor
        public new SetupSubTutorial View {
            get {
                return (SetupSubTutorial)base.View;
            }
        }
    }
}

