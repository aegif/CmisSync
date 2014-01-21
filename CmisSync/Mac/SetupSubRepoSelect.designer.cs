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
	[Register ("SetupSubRepoSelectController")]
	partial class SetupSubRepoSelectController
	{
		[Outlet]
		MonoMac.AppKit.NSButton BackButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton CancelButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton ContinueButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSOutlineView Outline { get; set; }

		[Action ("OnBack:")]
		partial void OnBack (MonoMac.Foundation.NSObject sender);

		[Action ("OnCancel:")]
		partial void OnCancel (MonoMac.Foundation.NSObject sender);

		[Action ("OnContinue:")]
		partial void OnContinue (MonoMac.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (BackButton != null) {
				BackButton.Dispose ();
				BackButton = null;
			}

			if (CancelButton != null) {
				CancelButton.Dispose ();
				CancelButton = null;
			}

			if (ContinueButton != null) {
				ContinueButton.Dispose ();
				ContinueButton = null;
			}

			if (Outline != null) {
				Outline.Dispose ();
				Outline = null;
			}
		}
	}

	[Register ("SetupSubRepoSelect")]
	partial class SetupSubRepoSelect
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
