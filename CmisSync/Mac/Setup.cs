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
using CmisSync.Lib.Credentials;
using CmisSync.CmisTree;

namespace CmisSync {

    public class Setup : SetupWindow {

        public SetupController Controller = new SetupController ();

        private NSButton ContinueButton;
        private NSButton CancelButton;
        private NSButton SkipTutorialButton;
        private NSButton StartupCheckButton;
        private NSButton OpenFolderButton;
        private NSButton FinishButton;
        private NSImage SlideImage;
        private NSImageView SlideImageView;
        private NSProgressIndicator ProgressIndicator;
        private NSTextField AddressTextField;
        private NSTextField AddressLabel;
        private NSTextField AddressHelpLabel;
        private NSTextField PasswordTextField;
        private NSTextField PasswordLabel;
        private NSTextField WarningTextField;
        private NSImage WarningImage;
        private NSImageView WarningImageView;
        private NSOutlineView OutlineView;
        private NSScrollView ScrollView;


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
                Header      = Properties_Resources.Welcome;
                Description = Properties_Resources.Intro;

                CancelButton = new NSButton () {
                    Title = Properties_Resources.Cancel
                };

                ContinueButton = new NSButton () {
                    Title    = Properties_Resources.Continue,
                    Enabled  = false
                };

                ContinueButton.Activated += delegate {
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
                    Delegate    = new TextFieldDelegate (),
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
                    Delegate    = new TextFieldDelegate ()
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

                (AddressTextField.Delegate as TextFieldDelegate).StringValueChanged += delegate {
                    string error = Controller.CheckAddPage ( AddressTextField.StringValue);
                    if(String.IsNullOrEmpty(error))
                        AddressHelpLabel.StringValue = "";
                    else
                        AddressHelpLabel.StringValue = Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                };

                ContinueButton.Activated += delegate {
                    ServerCredentials credentials = new ServerCredentials() {
                        UserName = UserTextField.StringValue,
                        Password = PasswordTextField.StringValue,
                        Address  = new Uri(AddressTextField.StringValue)
                    };
                    Tuple<CmisServer, Exception> fuzzyResult = 
                        CmisUtils.GetRepositoriesFuzzy(credentials);
                    CmisServer cmisServer = fuzzyResult.Item1;
                    if(cmisServer!=null) {
                        Controller.repositories = cmisServer.Repositories;
                        AddressTextField.StringValue = cmisServer.Url.ToString();
                    } else {
                        Controller.repositories = null;
                    }
                    if(Controller.repositories == null) {
                        WarningTextField.StringValue = Controller.getConnectionsProblemWarning(fuzzyResult.Item1, fuzzyResult.Item2);
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

                OutlineView = new NSOutlineView () {
                    Frame            = new RectangleF (0, 0, 0, 0),
                    RowHeight        = 34,
                    IntercellSpacing = new SizeF (8, 12),
                    HeaderView       = null,
                    Delegate         = new OutlineViewDelegate ()
                };

                OutlineView.AddColumn(new NSTableColumn(){
                    Identifier = "colName",
                    Editable = false
                });
                
                ScrollView = new NSScrollView () {
                    Frame               = new RectangleF (190, Frame.Height - 340, 408, 255),
                    DocumentView        = OutlineView,
                    HasVerticalScroller = true,
                    BorderType          = NSBorderType.BezelBorder
                };

                bool firstRepo = true;
                List<RootFolder> repos = new List<RootFolder>();
                foreach (KeyValuePair<String, String> repository in Controller.repositories)
                {
                    RootFolder repo = new RootFolder()
                    {
                        Name = repository.Value,
                        Id = repository.Key,
                        Address = Controller.saved_address.ToString()
                    };
                    if (firstRepo)
                    {
                        repo.Selected = true;
                        firstRepo = false;
                    }
                    repos.Add(repo);
                }

                CmisTree.CmisTreeDataSource DataSource = new CmisTree.CmisTreeDataSource(repos);
                OutlineView.DataSource = DataSource;
                OutlineView.ReloadData ();

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

                (OutlineView.Delegate as OutlineViewDelegate).SelectionChanged += delegate {
                    Node selectedNode = ((NSNodeObject)OutlineView.ItemAtRow(OutlineView.SelectedRow)).Node;
                    while(selectedNode.Parent != null)
                        selectedNode = selectedNode.Parent;
                    RootFolder root = selectedNode as RootFolder;
                    if(root != null)
                        ContinueButton.Enabled = true;
                    else
                        ContinueButton.Enabled = false;
                };

                ContinueButton.Activated += delegate {
                    Node selectedNode = ((NSNodeObject)OutlineView.ItemAtRow(OutlineView.SelectedRow)).Node;
                    while(selectedNode.Parent != null)
                        selectedNode = selectedNode.Parent;
                    RootFolder root = selectedNode as RootFolder;
                    if(root != null) {
                        Controller.saved_repository = root.Id;
                        Controller.Add2PageCompleted (root.Id, root.Path);
                    }
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

            if (type == PageType.Customize)
            {
                Header = Properties_Resources.Customize;
                string localfoldername = Controller.saved_address.Host.ToString();
                foreach (KeyValuePair<String, String> repository in Controller.repositories)
                {
                    if (repository.Key == Controller.saved_repository)
                    {
                        localfoldername += "/" + repository.Value;
                        break;
                    }
                }
                NSTextField LocalFolderLabel = new NSTextField()
                {
                    Alignment       = NSTextAlignment.Left,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (190, 320 , 196 + 196 + 16, 17),
                    Font            = UI.BoldFont,
                    StringValue     = Properties_Resources.EnterLocalFolderName
                };

                NSTextField LocalFolderTextField = new NSTextField () {
                    Frame       = new RectangleF (190, 290, 196 + 196 + 16, 22),
                    Font        = UI.Font,
                    Delegate    = new TextFieldDelegate (),
                    StringValue = localfoldername
                };

                NSTextField LocalRepoPathLabel = new NSTextField()
                {
                    Alignment       = NSTextAlignment.Left,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (190, 220 , 196 + 196 + 16, 17),
                    Font            = UI.BoldFont,
                    StringValue     = Properties_Resources.ChangeRepoPath
                };

                NSTextField LocalRepoPathTextField = new NSTextField () {
                    Frame       = new RectangleF (190, 190, 196 + 196 + 16, 22),
                    Font        = UI.Font,
                    Delegate    = new TextFieldDelegate (),
                    StringValue = Path.Combine(Controller.DefaultRepoPath, LocalFolderTextField.StringValue)
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

                ContinueButton = new NSButton()
                {
                    Title = Properties_Resources.Add,
                    Enabled = false
                };

                NSButton BackButton = new NSButton() {
                    Title = Properties_Resources.Back
                };

                CancelButton = new NSButton() {
                    Title = Properties_Resources.Cancel
                };

                BackButton.Activated += delegate
                {
                    Controller.BackToPage2 ();
                };

                CancelButton.Activated += delegate {
                    Controller.PageCancelled ();
                };

                ContinueButton.Activated += delegate {
                    Controller.CustomizePageCompleted (LocalFolderTextField.StringValue, LocalRepoPathTextField.StringValue);
                };

                (LocalFolderTextField.Delegate as TextFieldDelegate).StringValueChanged += delegate {
                    try{
                        LocalRepoPathTextField.StringValue = Path.Combine(Controller.DefaultRepoPath, LocalFolderTextField.StringValue);
                    }catch(Exception){}
                    string error = Controller.CheckRepoPathAndName(LocalRepoPathTextField.StringValue, LocalFolderTextField.StringValue);
                    if(String.IsNullOrEmpty(error))
                        WarningTextField.StringValue = "";
                    else
                        WarningTextField.StringValue = error;
                };

                (LocalRepoPathTextField.Delegate as TextFieldDelegate).StringValueChanged += delegate {
                    string error = Controller.CheckRepoPathAndName(LocalRepoPathTextField.StringValue, LocalFolderTextField.StringValue);
                    if(String.IsNullOrEmpty(error))
                        WarningTextField.StringValue = "";
                    else
                        WarningTextField.StringValue = error;
                };

                {
                    string error = Controller.CheckRepoPathAndName(LocalRepoPathTextField.StringValue, LocalFolderTextField.StringValue);
                    if(String.IsNullOrEmpty(error))
                        WarningTextField.StringValue = "";
                    else
                        WarningTextField.StringValue = error;
                }
                ContentView.AddSubview(LocalFolderLabel);
                ContentView.AddSubview(LocalFolderTextField);
                ContentView.AddSubview(LocalRepoPathLabel);
                ContentView.AddSubview(LocalRepoPathTextField);
                ContentView.AddSubview(WarningTextField);

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
                    Title = String.Format ("Open {0}", Path.GetFileName(Controller.PreviousPath))
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
                        Header      = Properties_Resources.WhatsNext;
                        Description = Properties_Resources.CmisSyncCreates;


                        SkipTutorialButton = new NSButton () {
                                Title = Properties_Resources.SkipTutorial
                        };

                        ContinueButton = new NSButton () {
                                Title = Properties_Resources.Continue
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
                        Header      = Properties_Resources.Synchronization;
                        Description = Properties_Resources.DocumentsAre;

                        ContinueButton = new NSButton () {
                            Title = Properties_Resources.Continue
                        };

                        ContinueButton.Activated += delegate {
                            Controller.TutorialPageCompleted ();
                        };

                        Buttons.Add (ContinueButton);

                        break;
                    }

                    case 3: {
                        Header      = Properties_Resources.StatusIcon;
                        Description = Properties_Resources.StatusIconShows;

                        ContinueButton = new NSButton () {
                            Title = Properties_Resources.Continue
                        };

                        ContinueButton.Activated += delegate {
                            Controller.TutorialPageCompleted ();
                        };

                        Buttons.Add (ContinueButton);

                        break;
                    }

                    case 4: {
                        Header      = Properties_Resources.AddFolders;
                        Description = Properties_Resources.YouCan;


                        StartupCheckButton = new NSButton () {
                            Frame = new RectangleF (190, Frame.Height - 400, 300, 18),
                            Title = Properties_Resources.Startup,
                            State = NSCellStateValue.On
                        };

                        StartupCheckButton.SetButtonType (NSButtonType.Switch);

                        FinishButton = new NSButton () {
                            Title = Properties_Resources.Finish
                        };

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
}
