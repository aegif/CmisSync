// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using MonoMac.Foundation;
using System.CodeDom.Compiler;

namespace CmisSync
{
	[Register ("EditWizardController")]
	partial class EditWizardController
	{
		[Outlet]
		MonoMac.AppKit.NSButton CancelButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton FinishButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField Header { get; set; }

		[Outlet]
		MonoMac.AppKit.NSOutlineView Outline { get; set; }

		[Outlet]
		MonoMac.AppKit.NSImageView SideSplashView { get; set; }

		[Action ("OnCancel:")]
		partial void OnCancel (MonoMac.Foundation.NSObject sender);

		[Action ("OnFinish:")]
		partial void OnFinish (MonoMac.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (CancelButton != null) {
				CancelButton.Dispose ();
				CancelButton = null;
			}

			if (FinishButton != null) {
				FinishButton.Dispose ();
				FinishButton = null;
			}

			if (Header != null) {
				Header.Dispose ();
				Header = null;
			}

			if (Outline != null) {
				Outline.Dispose ();
				Outline = null;
			}

			if (SideSplashView != null) {
				SideSplashView.Dispose ();
				SideSplashView = null;
			}
		}
	}

	[Register ("EditWizard")]
	partial class EditWizard
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
