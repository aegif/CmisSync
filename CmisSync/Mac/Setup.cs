//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Globalization;

using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;
using MonoMac.WebKit;

using Mono.Unix;

using CmisSync.Lib.Cmis;

namespace CmisSync {

    public class Setup : SetupWindow {

        public SetupController Controller = new SetupController ();

        private NSButton ContinueButton;
        private NSButton AddButton;
        private NSButton TryAgainButton;
        private NSButton CancelButton;
        private NSButton SkipTutorialButton;
        private NSButton StartupCheckButton;
        private NSButton HistoryCheckButton;
        private NSButton ShowPasswordCheckButton;
        private NSButton OpenFolderButton;
        private NSButton FinishButton;
        private NSImage SlideImage;
        private NSImageView SlideImageView;
        private NSProgressIndicator ProgressIndicator;
        private NSTextField EmailLabel;
        private NSTextField EmailTextField;
        private NSTextField FullNameTextField;
        private NSTextField FullNameLabel;
        private NSTextField AddressTextField;
        private NSTextField AddressLabel;
        private NSTextField AddressHelpLabel;
        private NSTextField PathTextField;
        private NSTextField PathLabel;
        private NSTextField PathHelpLabel;
        private NSTextField PasswordTextField;
        private NSTextField VisiblePasswordTextField;
        private NSTextField PasswordLabel;
        private NSTextField WarningTextField;
        private NSImage WarningImage;
        private NSImageView WarningImageView;
        private NSTableView TableView;
        private NSScrollView ScrollView;
        private NSTableColumn IconColumn;
        private NSTableColumn DescriptionColumn;


        public Setup () : base ()
        {
            Controller.HideWindowEvent += delegate {
                InvokeOnMainThread (delegate {
                    PerformClose (this);
                });
            };

            Controller.ShowWindowEvent += delegate {
                InvokeOnMainThread (delegate {
                    OrderFrontRegardless ();
                });
            };

            Controller.ChangePageEvent += delegate (PageType type) {
                using (var a = new NSAutoreleasePool ())
                {
                    InvokeOnMainThread (delegate {
                        Reset ();
                        ShowPage (type);
                        ShowAll ();
                    });
                }
            };
        }


