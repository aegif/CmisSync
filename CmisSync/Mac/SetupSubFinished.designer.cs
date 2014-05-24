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
	[Register ("SetupSubFinishedController")]
	partial class SetupSubFinishedController
	{
		[Outlet]
		MonoMac.AppKit.NSButton FinishButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton OpenButton { get; set; }

		[Action ("OnFinish:")]
		partial void OnFinish (MonoMac.Foundation.NSObject sender);

		[Action ("OnOpen:")]
		partial void OnOpen (MonoMac.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (OpenButton != null) {
				OpenButton.Dispose ();
				OpenButton = null;
			}

			if (FinishButton != null) {
				FinishButton.Dispose ();
				FinishButton = null;
			}
		}
	}

	[Register ("SetupSubFinished")]
	partial class SetupSubFinished
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
