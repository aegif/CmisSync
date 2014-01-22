using System;
using System.Collections.Generic;
using System.Linq;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace CmisSync
{
    public partial class SetupSubFinishedController : MonoMac.AppKit.NSViewController
    {

        #region Constructors

        // Called when created from unmanaged code
        public SetupSubFinishedController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public SetupSubFinishedController (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Call to load from the XIB/NIB file
        public SetupSubFinishedController (SetupController controller) : base ("SetupSubFinished", NSBundle.MainBundle)
        {
            this.Controller = controller;
            Initialize ();
        }
        // Shared initialization code
        void Initialize ()
        {
        }

        #endregion

        protected override void Dispose (bool disposing)
        {
            base.Dispose (disposing);
            Console.WriteLine (this.GetType ().ToString () + " disposed " + disposing.ToString ());
        }

        SetupController Controller;

        public override void AwakeFromNib ()
        {
            base.AwakeFromNib ();

            this.OpenButton.Title = Properties_Resources.OpenFolder;
            this.OpenButton.SizeToFit ();
            this.FinishButton.Title = Properties_Resources.Finish;
            this.FinishButton.KeyEquivalent = "\r";

            //  TODO: comment the blow line, since it may cause crash?
//            NSApplication.SharedApplication.RequestUserAttention(NSRequestUserAttentionType.CriticalRequest);
        }

        partial void OnOpen (MonoMac.Foundation.NSObject sender)
        {
            Controller.OpenFolderClicked();
        }

        partial void OnFinish (MonoMac.Foundation.NSObject sender)
        {
            Controller.FinishPageCompleted();
        }

        //strongly typed view accessor
        public new SetupSubFinished View {
            get {
                return (SetupSubFinished)base.View;
            }
        }
    }
}

