using System;
using System.Collections.Generic;
using System.Linq;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace CmisSync
{
    public partial class SetupSubWelcomeController : MonoMac.AppKit.NSViewController
    {

        #region Constructors

        // Called when created from unmanaged code
        public SetupSubWelcomeController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public SetupSubWelcomeController (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Call to load from the XIB/NIB file
        public SetupSubWelcomeController (SetupController controller) : base ("SetupSubWelcome", NSBundle.MainBundle)
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

            this.WelcomeText.StringValue = Properties_Resources.Intro;

            this.CancelButton.Title = Properties_Resources.Cancel;
            this.ContinueButton.Title = Properties_Resources.Continue;
        }

        partial void OnCancel (MonoMac.Foundation.NSObject sender)
        {
            Controller.SetupPageCancelled();
        }

        partial void OnContinue (MonoMac.Foundation.NSObject sender)
        {
            Controller.SetupPageCompleted();
        }

        //strongly typed view accessor
        public new SetupSubWelcome View {
            get {
                return (SetupSubWelcome)base.View;
            }
        }
    }
}