        public void ShowPage (PageType type)
        {
            if (type == PageType.Setup) {
                Header      = "Welcome to CmisSync!";
                Description = "First off, what's your name and email?\n(visible only to team members)";


                FullNameLabel = new NSTextField () {
                    Alignment       = NSTextAlignment.Right,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (165, Frame.Height - 234, 160, 17),
                    StringValue     = "Full Name:",
                    Font            = UI.Font
                };

                FullNameTextField = new NSTextField () {
                    Frame       = new RectangleF (330, Frame.Height - 238, 196, 22),
                    StringValue = UnixUserInfo.GetRealUser ().RealName,
                    Delegate    = new CmisSyncTextFieldDelegate ()
                };

                EmailLabel = new NSTextField () {
                    Alignment       = NSTextAlignment.Right,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (165, Frame.Height - 264, 160, 17),
                    StringValue     = "Email:",
                    Font            = UI.Font
                };

                EmailTextField = new NSTextField () {
                    Frame       = new RectangleF (330, Frame.Height - 268, 196, 22),
                    Delegate    = new CmisSyncTextFieldDelegate ()
                };

                CancelButton = new NSButton () {
                    Title = "Cancel"
                };

                ContinueButton = new NSButton () {
                    Title    = "Continue",
                    Enabled  = false
                };


                (FullNameTextField.Delegate as CmisSyncTextFieldDelegate).StringValueChanged += delegate {
                    Controller.CheckSetupPage ();
                };

                (EmailTextField.Delegate as CmisSyncTextFieldDelegate).StringValueChanged += delegate {
                    Controller.CheckSetupPage ();
                };

                ContinueButton.Activated += delegate {
                    string full_name = FullNameTextField.StringValue.Trim ();
                    string email     = EmailTextField.StringValue.Trim ();

                    Controller.SetupPageCompleted ();
                };

                CancelButton.Activated += delegate {
                    Controller.SetupPageCancelled ();
                };

                Controller.UpdateSetupContinueButtonEvent += delegate (bool button_enabled) {
                    InvokeOnMainThread (delegate {
                        ContinueButton.Enabled = button_enabled;
                    });
                };


                ContentView.AddSubview (FullNameLabel);
                ContentView.AddSubview (FullNameTextField);
                ContentView.AddSubview (EmailLabel);
                ContentView.AddSubview (EmailTextField);

                Buttons.Add (ContinueButton);
                Buttons.Add (CancelButton);

                Controller.CheckSetupPage ();
            }

			if (type == PageType.Add1) {
				Header      = CmisSync.Properties_Resources.Where;
				Description = "";

				AddressLabel = new NSTextField () {
					Alignment       = NSTextAlignment.Left,
					BackgroundColor = NSColor.WindowBackground,
					Bordered        = false,
					Editable        = false,
					Frame           = new RectangleF (190, 320 , 196 + 196 + 16, 17),
					StringValue     = Properties_Resources.EnterWebAddress,
					Font            = UI.BoldFont
				};

				AddressTextField = new NSTextField () {
					Frame       = new RectangleF (190, 290, 196 + 196 + 16, 22),
					Font        = UI.Font,
					Delegate    = new CmisSyncTextFieldDelegate (),
					StringValue = (Controller.PreviousAddress == null || String.IsNullOrEmpty(Controller.PreviousAddress.ToString()))? "https://" : Controller.PreviousAddress.ToString() 
				};

				AddressTextField.Cell.LineBreakMode = NSLineBreakMode.TruncatingHead;

				AddressHelpLabel = new NSTextField () {
					BackgroundColor = NSColor.WindowBackground,
					Bordered        = false,
					TextColor       = NSColor.DisabledControlText,
					Editable        = false,
					Frame           = new RectangleF (190, 265, 196 + 196 + 16, 17),
					Font            = NSFontManager.SharedFontManager.FontWithFamily ("Lucida Grande",
					                                                                  NSFontTraitMask.Condensed, 0, 11),
				};

				NSTextField UserLabel = new NSTextField () {
					Alignment       = NSTextAlignment.Left,
					BackgroundColor = NSColor.WindowBackground,
					Font            = UI.BoldFont,
					Bordered        = false,
					Editable        = false,
					Frame           = new RectangleF(190, 230, 196, 17),
					StringValue     = Properties_Resources.User
				};

				NSTextField UserTextField = new NSTextField()
				{
					Font = UI.Font,
					StringValue = String.IsNullOrEmpty(Controller.saved_user) ? Environment.UserName : Controller.saved_user,
					Frame = new RectangleF(190, 200, 196, 22)
				};
				UserTextField.Cell.LineBreakMode = NSLineBreakMode.TruncatingHead;

				PasswordLabel = new NSTextField () {
					Alignment       = NSTextAlignment.Left,
					BackgroundColor = NSColor.WindowBackground,
					Bordered        = false,
					Editable        = false,
					Frame           = new RectangleF (190 + 196 + 16, 230 , 196, 17),
					StringValue     = Properties_Resources.Password,
					Font            = UI.BoldFont
				};

				PasswordTextField = new NSSecureTextField () {
					Frame       = new RectangleF (190 + 196 + 16, 200, 196, 22),
					Delegate    = new CmisSyncTextFieldDelegate ()
				};

				WarningTextField = new NSTextField()
				{
					BackgroundColor = NSColor.WindowBackground,
					Bordered        = false,
					TextColor       = NSColor.Red,
					Editable        = false,
					Frame           = new RectangleF (190, 30, 196 + 196 + 16, 160),
					Font            = NSFontManager.SharedFontManager.FontWithFamily ("Lucida Grande",
					NSFontTraitMask.Condensed, 0, 11),
				};

				WarningTextField.Cell.LineBreakMode = NSLineBreakMode.ByWordWrapping;

				ContinueButton = new NSButton () {
					Title = Properties_Resources.Continue,
					Enabled = false
				};
				
				CancelButton = new NSButton () {
					Title = Properties_Resources.Cancel
				};
				
				
				Controller.ChangeAddressFieldEvent += delegate (string text,
				                                                string example_text) {
					
					InvokeOnMainThread (delegate {
						AddressTextField.StringValue = text;
						AddressTextField.Enabled     = true;
						AddressHelpLabel.StringValue = example_text;
					});
				};

				(AddressTextField.Delegate as CmisSyncTextFieldDelegate).StringValueChanged += delegate {
					string error = Controller.CheckAddPage ( AddressTextField.StringValue);
					if(String.IsNullOrEmpty(error))
						AddressHelpLabel.StringValue = "";
					else
						AddressHelpLabel.StringValue = Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
				};

				ContinueButton.Activated += delegate {
					Tuple<CmisServer, Exception> fuzzyResult = 
						CmisUtils.GetRepositoriesFuzzy(
							new Uri(AddressTextField.StringValue),
							UserTextField.StringValue,
							PasswordTextField.StringValue
						);
					CmisServer cmisServer = fuzzyResult.Item1;
					if(cmisServer!=null) {
						Controller.repositories = cmisServer.Repositories;
						AddressTextField.StringValue = cmisServer.Url.ToString();
					}else {
						Controller.repositories = null;
					}
					if(Controller.repositories == null) {
						string warning = "";
						string message = fuzzyResult.Item2.Message;
						Exception e = fuzzyResult.Item2;
						if(e is CmisPermissionDeniedException)
							warning = Properties_Resources.LoginFailedForbidden;
						else if(e is CmisServerNotFoundException)
							warning = Properties_Resources.ConnectFailure;
						else if(e.Message == "SendFailure" && cmisServer.Url.Scheme.StartsWith("https"))
							warning = Properties_Resources.SendFailureHttps;
						else if (e.Message == "TrustFailure")
							warning = Properties_Resources.TrustFailure;
						else
							warning = message + Environment.NewLine + Properties_Resources.Sorry;
						WarningTextField.StringValue = warning;
					} else {
						WarningTextField.StringValue = "";
						Controller.Add1PageCompleted (new Uri(AddressTextField.StringValue), UserTextField.StringValue , PasswordTextField.StringValue);
					}
				};
				
				CancelButton.Activated += delegate {
					Controller.PageCancelled ();
				};
				
				Controller.UpdateAddProjectButtonEvent += delegate (bool button_enabled) {
					InvokeOnMainThread (delegate {
						ContinueButton.Enabled = button_enabled;
					});
				};

				ContentView.AddSubview (AddressLabel);
				ContentView.AddSubview (AddressTextField);
				ContentView.AddSubview (AddressHelpLabel);
				ContentView.AddSubview (UserLabel);
				ContentView.AddSubview (UserTextField);
				ContentView.AddSubview (PasswordLabel);
				ContentView.AddSubview (PasswordTextField);
				ContentView.AddSubview (WarningTextField);
				Buttons.Add (ContinueButton);
				Buttons.Add (CancelButton);

				Controller.CheckAddPage (AddressTextField.StringValue);
			}
			
			if (type == PageType.Add2) {
				Header      = Properties_Resources.Which;
				Description = "";

				TableView = new NSTableView () {
					Frame            = new RectangleF (0, 0, 0, 0),
					RowHeight        = 34,
					IntercellSpacing = new SizeF (8, 12),
					HeaderView       = null,
					Delegate         = new CmisSyncTableDelegate ()
				};
				
				ScrollView = new NSScrollView () {
					Frame               = new RectangleF (190, Frame.Height - 340, 408, 255),
					DocumentView        = TableView,
					HasVerticalScroller = true,
					BorderType          = NSBorderType.BezelBorder
				};

				/* DataSource = new CmisSyncDataSource (Controller.Plugins);

				TableView.DataSource = DataSource;
				TableView.ReloadData ();*/

				ContinueButton = new NSButton () {
					Title = Properties_Resources.Continue,
					Enabled = false
				};
				
				CancelButton = new NSButton () {
					Title = Properties_Resources.Cancel
				};

				NSButton BackButton = new NSButton () {
					Title = Properties_Resources.Back
				};

				Controller.ChangeAddressFieldEvent += delegate (string text,
				                                                string example_text) {
					InvokeOnMainThread (delegate {
						AddressTextField.StringValue = text;
						AddressTextField.Enabled     = true;
						AddressHelpLabel.StringValue = example_text;
					});
				};

				Controller.ChangePathFieldEvent += delegate (string text,
				                                             string example_text) {
					
					InvokeOnMainThread (delegate {
						PathTextField.StringValue = text;
						PathTextField.Enabled     = true;
						PathHelpLabel.StringValue = example_text;
					});
				};

				(TableView.Delegate as CmisSyncTableDelegate).SelectionChanged += delegate {
					Controller.CheckAddPage (AddressTextField.StringValue);
				};

				ContinueButton.Activated += delegate {
					Controller.Add2PageCompleted (AddressTextField.StringValue, PathTextField.StringValue);
				};

				CancelButton.Activated += delegate {
					Controller.PageCancelled ();
				};

				BackButton.Activated += delegate
				{
					Controller.BackToPage1 ();
				};
				
				Controller.UpdateAddProjectButtonEvent += delegate (bool button_enabled) {
					InvokeOnMainThread (delegate {
						ContinueButton.Enabled = button_enabled;
					});
				};
				
				
				ContentView.AddSubview (ScrollView);

				Buttons.Add (ContinueButton);
				Buttons.Add (BackButton);
				Buttons.Add (CancelButton);
			}
			
			if (type == PageType.Syncing) {
				Header      = Properties_Resources.AddingFolder + " ‘" + Controller.SyncingReponame + "’…";
				Description = Properties_Resources.MayTakeTime;


                ProgressIndicator = new NSProgressIndicator () {
                    Frame         = new RectangleF (190, Frame.Height - 200, 640 - 150 - 80, 20),
                    Style         = NSProgressIndicatorStyle.Bar,
                    MinValue      = 0.0,
                    MaxValue      = 100.0,
                    Indeterminate = false,
                    DoubleValue   = Controller.ProgressBarPercentage
                };

                ProgressIndicator.StartAnimation (this);

                Controller.UpdateProgressBarEvent += delegate (double percentage) {
                    InvokeOnMainThread (() => {
                        ProgressIndicator.DoubleValue = percentage;
                    });
                };

                ContentView.AddSubview (ProgressIndicator);
            }

            if (type == PageType.Finished) {
				Header      = Properties_Resources.Ready;
				Description = Properties_Resources.YouCanFind;

                OpenFolderButton = new NSButton () {
                    Title = string.Format ("Open {0}", Path.GetFileName (Controller.PreviousPath))
                };

                FinishButton = new NSButton () {
					Title = Properties_Resources.Finish
                };


                OpenFolderButton.Activated += delegate {
                    Controller.OpenFolderClicked ();
                };

                FinishButton.Activated += delegate {
                    Controller.FinishPageCompleted ();
                };


                Buttons.Add (FinishButton);
                Buttons.Add (OpenFolderButton);

                NSApplication.SharedApplication.RequestUserAttention (NSRequestUserAttentionType.CriticalRequest);
            }

            if (type == PageType.Tutorial) {
                string slide_image_path = Path.Combine (NSBundle.MainBundle.ResourcePath,
                    "Pixmaps", "tutorial-slide-" + Controller.TutorialCurrentPage + ".png");

                SlideImage = new NSImage (slide_image_path) {
                    Size = new SizeF (350, 200)
                };

                SlideImageView = new NSImageView () {
                    Image = SlideImage,
                    Frame = new RectangleF (215, Frame.Height - 350, 350, 200)
                };

                ContentView.AddSubview (SlideImageView);


                switch (Controller.TutorialCurrentPage) {

                    case 1: {
                        Header      = "What's happening next?";
                        Description = "CmisSync creates a special folder on your computer " +
                            "that will keep track of your projects.";


                        SkipTutorialButton = new NSButton () {
                            Title = "Skip Tutorial"
                        };

                        ContinueButton = new NSButton () {
                            Title = "Continue"
                        };


                        SkipTutorialButton.Activated += delegate {
                            Controller.TutorialSkipped ();
                        };

                        ContinueButton.Activated += delegate {
                            Controller.TutorialPageCompleted ();
                        };


                        ContentView.AddSubview (SlideImageView);

                        Buttons.Add (ContinueButton);
                        Buttons.Add (SkipTutorialButton);

                        break;
                    }

                    case 2: {
                        Header      = "Sharing files with others";
                        Description = "All files added to your project folders are synced automatically with " +
                            "the host and your team members.";

                        ContinueButton = new NSButton () {
                            Title = "Continue"
                        };

                        ContinueButton.Activated += delegate {
                            Controller.TutorialPageCompleted ();
                        };

                        Buttons.Add (ContinueButton);

                        break;
                    }

                    case 3: {
                        Header      = "The status icon is here to help";
                        Description = "It shows the syncing progress, provides easy access to " +
                            "your projects and let's you view recent changes.";

                        ContinueButton = new NSButton () {
                            Title = "Continue"
                        };

                        ContinueButton.Activated += delegate {
                            Controller.TutorialPageCompleted ();
                        };

                        Buttons.Add (ContinueButton);

                        break;
                    }

                    case 4: {
                        Header      = "Adding projects to CmisSync";
                        Description = "You can do this through the status icon menu, or by clicking " +
                            "magic buttons on webpages that look like this:";


                        StartupCheckButton = new NSButton () {
                            Frame = new RectangleF (190, Frame.Height - 400, 300, 18),
                            Title = "Add CmisSync to startup items",
                            State = NSCellStateValue.On
                        };

                        StartupCheckButton.SetButtonType (NSButtonType.Switch);

                        FinishButton = new NSButton () {
                            Title = "Finish"
                        };

                        SlideImage.Size = new SizeF (350, 64);


                        StartupCheckButton.Activated += delegate {
                            Controller.StartupItemChanged (StartupCheckButton.State == NSCellStateValue.On);
                        };

                        FinishButton.Activated += delegate {
                            Controller.TutorialPageCompleted ();
                        };


                        ContentView.AddSubview (StartupCheckButton);
                        Buttons.Add (FinishButton);

                        break;
                    }
                }
            }
        }
    }

