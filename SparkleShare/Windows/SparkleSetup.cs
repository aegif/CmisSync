//   SparkleShare, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General private License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General private License for more details.
//
//   You should have received a copy of the GNU General private License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shell;

using Drawing = System.Drawing;
using Imaging = System.Windows.Interop.Imaging;
using WPF = System.Windows.Controls;

using SparkleLib.Cmis;
using SparkleLib;
using System.Globalization;

namespace SparkleShare {

    /**
     * Stores the metadata of an item in the folder selection dialog.
     */
    public class SelectionTreeItem
    {
        public bool childrenLoaded = false;
        public string fullPath;
        public SelectionTreeItem(string fullPath)
        {
            this.fullPath = fullPath;
        }
    }

    public class SparkleSetup : SparkleSetupWindow {
    
        public SparkleSetupController Controller = new SparkleSetupController ();
        
        public SparkleSetup ()
        {
            Controller.ShowWindowEvent += delegate {
               Dispatcher.BeginInvoke ((Action) delegate {
                    Show ();
                    Activate ();
                    BringIntoView ();
                });
            };

            Controller.HideWindowEvent += delegate {
                Dispatcher.BeginInvoke ((Action) delegate {
                    Hide ();
                });
            };
            
            Controller.ChangePageEvent += delegate (PageType type, string [] warnings) {
                Dispatcher.BeginInvoke ((Action) delegate {
                    Reset ();
                    
                    switch (type) {
                    case PageType.Setup: {
                        Header = SparkleShare.Properties.Resources.ResourceManager.GetString("Welcome", CultureInfo.CurrentCulture);
                        Description = SparkleShare.Properties.Resources.ResourceManager.GetString("Intro", CultureInfo.CurrentCulture);
                        
                        Button cancel_button = new Button () {
                            Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Cancel", CultureInfo.CurrentCulture)
                        };
                        
                        Button continue_button = new Button () {
                            Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture),
                            IsEnabled = false
                        };
                        
                        Buttons.Add (continue_button);
                        Buttons.Add (cancel_button);

                        continue_button.Focus();
                        
                        Controller.UpdateSetupContinueButtonEvent += delegate (bool enabled) {
                        	Dispatcher.BeginInvoke ((Action) delegate {
                                continue_button.IsEnabled = enabled;
                            });
                        };
                        
                        cancel_button.Click += delegate {
                            Dispatcher.BeginInvoke ((Action) delegate {
                                Program.UI.StatusIcon.Dispose ();    
                                Controller.SetupPageCancelled ();
                            });
                        };
                        
                        continue_button.Click += delegate {
                            Controller.SetupPageCompleted ();
                        };
                        
                        Controller.CheckSetupPage ();
                        
                        break;
                    }

                    case PageType.Invite: {
                        Header      = "You've received an invite!";
                           Description = "Do you want to add this project to SparkleShare?";
        
                        
                        TextBlock address_label = new TextBlock () {
                            Text = "Address:",
                            Width = 150,
                            TextAlignment = TextAlignment.Right
                        };
                                                    
                        TextBlock address_value = new TextBlock () {
                            Text  = Controller.PendingInvite.Address,
                            Width = 175,
                            FontWeight = FontWeights.Bold
                        };
                        
                        
                        TextBlock path_label = new TextBlock () {
                            Text  = "Remote Path:",
                            Width = 150,
                            TextAlignment = TextAlignment.Right
                        };
                        
                        TextBlock path_value = new TextBlock () {
                            Width = 175,
                            Text = Controller.PendingInvite.RemotePath,
                            FontWeight = FontWeights.Bold
                        };
                        
                        
                        
                        Button cancel_button = new Button () {
                            Content = "Cancel"
                        };
                        
                        Button add_button = new Button () {
                            Content = "Add"
                        };


                        ContentCanvas.Children.Add (address_label);
                        Canvas.SetLeft (address_label, 180);
                        Canvas.SetTop (address_label, 200);
                        
                        ContentCanvas.Children.Add (address_value);
                        Canvas.SetLeft (address_value, 340);
                        Canvas.SetTop (address_value, 200);
                        
                        ContentCanvas.Children.Add (path_label);
                        Canvas.SetLeft (path_label, 180);
                        Canvas.SetTop (path_label, 225);
                        
                        ContentCanvas.Children.Add (path_value);
                        Canvas.SetLeft (path_value, 340);
                        Canvas.SetTop (path_value, 225);
                        
                        Buttons.Add (add_button);
                        Buttons.Add (cancel_button);
                        
                        
                        cancel_button.Click += delegate {
                            Controller.PageCancelled ();
                        };
                        
                        add_button.Click += delegate {
                            Controller.InvitePageCompleted ();
                        };
                        
                        break;
                    }
                        
                    case PageType.Add1: {
                        Header = SparkleShare.Properties.Resources.ResourceManager.GetString("Where", CultureInfo.CurrentCulture);
                        
                        // Address
                        TextBlock address_label = new TextBlock () {
                            Text       = SparkleShare.Properties.Resources.ResourceManager.GetString("EnterWebAddress", CultureInfo.CurrentCulture),
                            FontWeight = FontWeights.Bold
                        };
                        
                        TextBox address_box = new TextBox () {
                            Width = 420,
                            Text  = Controller.PreviousAddress,
                            IsEnabled = (Controller.SelectedPlugin.Address == null)
                        };

                        TextBlock address_help_label = new TextBlock()
                        {
                            Text = SparkleShare.Properties.Resources.ResourceManager.GetString("Help", CultureInfo.CurrentCulture) + ": ",
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128))
                        };
                        Run run = new Run(SparkleShare.Properties.Resources.ResourceManager.GetString("WhereToFind", CultureInfo.CurrentCulture));
                        Hyperlink link = new Hyperlink(run);
                        link.NavigateUri = new Uri("https://github.com/nicolas-raoul/CmisSync/wiki/What-address");
                        address_help_label.Inlines.Add(link);
                        link.RequestNavigate += (sender, e) =>
                            {
                                System.Diagnostics.Process.Start(e.Uri.ToString());
                            };

                        TextBlock address_error_label = new TextBlock()
                        {
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(255, 128, 128)),
                            Visibility = Visibility.Hidden
                        };

                        // User
                        TextBlock user_label = new TextBlock()
                        {
                            Text = SparkleShare.Properties.Resources.ResourceManager.GetString("User", CultureInfo.CurrentCulture) + ":",
                            FontWeight = FontWeights.Bold,
                            Width = 200
                        };

                        TextBox user_box = new TextBox()
                        {
                            Width = 200,
                            Text = Controller.PreviousPath,
                            IsEnabled = (Controller.SelectedPlugin.Path == null)
                        };

                        TextBlock user_help_label = new TextBlock()
                        {
                            Text = Controller.SelectedPlugin.UserExample,
                            FontSize = 11,
                            Width = 200,
                            Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128))
                        };

