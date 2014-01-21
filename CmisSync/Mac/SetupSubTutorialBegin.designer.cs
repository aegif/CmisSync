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
	[Register ("SetupSubTutorialBeginController")]
	partial class SetupSubTutorialBeginController
	{
		[Outlet]
		MonoMac.AppKit.NSButton ContinueButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton SkipButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField TutorialText { get; set; }

		[Outlet]
		MonoMac.AppKit.NSImageView TutorialView { get; set; }

		[Action ("OnContinue:")]
		partial void OnContinue (MonoMac.Foundation.NSObject sender);

		[Action ("OnSkip:")]
		partial void OnSkip (MonoMac.Foundation.NSObject sender);
		
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

			if (SkipButton != null) {
				SkipButton.Dispose ();
				SkipButton = null;
			}

			if (ContinueButton != null) {
				ContinueButton.Dispose ();
				ContinueButton = null;
			}
		}
	}

	[Register ("SetupSubTutorialBegin")]
	partial class SetupSubTutorialBegin
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