	/*
    [Register("CmisSyncDataSource")]
    public class CmisSyncDataSource : NSTableViewDataSource {

        public List<object> Items ;
        public NSAttributedString [] Cells;
        public NSAttributedString [] SelectedCells;


        public CmisSyncDataSource (List<SparklePlugin> plugins)
        {
            Items         = new List <object> ();
            Cells         = new NSAttributedString [plugins.Count];
            SelectedCells = new NSAttributedString [plugins.Count];

            int i = 0;
            foreach (SparklePlugin plugin in plugins) {
                Items.Add (plugin);

                NSTextFieldCell cell = new NSTextFieldCell ();

                NSData name_data = NSData.FromString ("<font face='Lucida Grande'><b>" + plugin.Name + "</b></font>");

                NSDictionary name_dictionary       = new NSDictionary();
                NSAttributedString name_attributes = new NSAttributedString (
                    name_data, new NSUrl ("file://"), out name_dictionary);

                NSData description_data = NSData.FromString (
                    "<small><font style='line-height: 150%' color='#aaa' face='Lucida Grande'>" + plugin.Description + "</font></small>");

                NSDictionary description_dictionary       = new NSDictionary();
                NSAttributedString description_attributes = new NSAttributedString (
                    description_data, new NSUrl ("file://"), out description_dictionary);

                NSMutableAttributedString mutable_attributes = new NSMutableAttributedString (name_attributes);
                mutable_attributes.Append (new NSAttributedString ("\n"));
                mutable_attributes.Append (description_attributes);

                cell.AttributedStringValue = mutable_attributes;
                Cells [i] = (NSAttributedString) cell.ObjectValue;

                NSTextFieldCell selected_cell = new NSTextFieldCell ();

                NSData selected_name_data = NSData.FromString (
                    "<font color='white' face='Lucida Grande'><b>" + plugin.Name + "</b></font>");

                NSDictionary selected_name_dictionary = new NSDictionary ();
                NSAttributedString selected_name_attributes = new NSAttributedString (
                    selected_name_data, new NSUrl ("file://"), out selected_name_dictionary);

                NSData selected_description_data = NSData.FromString (
                    "<small><font style='line-height: 150%' color='#9bbaeb' face='Lucida Grande'>" +
                    plugin.Description + "</font></small>");

                NSDictionary selected_description_dictionary       = new NSDictionary ();
                NSAttributedString selected_description_attributes = new NSAttributedString (
                    selected_description_data, new NSUrl ("file://"), out selected_description_dictionary);

                NSMutableAttributedString selected_mutable_attributes =
                    new NSMutableAttributedString (selected_name_attributes);

                selected_mutable_attributes.Append (new NSAttributedString ("\n"));
                selected_mutable_attributes.Append (selected_description_attributes);

                selected_cell.AttributedStringValue = selected_mutable_attributes;
                SelectedCells [i] = (NSAttributedString) selected_cell.ObjectValue;

                i++;
            }
        }


        [Export("numberOfRowsInTableView:")]
        public int numberOfRowsInTableView (NSTableView table_view)
        {
            if (Items == null)
                return 0;
            else
                return Items.Count;
        }


        [Export("tableView:objectValueForTableColumn:row:")]
        public NSObject objectValueForTableColumn (NSTableView table_view,
            NSTableColumn table_column, int row_index)
        {
            if (table_column.HeaderToolTip.Equals ("Description")) {
                if (table_view.SelectedRow == row_index &&
                    Program.UI.Setup.IsKeyWindow &&
                    Program.UI.Setup.FirstResponder == table_view) {

                    return SelectedCells [row_index];

                } else {
                    return Cells [row_index];
                }

            } else {
                return new NSImage ((Items [row_index] as SparklePlugin).ImagePath) {
                    Size = new SizeF (24, 24)
                };
            }
        }
    }*/


    public class CmisSyncTextFieldDelegate : NSTextFieldDelegate {

        public event StringValueChangedHandler StringValueChanged;
        public delegate void StringValueChangedHandler ();


        public override void Changed (NSNotification notification)
        {
            if (StringValueChanged != null)
                StringValueChanged ();
        }
    }


    public class CmisSyncTableDelegate : NSTableViewDelegate {

        public event SelectionChangedHandler SelectionChanged;
        public delegate void SelectionChangedHandler ();


        public override void SelectionDidChange (NSNotification notification)
        {
            if (SelectionChanged != null)
                SelectionChanged ();
        }
    }
}
