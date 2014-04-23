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
	[Register ("SetupSubCustomizeController")]
	partial class SetupSubCustomizeController
	{
		[Outlet]
		MonoMac.AppKit.NSButton AddButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton BackButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton CancelButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton LocalPathButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField LocalPathLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField LocalPathText { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField RepoNameLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField RepoNameText { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField WarnText { get; set; }

		[Action ("OnAdd:")]
		partial void OnAdd (MonoMac.Foundation.NSObject sender);

		[Action ("OnBack:")]
		partial void OnBack (MonoMac.Foundation.NSObject sender);

		[Action ("OnCancel:")]
		partial void OnCancel (MonoMac.Foundation.NSObject sender);

		[Action ("OnLocalPath:")]
		partial void OnLocalPath (MonoMac.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (AddButton != null) {
				AddButton.Dispose ();
				AddButton = null;
			}

			if (BackButton != null) {
				BackButton.Dispose ();
				BackButton = null;
			}

			if (CancelButton != null) {
				CancelButton.Dispose ();
				CancelButton = null;
			}

			if (LocalPathButton != null) {
				LocalPathButton.Dispose ();
				LocalPathButton = null;
			}

			if (LocalPathLabel != null) {
				LocalPathLabel.Dispose ();
				LocalPathLabel = null;
			}

			if (LocalPathText != null) {
				LocalPathText.Dispose ();
				LocalPathText = null;
			}

			if (RepoNameLabel != null) {
				RepoNameLabel.Dispose ();
				RepoNameLabel = null;
			}

			if (RepoNameText != null) {
				RepoNameText.Dispose ();
				RepoNameText = null;
			}

			if (WarnText != null) {
				WarnText.Dispose ();
				WarnText = null;
			}
		}
	}

	[Register ("SetupSubCustomize")]
	partial class SetupSubCustomize
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
