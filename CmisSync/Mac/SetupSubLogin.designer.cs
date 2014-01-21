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
	[Register ("SetupSubLoginController")]
	partial class SetupSubLoginController
	{
		[Outlet]
		MonoMac.AppKit.NSTextField AddressHelp { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField AddressLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField AddressText { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton CancelButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton ContinueButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField PasswordLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSSecureTextField PasswordText { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField UserLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField UserText { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField WarnText { get; set; }

		[Action ("OnCancel:")]
		partial void OnCancel (MonoMac.Foundation.NSObject sender);

		[Action ("OnContinue:")]
		partial void OnContinue (MonoMac.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (AddressHelp != null) {
				AddressHelp.Dispose ();
				AddressHelp = null;
			}

			if (AddressLabel != null) {
				AddressLabel.Dispose ();
				AddressLabel = null;
			}

			if (AddressText != null) {
				AddressText.Dispose ();
				AddressText = null;
			}

			if (CancelButton != null) {
				CancelButton.Dispose ();
				CancelButton = null;
			}

			if (ContinueButton != null) {
				ContinueButton.Dispose ();
				ContinueButton = null;
			}

			if (PasswordLabel != null) {
				PasswordLabel.Dispose ();
				PasswordLabel = null;
			}

			if (PasswordText != null) {
				PasswordText.Dispose ();
				PasswordText = null;
			}

			if (UserLabel != null) {
				UserLabel.Dispose ();
				UserLabel = null;
			}

			if (UserText != null) {
				UserText.Dispose ();
				UserText = null;
			}

			if (WarnText != null) {
				WarnText.Dispose ();
				WarnText = null;
			}
		}
	}

	[Register ("SetupSubLogin")]
	partial class SetupSubLogin
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
