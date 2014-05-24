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
	[Register ("SetupSubTutorialController")]
	partial class SetupSubTutorialController
	{
		[Outlet]
		MonoMac.AppKit.NSButton ContinueButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField TutorialText { get; set; }

		[Outlet]
		MonoMac.AppKit.NSImageView TutorialView { get; set; }

		[Action ("OnContinue:")]
		partial void OnContinue (MonoMac.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (ContinueButton != null) {
				ContinueButton.Dispose ();
				ContinueButton = null;
			}

			if (TutorialText != null) {
				TutorialText.Dispose ();
				TutorialText = null;
			}

			if (TutorialView != null) {
				TutorialView.Dispose ();
				TutorialView = null;
			}
		}
	}

	[Register ("SetupSubTutorial")]
	partial class SetupSubTutorial
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