                        // Password
                        TextBlock password_label = new TextBlock()
                        {
                            Text = SparkleShare.Properties.Resources.ResourceManager.GetString("Password", CultureInfo.CurrentCulture) + ":",
                            FontWeight = FontWeights.Bold,
                            Width = 200
                        };

                        PasswordBox password_box = new PasswordBox()
                        {
                            Width = 200,
                            IsEnabled = (Controller.SelectedPlugin.Path == null),
                        };

                        TextBlock password_help_label = new TextBlock()
                        {
                            Text = Controller.SelectedPlugin.PasswordExample,
                            FontSize = 11,
                            Width = 200,
                            Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128))
                        };
                        
                        Button cancel_button = new Button () {
                            Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Cancel", CultureInfo.CurrentCulture)
                        };
                        
                        Button continue_button = new Button () {
                            Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture)
                        };

						// Address
                        ContentCanvas.Children.Add (address_label);
                        Canvas.SetTop (address_label, 160);
                        Canvas.SetLeft (address_label, 185);
                        
                        ContentCanvas.Children.Add (address_box);
                        Canvas.SetTop (address_box, 180);
                        Canvas.SetLeft (address_box, 185);

                        ContentCanvas.Children.Add(address_help_label);
                        Canvas.SetTop(address_help_label, 205);
                        Canvas.SetLeft(address_help_label, 185);

                        ContentCanvas.Children.Add(address_error_label);
                        Canvas.SetTop(address_error_label, 235);
                        Canvas.SetLeft(address_error_label, 185);

                        // User
                        ContentCanvas.Children.Add(user_label);
                        Canvas.SetTop(user_label, 300);
                        Canvas.SetLeft(user_label, 185);

                        ContentCanvas.Children.Add(user_box);
                        Canvas.SetTop(user_box, 330);
                        Canvas.SetLeft(user_box, 185);

                        ContentCanvas.Children.Add(user_help_label);
                        Canvas.SetTop(user_help_label, 355);
                        Canvas.SetLeft(user_help_label, 185);

                        // Password
                        ContentCanvas.Children.Add(password_label);
                        Canvas.SetTop(password_label, 300);
                        Canvas.SetRight(password_label, 30);

                        ContentCanvas.Children.Add(password_box);
                        Canvas.SetTop(password_box, 330);
                        Canvas.SetRight(password_box, 30);

                        ContentCanvas.Children.Add(password_help_label);
                        Canvas.SetTop(password_help_label, 355);
                        Canvas.SetRight(password_help_label, 30);

                        TaskbarItemInfo.ProgressValue = 0.0;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
						
                        Buttons.Add (continue_button);
                        Buttons.Add (cancel_button);
                        
						address_box.Focus ();
                        address_box.Select (address_box.Text.Length, 0);
						

						Controller.ChangeAddressFieldEvent += delegate (string text,
                            string example_text, FieldState state) {
                            Dispatcher.BeginInvoke ((Action) delegate {
                                address_box.Text        = text;
                                address_box.IsEnabled   = (state == FieldState.Enabled);
                                address_help_label.Text = example_text;
                            });
                        };

                        Controller.ChangeUserFieldEvent += delegate(string text,
                            string example_text, FieldState state) {
                            Dispatcher.BeginInvoke((Action)delegate
                            {
                                user_box.Text = text;
                                user_box.IsEnabled = (state == FieldState.Enabled);
                                user_help_label.Text = example_text;
                            });
                        };

                        Controller.ChangePasswordFieldEvent += delegate(string text,
                            string example_text, FieldState state) {
                            Dispatcher.BeginInvoke((Action)delegate
                            {
                                password_box.Password = text;
                                password_box.IsEnabled = (state == FieldState.Enabled);
                                password_help_label.Text = example_text;
                            });
                        };

                        Controller.UpdateAddProjectButtonEvent += delegate(bool button_enabled)
                        {
                            Dispatcher.BeginInvoke ((Action) delegate {
                                continue_button.IsEnabled = button_enabled;
                            });
                        };
                        
                        Controller.CheckAddPage (address_box.Text);
                        
                        address_box.TextChanged += delegate {
                            Controller.CheckAddPage (address_box.Text);
                        };
                        
                        cancel_button.Click += delegate {
                            Controller.PageCancelled ();
                        };

                        continue_button.Click += delegate {
                            // Show wait cursor
                            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;

                            // Try to find the CMIS server
                            CmisServer cmisServer = CmisUtils.GetRepositoriesFuzzy(
                                address_box.Text, user_box.Text, password_box.Password);
                            Controller.repositories = cmisServer.repositories;
                            address_box.Text = cmisServer.url;

                            // Hide wait cursor
                            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;

                            if (Controller.repositories == null)
                            {
                                // Show warning
                                address_error_label.Text = SparkleShare.Properties.Resources.ResourceManager.GetString("Sorry", CultureInfo.CurrentCulture);
                                address_error_label.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                // Continue to folder selection
                                Controller.Add1PageCompleted(
                                    address_box.Text, user_box.Text, password_box.Password);
                            }
                        };
                        
                        break;
                    }

                    case PageType.Add2: {
                        Header = SparkleShare.Properties.Resources.ResourceManager.GetString("Which", CultureInfo.CurrentCulture);

                        System.Windows.Controls.TreeView treeView = new System.Windows.Controls.TreeView();
                        treeView.Width = 410;
                        treeView.Height = 267;
                        foreach (string repository in Controller.repositories)
                        {
                            System.Windows.Controls.TreeViewItem item = new System.Windows.Controls.TreeViewItem();
                            item.Tag = new SelectionTreeItem("/");
                            item.Header = repository;
                            treeView.Items.Add(item);
                        }
                        ContentCanvas.Children.Add(treeView);
                        Canvas.SetTop(treeView, 70);
                        Canvas.SetLeft(treeView, 185);

                        treeView.SelectedItemChanged += delegate
                        {
                            // Identify the selected remote path.
                            TreeViewItem item = (TreeViewItem)treeView.SelectedItem;
                            Controller.saved_remote_path = ((SelectionTreeItem)item.Tag).fullPath;

                            // Identify the selected repository.
                            object cursor = item;
                            while (cursor is TreeViewItem)
                            {
                                TreeViewItem treeViewItem = (TreeViewItem)cursor;
                                cursor = treeViewItem.Parent;
                                if ( ! (cursor is TreeViewItem))
                                {
                                    Controller.saved_repository = (string)treeViewItem.Header;
                                }
                            }
                            
                            // Load sub-folders if it has not been done already.
                            // We use each item's Tag to store metadata: whether this item's subfolders have been loaded or not.
                            if ( ((SelectionTreeItem)item.Tag).childrenLoaded == false)
                            {
                                System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;

                                // Get list of subfolders
                                string[] subfolders = CmisUtils.GetSubfolders(Controller.saved_repository, Controller.saved_remote_path,
                                    Controller.saved_address, Controller.saved_user, Controller.saved_password);

                                // Create a sub-item for each subfolder
                                foreach (string subfolder in subfolders)
                                {
                                    System.Windows.Controls.TreeViewItem subItem =
                                        new System.Windows.Controls.TreeViewItem();
                                    subItem.Tag = new SelectionTreeItem(subfolder);
                                    subItem.Header = Path.GetFileName(subfolder);
                                    item.Items.Add(subItem);
                                }
                                ((SelectionTreeItem)item.Tag).childrenLoaded = true;
                                item.ExpandSubtree();
                                System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;
                            }
                        };

                        Button cancel_button = new Button()
                        {
                            Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Cancel", CultureInfo.CurrentCulture)
                        };

                        Button add_button = new Button()
                        {
                            Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Add", CultureInfo.CurrentCulture)
                        };

                        Buttons.Add(add_button);
                        Buttons.Add(cancel_button);

                        cancel_button.Click += delegate
                        {
                            Controller.PageCancelled();
                        };

                        add_button.Click += delegate
                        {
                            Controller.Add2PageCompleted(
                                Controller.saved_repository, Controller.saved_remote_path);
                        };

                        break;
                    }

                        
                    case PageType.Syncing: {
                        Header      = SparkleShare.Properties.Resources.ResourceManager.GetString("AddingFolder", CultureInfo.CurrentCulture) + " ‘" + Controller.SyncingFolder + "’…";
                        Description = SparkleShare.Properties.Resources.ResourceManager.GetString("MayTakeTime", CultureInfo.CurrentCulture);

                        Button finish_button = new Button () {
                            Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Finish", CultureInfo.CurrentCulture),
                            IsEnabled = false
                        };

                        Button cancel_button = new Button () {
                            Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Cancel", CultureInfo.CurrentCulture)
                        };

                        ProgressBar progress_bar = new ProgressBar () {
                            Width  = 414,
                            Height = 15,
                            Value  = Controller.ProgressBarPercentage
                        };
                        
						
                        ContentCanvas.Children.Add (progress_bar);
                        Canvas.SetLeft (progress_bar, 185);
                        Canvas.SetTop (progress_bar, 150);
                        
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
						
                        Buttons.Add (cancel_button);
                        Buttons.Add(finish_button);
                                                   
                         
                        Controller.UpdateProgressBarEvent += delegate (double percentage) {
                            Dispatcher.BeginInvoke ((Action) delegate {
                                progress_bar.Value = percentage;	
								TaskbarItemInfo.ProgressValue = percentage / 100;
                            });
                        };
    
                        cancel_button.Click += delegate {
                            Controller.SyncingCancelled ();
                        };
                        
                        break;
                    }
                        
                        
                    case PageType.Error: {
                        Header      = "Oops! Something went wrong…";
                        Description = "Please check the following:";

						TextBlock help_block = new TextBlock () {
							TextWrapping = TextWrapping.Wrap,
                			Width        = 310	
						};

                        TextBlock bullets_block = new TextBlock () {
                            Text = "•\n\n\n•"
                        };

                        help_block.Inlines.Add (new Bold (new Run (Controller.PreviousUrl)));
                        help_block.Inlines.Add (" is the address we've compiled. Does this look alright?\n\n");
                        help_block.Inlines.Add ("Do you have access rights to this remote project?");

                        if (warnings.Length > 0) {
                            bullets_block.Text += "\n\n•";
                            help_block.Inlines.Add ("\n\nHere's the raw error message:");

                            foreach (string warning in warnings) {
                                help_block.Inlines.Add ("\n");
                                help_block.Inlines.Add (new Bold (new Run (warning)));
                            }
                        }

                        Button cancel_button = new Button () {
                            Content = "Cancel"
                        };
                        
                        Button try_again_button = new Button () {
                            Content = "Try again…"
                        };

						
				        ContentCanvas.Children.Add (bullets_block);
                        Canvas.SetLeft (bullets_block, 195);
                        Canvas.SetTop (bullets_block, 100);
						
				        ContentCanvas.Children.Add (help_block);
                        Canvas.SetLeft (help_block, 210);
                        Canvas.SetTop (help_block, 100);
                        
						TaskbarItemInfo.ProgressValue = 1.0;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;
						
                        Buttons.Add (try_again_button);
                        Buttons.Add (cancel_button);
    
                        
                        cancel_button.Click += delegate {
                            Controller.PageCancelled ();
                        };
                        
                        try_again_button.Click += delegate {
                            Controller.ErrorPageCompleted ();
                        };
                        
                        break;
                    }    
                        
                    case PageType.Finished: {
                        Header      = SparkleShare.Properties.Resources.ResourceManager.GetString("Ready", CultureInfo.CurrentCulture);
                        Description = SparkleShare.Properties.Resources.ResourceManager.GetString("YouCanFind", CultureInfo.CurrentCulture);
                        
                        Button finish_button = new Button () {
                            Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Finish", CultureInfo.CurrentCulture)
                        };
    
                        Button open_folder_button = new Button () {
                            Content = SparkleShare.Properties.Resources.ResourceManager.GetString("OpenFolder", CultureInfo.CurrentCulture)
                        };

                        if (warnings.Length > 0) {
							Image warning_image = new Image () {
								Source = Imaging.CreateBitmapSourceFromHIcon (Drawing.SystemIcons.Information.Handle,
                                	Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions ())
							};
							
                            TextBlock warning_block = new TextBlock () {
								Text         = warnings [0],
								Width        = 310,
								TextWrapping = TextWrapping.Wrap
							};
							                                                    
					        ContentCanvas.Children.Add (warning_image);
	                        Canvas.SetLeft (warning_image, 193);
	                        Canvas.SetTop (warning_image, 100);
							                                                    
					        ContentCanvas.Children.Add (warning_block);
	                        Canvas.SetLeft (warning_block, 240);
	                        Canvas.SetTop (warning_block, 100);
						}
						
						TaskbarItemInfo.ProgressValue = 0.0;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

                        Buttons.Add(open_folder_button);
                        Buttons.Add (finish_button);
                        

                        finish_button.Click += delegate {
                            Controller.FinishPageCompleted ();
                        };

                        open_folder_button.Click += delegate {
                            Controller.OpenFolderClicked ();
                        };
                        
                       
                        SystemSounds.Exclamation.Play ();
                        
                        break;
                    }
                        
                    case PageType.Tutorial: {
                        
                        switch (Controller.TutorialPageNumber) {
                            case 1: {
                                Header = SparkleShare.Properties.Resources.ResourceManager.GetString("WhatsNext", CultureInfo.CurrentCulture);
                                Description = SparkleShare.Properties.Resources.ResourceManager.GetString("CmisSyncCreates", CultureInfo.CurrentCulture);
    
                            
                                WPF.Image slide_image = new WPF.Image () {
                                    Width  = 350,
                                    Height = 200
                                };
                            
                                slide_image.Source = SparkleUIHelpers.GetImageSource ("tutorial-slide-1");
                            
								Button skip_tutorial_button = new Button () {
                                    Content = SparkleShare.Properties.Resources.ResourceManager.GetString("SkipTutorial", CultureInfo.CurrentCulture)
								};
								
								Button continue_button = new Button () {
									Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture)
								};
							
                            
                                ContentCanvas.Children.Add (slide_image);
                                Canvas.SetLeft (slide_image, 215);
                                Canvas.SetTop (slide_image, 130);
                            
								Buttons.Add (continue_button);
								Buttons.Add (skip_tutorial_button);
                               
                                
                                skip_tutorial_button.Click += delegate {
                                    Controller.TutorialSkipped ();
                                };
                                
                                continue_button.Click += delegate {
                                    Controller.TutorialPageCompleted ();
                                };
                            
                                break;
                            }
                        
                            case 2: {
                                Header      = SparkleShare.Properties.Resources.ResourceManager.GetString("Synchronization", CultureInfo.CurrentCulture);
                                Description = SparkleShare.Properties.Resources.ResourceManager.GetString("DocumentsAre", CultureInfo.CurrentCulture);
    
                            
                                Button continue_button = new Button () {
                                    Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture)
                                };
                                
                                WPF.Image slide_image = new WPF.Image () {
                                    Width  = 350,
                                    Height = 200
                                };
                            
                                slide_image.Source = SparkleUIHelpers.GetImageSource ("tutorial-slide-2");
                            
                            
                                ContentCanvas.Children.Add (slide_image);
                                Canvas.SetLeft (slide_image, 215);
                                Canvas.SetTop (slide_image, 130);
                            
                                Buttons.Add (continue_button);
                            
                                
                                continue_button.Click += delegate {
                                    Controller.TutorialPageCompleted ();
                                };
                            
                                break;
                            }
                            
                            case 3: {
                                Header      = SparkleShare.Properties.Resources.ResourceManager.GetString("StatusIcon", CultureInfo.CurrentCulture);
                                Description = SparkleShare.Properties.Resources.ResourceManager.GetString("StatusIconShows", CultureInfo.CurrentCulture);
    
                            
                                Button continue_button = new Button () {
                                    Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture)
                                };
                                
                                WPF.Image slide_image = new WPF.Image () {
                                    Width  = 350,
                                    Height = 200
                                };
                            
                                slide_image.Source = SparkleUIHelpers.GetImageSource ("tutorial-slide-3");
                            
                            
                                ContentCanvas.Children.Add (slide_image);
                                Canvas.SetLeft (slide_image, 215);
                                Canvas.SetTop (slide_image, 130);
                            
                                Buttons.Add (continue_button);                                
                            
                            
                                continue_button.Click += delegate {
                                    Controller.TutorialPageCompleted ();
                                };
                            
                                break;
                            }
                            
                            case 4: {
                                Header      = SparkleShare.Properties.Resources.ResourceManager.GetString("AddFolders", CultureInfo.CurrentCulture);
                                Description = SparkleShare.Properties.Resources.ResourceManager.GetString("YouCan", CultureInfo.CurrentCulture);
                            
                            
                                Button finish_button = new Button () {
                                    Content = SparkleShare.Properties.Resources.ResourceManager.GetString("Finish", CultureInfo.CurrentCulture)
                                };
                                
                                WPF.Image slide_image = new WPF.Image () {
                                    Width  = 350,
                                    Height = 200
                                };
                                
                                slide_image.Source = SparkleUIHelpers.GetImageSource ("tutorial-slide-4");
    
                                CheckBox check_box = new CheckBox () {
                                    Content   = SparkleShare.Properties.Resources.ResourceManager.GetString("Startup", CultureInfo.CurrentCulture),
                                    IsChecked = true
                                };
                            
                            
                                ContentCanvas.Children.Add (slide_image);
                                Canvas.SetLeft (slide_image, 215);
                                Canvas.SetTop (slide_image, 130);

                                ContentCanvas.Children.Add (check_box);
                                Canvas.SetLeft (check_box, 185);
                                Canvas.SetBottom (check_box, 12);
                                
                                Buttons.Add (finish_button);
                                
                                
                                check_box.Click += delegate {
                                    Controller.StartupItemChanged (check_box.IsChecked.Value);
                                };
                            
                                finish_button.Click += delegate {
                                    Controller.TutorialPageCompleted ();
                                };
    
                                break;
                            }
                        }
                        break;
                    }
                    }
                    
                    ShowAll ();
                });        
            };
        }
    }
}
