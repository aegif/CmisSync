using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace CmisSync
{
    public partial class SetupSubTutorialBeginController : MonoMac.AppKit.NSViewController
    {

        #region Constructors

        // Called when created from unmanaged code
        public SetupSubTutorialBeginController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public SetupSubTutorialBeginController (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Call to load from the XIB/NIB file
        public SetupSubTutorialBeginController (SetupController controller) : base ("SetupSubTutorialBegin", NSBundle.MainBundle)
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

            this.SkipButton.Title = Properties_Resources.SkipTutorial;
            this.ContinueButton.Title = Properties_Resources.Continue;

            NSImage image = new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "tutorial-slide-" + Controller.TutorialCurrentPage + ".png")) {
                Size = new SizeF (350, 200)
            };
            TutorialView.Image = image;

            switch (Controller.TutorialCurrentPage) {
            case 1:
                TutorialText.StringValue = Properties_Resources.CmisSyncCreates;
                break;
            default:
                break;
            }
        }

        partial void OnSkip (MonoMac.Foundation.NSObject sender)
        {
            Controller.TutorialSkipped();
        }

        partial void OnContinue (MonoMac.Foundation.NSObject sender)
        {
            Controller.TutorialPageCompleted();
        }


        //strongly typed view accessor
        public new SetupSubTutorialBegin View {
            get {
                return (SetupSubTutorialBegin)base.View;
            }
        }
    }
}

