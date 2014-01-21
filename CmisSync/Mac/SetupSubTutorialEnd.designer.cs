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
	[Register ("SetupSubTutorialEndController")]
	partial class SetupSubTutorialEndController
	{
		[Outlet]
		MonoMac.AppKit.NSButton FinishButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton StartCheck { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField TutorialText { get; set; }

		[Outlet]
		MonoMac.AppKit.NSImageView TutorialView { get; set; }

		[Action ("OnFinish:")]
		partial void OnFinish (MonoMac.Foundation.NSObject sender);

		[Action ("OnStart:")]
		partial void OnStart (MonoMac.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (TutorialText != null) {
				TutorialText.Dispose ();
				TutorialText = null;
			}

			if (TutorialView != null) {
				TutorialView.Dispose ();
				TutorialView = null;
			}

			if (StartCheck != null) {
				StartCheck.Dispose ();
				StartCheck = null;
			}

			if (FinishButton != null) {
				FinishButton.Dispose ();
				FinishButton = null;
			}
		}
	}

	[Register ("SetupSubTutorialEnd")]
	partial class SetupSubTutorialEnd
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
