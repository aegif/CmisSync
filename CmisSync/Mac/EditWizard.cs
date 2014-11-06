using System;
using System.Collections.Generic;
using System.Linq;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace CmisSync
{
    public partial class EditWizard : MonoMac.AppKit.NSWindow
    {

        #region Constructors

        // Called when created from unmanaged code
        public EditWizard (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public EditWizard (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Shared initialization code
        void Initialize ()
        {
        }

        #endregion

        public override void OrderFrontRegardless ()
        {
            NSApplication.SharedApplication.AddWindowsItem (this, "CmisSync", false);
            NSApplication.SharedApplication.ActivateIgnoringOtherApps (true);
            MakeKeyAndOrderFront (this);

            if (Program.UI != null)
                Program.UI.UpdateDockIconVisibility ();

            base.OrderFrontRegardless ();
        }

        public override void PerformClose (NSObject sender)
        {
            base.OrderOut (this);
            NSApplication.SharedApplication.RemoveWindowsItem (this);

            if (Program.UI != null)
                Program.UI.UpdateDockIconVisibility ();

            base.PerformClose (this);

            return;
        }

    }
}

