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
	[Register ("SetupWizardController")]
	partial class SetupWizardController
	{
		[Outlet]
		MonoMac.AppKit.NSBox Content { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField Description { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField Header { get; set; }

		[Outlet]
		MonoMac.AppKit.NSImageView SideSplashView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Content != null) {
				Content.Dispose ();
				Content = null;
			}

			if (Description != null) {
				Description.Dispose ();
				Description = null;
			}

			if (Header != null) {
				Header.Dispose ();
				Header = null;
			}

			if (SideSplashView != null) {
				SideSplashView.Dispose ();
				SideSplashView = null;
			}
		}
	}

	[Register ("SetupWizard")]
	partial class SetupWizard
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
