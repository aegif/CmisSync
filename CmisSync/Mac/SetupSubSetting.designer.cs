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
	[Register ("SetupSubSettingController")]
	partial class SetupSubSettingController
	{
		[Outlet]
		MonoMac.AppKit.NSTextField AddressLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField AddressText { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton CancelButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField PasswordLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSSecureTextField PasswordText { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton SaveButton { get; set; }

		[Outlet]
		MonoMac.AppKit.NSSlider Slider { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField SliderLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField SliderMaxLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField SliderMinLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField SliderValueLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSButton StartupCheckbox { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField UserLabel { get; set; }

		[Outlet]
		MonoMac.AppKit.NSTextField UserText { get; set; }

		[Action ("OnCancel:")]
		partial void OnCancel (MonoMac.Foundation.NSObject sender);

		[Action ("OnSave:")]
		partial void OnSave (MonoMac.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
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

			if (PasswordLabel != null) {
				PasswordLabel.Dispose ();
				PasswordLabel = null;
			}

			if (PasswordText != null) {
				PasswordText.Dispose ();
				PasswordText = null;
			}

			if (SaveButton != null) {
				SaveButton.Dispose ();
				SaveButton = null;
			}

			if (Slider != null) {
				Slider.Dispose ();
				Slider = null;
			}

			if (SliderLabel != null) {
				SliderLabel.Dispose ();
				SliderLabel = null;
			}

			if (SliderValueLabel != null) {
				SliderValueLabel.Dispose ();
				SliderValueLabel = null;
			}

			if (SliderMaxLabel != null) {
				SliderMaxLabel.Dispose ();
				SliderMaxLabel = null;
			}

			if (SliderMinLabel != null) {
				SliderMinLabel.Dispose ();
				SliderMinLabel = null;
			}

			if (StartupCheckbox != null) {
				StartupCheckbox.Dispose ();
				StartupCheckbox = null;
			}

			if (UserLabel != null) {
				UserLabel.Dispose ();
				UserLabel = null;
			}

			if (UserText != null) {
				UserText.Dispose ();
				UserText = null;
			}
		}
	}

	[Register ("SetupSubSetting")]
	partial class SetupSubSetting
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
