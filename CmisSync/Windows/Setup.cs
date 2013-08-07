//   CmisSync, a collaboration and sharing tool.
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

using CmisSync.Lib.Cmis;
using CmisSync.Lib;
using System.Globalization;
using log4net;
using System.Collections.ObjectModel;

namespace CmisSync
{
    /// <summary>
    /// Dialog for the tutorial, and for the wizard to add a new remote folder.
    /// </summary>
    public class Setup : SetupWindow
    {
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(Setup));

        /// <summary>
        /// MVC controller.
        /// </summary>
        public SetupController Controller = new SetupController();

        delegate Tuple<CmisServer, Exception> GetRepositoriesFuzzyDelegate(Uri url, string user, string password);

        /// <summary>
        /// Constructor.
        /// </summary>
        public Setup()
        {
            Logger.Info("Entering constructor.");

            // Defines how to show the setup window.
            Controller.ShowWindowEvent += delegate
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    Logger.Info("Entering ShowWindowEvent.");
                    Show();
                    Activate();
                    BringIntoView();
                    Logger.Info("Exiting ShowWindowEvent.");
                });
            };

            // Defines how to hide the setup windows.
            Controller.HideWindowEvent += delegate
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    Hide();
                });
            };

            // Defines what to do when changing page.
            // The remote folder addition wizard has several steps.
            Controller.ChangePageEvent += delegate(PageType type)
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    Logger.Info("Entering ChangePageEvent.");
                    Reset();

                    // Show appropriate setup page.
                    switch (type)
                    {
                        // Welcome page that shows up at first run.
                        #region Page Setup
                        case PageType.Setup:
                            {
                                // UI elements.

                                Header = CmisSync.Properties_Resources.ResourceManager.GetString("Welcome", CultureInfo.CurrentCulture);
                                Description = CmisSync.Properties_Resources.ResourceManager.GetString("Intro", CultureInfo.CurrentCulture);

                                Button cancel_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Cancel", CultureInfo.CurrentCulture)
                                };

                                Button continue_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture),
                                    IsEnabled = false
                                };

                                Buttons.Add(continue_button);
                                Buttons.Add(cancel_button);

                                continue_button.Focus();

                                // Actions.

                                Controller.UpdateSetupContinueButtonEvent += delegate(bool enabled)
                                {
                                    Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        continue_button.IsEnabled = enabled;
                                    });
                                };

                                cancel_button.Click += delegate
                                {
                                    Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        Program.UI.StatusIcon.Dispose();
                                        Controller.SetupPageCancelled();
                                    });
                                };

                                continue_button.Click += delegate
                                {
                                    Controller.SetupPageCompleted();
                                };

                                Controller.CheckSetupPage();

                                break;
                            }
                        #endregion

                        #region Page Tutorial
                        case PageType.Tutorial:
                            {
                                switch (Controller.TutorialCurrentPage)
                                {
                                    // First page of the tutorial.
                                    case 1:
                                        {
                                            // UI elements.

                                            Header = CmisSync.Properties_Resources.ResourceManager.GetString("WhatsNext", CultureInfo.CurrentCulture);
                                            Description = CmisSync.Properties_Resources.ResourceManager.GetString("CmisSyncCreates", CultureInfo.CurrentCulture);

                                            WPF.Image slide_image = new WPF.Image()
                                            {
                                                Width = 350,
                                                Height = 200
                                            };

                                            slide_image.Source = UIHelpers.GetImageSource("tutorial-slide-1");

                                            Button skip_tutorial_button = new Button()
                                            {
                                                Content = CmisSync.Properties_Resources.ResourceManager.GetString("SkipTutorial", CultureInfo.CurrentCulture)
                                            };

                                            Button continue_button = new Button()
                                            {
                                                Content = CmisSync.Properties_Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture)
                                            };


                                            ContentCanvas.Children.Add(slide_image);
                                            Canvas.SetLeft(slide_image, 215);
                                            Canvas.SetTop(slide_image, 130);

                                            Buttons.Add(continue_button);
                                            Buttons.Add(skip_tutorial_button);

                                            // Actions.

                                            skip_tutorial_button.Click += delegate
                                            {
                                                Controller.TutorialSkipped();
                                            };

                                            continue_button.Click += delegate
                                            {
                                                Controller.TutorialPageCompleted();
                                            };

                                            break;
                                        }

                                    // Second page of the tutorial.
                                    case 2:
                                        {
                                            // UI elements.

                                            Header = CmisSync.Properties_Resources.ResourceManager.GetString("Synchronization", CultureInfo.CurrentCulture);
                                            Description = CmisSync.Properties_Resources.ResourceManager.GetString("DocumentsAre", CultureInfo.CurrentCulture);


                                            Button continue_button = new Button()
                                            {
                                                Content = CmisSync.Properties_Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture)
                                            };

                                            WPF.Image slide_image = new WPF.Image()
                                            {
                                                Width = 350,
                                                Height = 200
                                            };

                                            slide_image.Source = UIHelpers.GetImageSource("tutorial-slide-2");


                                            ContentCanvas.Children.Add(slide_image);
                                            Canvas.SetLeft(slide_image, 215);
                                            Canvas.SetTop(slide_image, 130);

                                            Buttons.Add(continue_button);

                                            // Actions.

                                            continue_button.Click += delegate
                                            {
                                                Controller.TutorialPageCompleted();
                                            };

                                            break;
                                        }

                                    // Third page of the tutorial.
                                    case 3:
                                        {
                                            // UI elements.

                                            Header = CmisSync.Properties_Resources.ResourceManager.GetString("StatusIcon", CultureInfo.CurrentCulture);
                                            Description = CmisSync.Properties_Resources.ResourceManager.GetString("StatusIconShows", CultureInfo.CurrentCulture);


                                            Button continue_button = new Button()
                                            {
                                                Content = CmisSync.Properties_Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture)
                                            };

                                            WPF.Image slide_image = new WPF.Image()
                                            {
                                                Width = 350,
                                                Height = 200
                                            };

                                            slide_image.Source = UIHelpers.GetImageSource("tutorial-slide-3");


                                            ContentCanvas.Children.Add(slide_image);
                                            Canvas.SetLeft(slide_image, 215);
                                            Canvas.SetTop(slide_image, 130);

                                            Buttons.Add(continue_button);

                                            // Actions.

                                            continue_button.Click += delegate
                                            {
                                                Controller.TutorialPageCompleted();
                                            };

                                            break;
                                        }

                                    // Fourth page of the tutorial.
                                    case 4:
                                        {
                                            // UI elements.

                                            Header = CmisSync.Properties_Resources.ResourceManager.GetString("AddFolders", CultureInfo.CurrentCulture);
                                            Description = CmisSync.Properties_Resources.ResourceManager.GetString("YouCan", CultureInfo.CurrentCulture);


                                            Button finish_button = new Button()
                                            {
                                                Content = CmisSync.Properties_Resources.ResourceManager.GetString("Finish", CultureInfo.CurrentCulture)
                                            };

                                            WPF.Image slide_image = new WPF.Image()
                                            {
                                                Width = 350,
                                                Height = 200
                                            };

                                            slide_image.Source = UIHelpers.GetImageSource("tutorial-slide-4");

                                            CheckBox check_box = new CheckBox()
                                            {
                                                Content = CmisSync.Properties_Resources.ResourceManager.GetString("Startup", CultureInfo.CurrentCulture),
                                                IsChecked = true
                                            };


                                            ContentCanvas.Children.Add(slide_image);
                                            Canvas.SetLeft(slide_image, 215);
                                            Canvas.SetTop(slide_image, 130);

                                            ContentCanvas.Children.Add(check_box);
                                            Canvas.SetLeft(check_box, 185);
                                            Canvas.SetBottom(check_box, 12);

                                            Buttons.Add(finish_button);

                                            // Actions.

                                            check_box.Click += delegate
                                            {
                                                Controller.StartupItemChanged(check_box.IsChecked.Value);
                                            };

                                            finish_button.Click += delegate
                                            {
                                                Controller.TutorialPageCompleted();
                                            };

                                            break;
                                        }
                                }
                                break;
                            }
                        #endregion

                        // First step of the remote folder addition dialog: Specifying the server.
                        #region Page Add1
                        case PageType.Add1:
                            {
                                // UI elements.

                                Header = CmisSync.Properties_Resources.ResourceManager.GetString("Where", CultureInfo.CurrentCulture);

                                // Address input UI.
                                TextBlock address_label = new TextBlock()
                                {
                                    Text = CmisSync.Properties_Resources.ResourceManager.GetString("EnterWebAddress", CultureInfo.CurrentCulture),
                                    FontWeight = FontWeights.Bold
                                };

                                TextBox address_box = new TextBox()
                                {
                                    Width = 420,
                                    Text = Controller.PreviousAddress
                                };

                                TextBlock address_help_label = new TextBlock()
                                {
                                    Text = CmisSync.Properties_Resources.ResourceManager.GetString("Help", CultureInfo.CurrentCulture) + ": ",
                                    FontSize = 11,
                                    Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128))
                                };
                                Run run = new Run(CmisSync.Properties_Resources.ResourceManager.GetString("WhereToFind", CultureInfo.CurrentCulture));
                                Hyperlink link = new Hyperlink(run);
                                link.NavigateUri = new Uri("https://github.com/nicolas-raoul/CmisSync/wiki/What-address");
                                address_help_label.Inlines.Add(link);
                                link.RequestNavigate += (sender, e) =>
                                    {
                                        System.Diagnostics.Process.Start(e.Uri.ToString());
                                    };

                                // Rather than a TextBlock, we use a textBox so that users can copy/paste the error message and Google it.
                                TextBox address_error_label = new TextBox()
                                {
                                    FontSize = 11,
                                    Foreground = new SolidColorBrush(Color.FromRgb(255, 128, 128)),
                                    Visibility = Visibility.Hidden,
                                    IsReadOnly = true,
                                    Background = Brushes.Transparent,
                                    BorderThickness = new Thickness(0),
                                    TextWrapping = TextWrapping.Wrap,
                                    MaxWidth = 420
                                };

                                // User input UI.
                                TextBlock user_label = new TextBlock()
                                {
                                    Text = CmisSync.Properties_Resources.ResourceManager.GetString("User", CultureInfo.CurrentCulture) + ":",
                                    FontWeight = FontWeights.Bold,
                                    Width = 200
                                };

                                TextBox user_box = new TextBox()
                                {
                                    Width = 200
                                };
                                if (Controller.saved_user == String.Empty || Controller.saved_user == null)
                                {
                                    user_box.Text = Environment.UserName;
                                }
                                else
                                {
                                    user_box.Text = Controller.saved_user;
                                }

                                TextBlock user_help_label = new TextBlock()
                                {
                                    FontSize = 11,
                                    Width = 200,
                                    Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128))
                                };

                                // Password input UI.
                                TextBlock password_label = new TextBlock()
                                {
                                    Text = CmisSync.Properties_Resources.ResourceManager.GetString("Password", CultureInfo.CurrentCulture) + ":",
                                    FontWeight = FontWeights.Bold,
                                    Width = 200
                                };

                                PasswordBox password_box = new PasswordBox()
                                {
                                    Width = 200
                                };

                                TextBlock password_help_label = new TextBlock()
                                {
                                    FontSize = 11,
                                    Width = 200,
                                    Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128))
                                };

                                // Buttons.
                                Button cancel_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Cancel", CultureInfo.CurrentCulture)
                                };

                                Button continue_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture)
                                };

                                Buttons.Add(continue_button);
                                Buttons.Add(cancel_button);

                                // Address
                                ContentCanvas.Children.Add(address_label);
                                Canvas.SetTop(address_label, 160);
                                Canvas.SetLeft(address_label, 185);

                                ContentCanvas.Children.Add(address_box);
                                Canvas.SetTop(address_box, 180);
                                Canvas.SetLeft(address_box, 185);

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

                                if (Controller.PreviousAddress == null || Controller.PreviousAddress == String.Empty)
                                    address_box.Text = "https://";
                                else
                                    address_box.Text = Controller.PreviousAddress;
                                address_box.Focus();
                                address_box.Select(address_box.Text.Length, 0);

                                // Actions.

                                Controller.ChangeAddressFieldEvent += delegate(string text,
                                    string example_text)
                                {
                                    Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        address_box.Text = text;
                                        address_help_label.Text = example_text;
                                    });
                                };

                                Controller.ChangeUserFieldEvent += delegate(string text,
                                    string example_text)
                                {
                                    Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        user_box.Text = text;
                                        user_help_label.Text = example_text;
                                    });
                                };

                                Controller.ChangePasswordFieldEvent += delegate(string text,
                                    string example_text)
                                {
                                    Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        password_box.Password = text;
                                        password_help_label.Text = example_text;
                                    });
                                };

                                Controller.UpdateAddProjectButtonEvent += delegate(bool button_enabled)
                                {
                                    Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        continue_button.IsEnabled = button_enabled;
                                    });
                                };

                                Controller.CheckAddPage(address_box.Text);

                                address_box.TextChanged += delegate
                                {
                                    string error = Controller.CheckAddPage(address_box.Text);
                                    if (!String.IsNullOrEmpty(error))
                                    {
                                        address_error_label.Text = CmisSync.Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                                        address_error_label.Visibility = Visibility.Visible;
                                    }
                                    else address_error_label.Visibility = Visibility.Hidden;
                                };

                                cancel_button.Click += delegate
                                {
                                    Controller.PageCancelled();
                                };

                                continue_button.Click += delegate
                                {
                                    // Show wait cursor
                                    System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;

                                    // Try to find the CMIS server (asynchronously)
                                    GetRepositoriesFuzzyDelegate dlgt =
                                        new GetRepositoriesFuzzyDelegate(CmisUtils.GetRepositoriesFuzzy);
                                    IAsyncResult ar = dlgt.BeginInvoke(new Uri(address_box.Text), user_box.Text,
                                        password_box.Password, null, null);
                                    while (!ar.AsyncWaitHandle.WaitOne(100)) {
                                        System.Windows.Forms.Application.DoEvents();
                                    }
                                    Tuple<CmisServer, Exception> result = dlgt.EndInvoke(ar);
                                    CmisServer cmisServer = result.Item1;
                                    
                                    Controller.repositories = cmisServer != null ? cmisServer.Repositories : null;

                                    address_box.Text = cmisServer.Url.ToString();

                                    // Hide wait cursor
                                    System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;

                                    if (Controller.repositories == null)
                                    {
                                        // Could not retrieve repositories list from server, show warning.
                                        string warning = "";
                                        string message = result.Item2.Message;
                                        switch (message)
                                        {
                                            case "Forbidden":
                                                warning = CmisSync.Properties_Resources.ResourceManager.GetString("LoginFailedForbidden", CultureInfo.CurrentCulture);
                                                break;
                                            case "ConnectFailure":
                                                warning = CmisSync.Properties_Resources.ResourceManager.GetString("ConnectFailure", CultureInfo.CurrentCulture);
                                                break;
                                            case "SendFailure":
                                                if (cmisServer.Url.Scheme.StartsWith("https"))
                                                    warning = CmisSync.Properties_Resources.ResourceManager.GetString("SendFailureHttps", CultureInfo.CurrentCulture);
                                                else
                                                    goto default;
                                                break;
                                            default:
                                                warning = message + Environment.NewLine + CmisSync.Properties_Resources.ResourceManager.GetString("Sorry", CultureInfo.CurrentCulture);
                                                break;
                                        }
                                        address_error_label.Text = warning;
                                        address_error_label.Visibility = Visibility.Visible;
                                    }
                                    else
                                    {
                                        // Continue to next step, which is choosing a particular folder.
                                        Controller.Add1PageCompleted(
                                            address_box.Text, user_box.Text, password_box.Password);
                                    }
                                };
                                break;
                            }
                        #endregion

                        // Second step of the remote folder addition dialog: choosing the folder.
                        #region Page Add2
                        case PageType.Add2:
                            {
                                // UI elements.

                                Header = CmisSync.Properties_Resources.ResourceManager.GetString("Which", CultureInfo.CurrentCulture);

                                // A tree allowing the user to browse CMIS repositories/folders.
                                System.Uri resourceLocater = new System.Uri("/DataSpaceSync;component/TreeView.xaml", System.UriKind.Relative);
                                System.Windows.Controls.TreeView treeView = System.Windows.Application.LoadComponent(resourceLocater) as TreeView;

                                ObservableCollection<CmisRepo> repos = new ObservableCollection<CmisRepo>();

                                // Some CMIS servers hold several repositories (ex:Nuxeo). Show one root per repository.
                                foreach (KeyValuePair<String, String> repository in Controller.repositories)
                                {
//                                    System.Windows.Controls.TreeViewItem item = new System.Windows.Controls.TreeViewItem();
//                                    item.Tag = new SelectionTreeItem(repository.Key, "/");
//                                    item.Header = repository.Value;
//                                    treeView.Items.Add(item);
                                    CmisRepo repo = new CmisRepo(Controller.saved_user, Controller.saved_password, Controller.saved_address)
                                    {
                                        Name = repository.Value,
                                        Id = repository.Key
                                    };
                                    repos.Add(repo);
                                    repo.asyncSubFolderLoading();

                                }
                                treeView.DataContext = repos;

                                ContentCanvas.Children.Add(treeView);
                                Canvas.SetTop(treeView, 70);
                                Canvas.SetLeft(treeView, 185);

                                Button continue_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture),
                                    IsEnabled = false
                                };

                                // Action: when an element in the tree is clicked, loads its children and show them.
                                treeView.SelectedItemChanged += delegate
                                {
                                    // Identify the selected remote path.
                                    object o = treeView.SelectedItem;

                                    // Root entry has been selected
                                    CmisRepo repo = o as CmisRepo;
                                    if (repo != null)
                                    {
                                        Controller.saved_remote_path = repo.Path;
                                        Controller.saved_repository = repo.Id;
                                    }

                                    // Folder has been selected
                                    Folder folder = o as Folder;
                                    if (folder != null)
                                    {
                                        Controller.saved_remote_path = folder.Path;
                                        Controller.saved_repository = folder.Repo.Id;
                                    }

                                    // Status Item has been selected
                                    StatusItem status = o as StatusItem;
                                    if (status != null)
                                    {
                                        continue_button.IsEnabled = false;
                                    }
                                    else
                                    {
                                        continue_button.IsEnabled = true;
                                    }
                                };

                                Button cancel_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Cancel", CultureInfo.CurrentCulture)
                                };



                                Button back_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Back", CultureInfo.CurrentCulture)
                                };

                                Buttons.Add(back_button);
                                Buttons.Add(continue_button);
                                Buttons.Add(cancel_button);

                                continue_button.Focus();

                                cancel_button.Click += delegate
                                {
                                    Controller.PageCancelled();
                                };

                                continue_button.Click += delegate
                                {
                                    Controller.Add2PageCompleted(
                                        Controller.saved_repository, Controller.saved_remote_path);
                                };

                                back_button.Click += delegate
                                {
                                    Controller.BackToPage1();
                                };
                                break;
                            }
                        #endregion

                        // Third step of the remote folder addition dialog: Customizing the local folder.
                        #region Page Customize
                        case PageType.Customize:
                            {
                                string parentFolder = Controller.DefaultRepoPath;

                                // UI elements.

                                Header = CmisSync.Properties_Resources.ResourceManager.GetString("Customize", CultureInfo.CurrentCulture);

                                // Customize local folder name
                                TextBlock localfolder_label = new TextBlock()
                                {
                                    Text = CmisSync.Properties_Resources.ResourceManager.GetString("EnterLocalFolderName", CultureInfo.CurrentCulture),
                                    FontWeight = FontWeights.Bold
                                };

                                TextBox localfolder_box = new TextBox()
                                {
                                    Width = 420,
                                    Text = Controller.SyncingReponame
                                };

                                TextBlock localrepopath_label = new TextBlock()
                                {
                                    Text = CmisSync.Properties_Resources.ResourceManager.GetString("ChangeRepoPath", CultureInfo.CurrentCulture),
                                    FontWeight = FontWeights.Bold
                                };

                                TextBox localrepopath_box = new TextBox()
                                {
                                    Width = 375,
                                    Text = Path.Combine(parentFolder, localfolder_box.Text)
                                };

                                localfolder_box.TextChanged += delegate
                                {
                                    localrepopath_box.Text = Path.Combine(parentFolder, localfolder_box.Text);
                                };

                                Button choose_folder_button = new Button()
                                {
                                    Width = 40,
                                    Content = "..."
                                };

                                TextBlock localfolder_error_label = new TextBlock()
                                {
                                    FontSize = 11,
                                    Foreground = new SolidColorBrush(Color.FromRgb(255, 128, 128)),
                                    Visibility = Visibility.Hidden,
                                    TextWrapping = TextWrapping.Wrap
                                };

                                Button cancel_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Cancel", CultureInfo.CurrentCulture)
                                };

                                Button add_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Add", CultureInfo.CurrentCulture)
                                };

                                Button back_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Back", CultureInfo.CurrentCulture)
                                };

                                Buttons.Add(back_button);
                                Buttons.Add(add_button);
                                Buttons.Add(cancel_button);

                                add_button.Focus();

                                // Local Folder Name
                                ContentCanvas.Children.Add(localfolder_label);
                                Canvas.SetTop(localfolder_label, 160);
                                Canvas.SetLeft(localfolder_label, 185);

                                ContentCanvas.Children.Add(localfolder_box);
                                Canvas.SetTop(localfolder_box, 180);
                                Canvas.SetLeft(localfolder_box, 185);

                                ContentCanvas.Children.Add(localrepopath_label);
                                Canvas.SetTop(localrepopath_label, 200);
                                Canvas.SetLeft(localrepopath_label, 185);

                                ContentCanvas.Children.Add(localrepopath_box);
                                Canvas.SetTop(localrepopath_box, 220);
                                Canvas.SetLeft(localrepopath_box, 185);

                                ContentCanvas.Children.Add(choose_folder_button);
                                Canvas.SetTop(choose_folder_button, 220);
                                Canvas.SetLeft(choose_folder_button, 565);

                                ContentCanvas.Children.Add(localfolder_error_label);
                                Canvas.SetTop(localfolder_error_label, 275);
                                Canvas.SetLeft(localfolder_error_label, 185);

                                TaskbarItemInfo.ProgressValue = 0.0;
                                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

                                localfolder_box.Focus();
                                localfolder_box.Select(localfolder_box.Text.Length, 0);

                                // Actions.

                                Controller.UpdateAddProjectButtonEvent += delegate(bool button_enabled)
                                {
                                    Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        add_button.IsEnabled = button_enabled;
                                    });
                                };

                                // Repo name validity.

                                string error = Controller.CheckRepoPathAndName(localrepopath_box.Text, localfolder_box.Text);

                                if (!String.IsNullOrEmpty(error))
                                {
                                    localfolder_error_label.Text = CmisSync.Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                                    localfolder_error_label.Visibility = Visibility.Visible;
                                }
                                else localfolder_error_label.Visibility = Visibility.Hidden;

                                localfolder_box.TextChanged += delegate
                                {
                                    error = Controller.CheckRepoPathAndName(localrepopath_box.Text, localfolder_box.Text);
                                    if (!String.IsNullOrEmpty(error))
                                    {
                                        localfolder_error_label.Text = CmisSync.Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                                        localfolder_error_label.Visibility = Visibility.Visible;
                                    }
                                    else localfolder_error_label.Visibility = Visibility.Hidden;
                                };

                                // Repo path validity.

                                error = Controller.CheckRepoPathAndName(localrepopath_box.Text, localfolder_box.Text);
                                if (!String.IsNullOrEmpty(error))
                                {
                                    localfolder_error_label.Text = CmisSync.Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                                    localfolder_error_label.Visibility = Visibility.Visible;
                                }
                                else localfolder_error_label.Visibility = Visibility.Hidden;

                                localrepopath_box.TextChanged += delegate
                                {
                                    error = Controller.CheckRepoPathAndName(localrepopath_box.Text, localfolder_box.Text);
                                    if (!String.IsNullOrEmpty(error))
                                    {
                                        localfolder_error_label.Text = CmisSync.Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                                        localfolder_error_label.Visibility = Visibility.Visible;
                                       
                                    }
                                    else localfolder_error_label.Visibility = Visibility.Hidden;
                                };

                                // Choose a folder.
                                choose_folder_button.Click += delegate
                                {
                                    System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
                                    if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                                    {
                                        parentFolder = folderBrowserDialog1.SelectedPath;
                                        localrepopath_box.Text = Path.Combine(parentFolder, localfolder_box.Text);
                                    }
                                };

                                // Other actions.

                                cancel_button.Click += delegate
                                {
                                    Controller.PageCancelled();
                                };

                                back_button.Click += delegate
                                {
                                    Controller.BackToPage2();
                                };

                                add_button.Click += delegate
                                {
                                    Controller.CustomizePageCompleted(localfolder_box.Text, localrepopath_box.Text);
                                };
                                break;
                            }
                        #endregion

                        // Fourth page of the remote folder addition dialog: starting to sync.
                        // TODO: This step should be removed. Now it appears just a brief instant, because sync is asynchronous.
                        #region Page Syncing
                        case PageType.Syncing:
                            {
                                // UI elements.

                                Header = CmisSync.Properties_Resources.ResourceManager.GetString("AddingFolder", CultureInfo.CurrentCulture) + " ‘" + Controller.SyncingReponame + "’…";
                                Description = CmisSync.Properties_Resources.ResourceManager.GetString("MayTakeTime", CultureInfo.CurrentCulture);

                                Button finish_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Finish", CultureInfo.CurrentCulture),
                                    IsEnabled = false
                                };

                                ProgressBar progress_bar = new ProgressBar()
                                {
                                    Width = 414,
                                    Height = 15,
                                    Value = Controller.ProgressBarPercentage
                                };


                                ContentCanvas.Children.Add(progress_bar);
                                Canvas.SetLeft(progress_bar, 185);
                                Canvas.SetTop(progress_bar, 150);

                                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;

                                Buttons.Add(finish_button);

                                // Actions.

                                Controller.UpdateProgressBarEvent += delegate(double percentage)
                                {
                                    Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        progress_bar.Value = percentage;
                                        TaskbarItemInfo.ProgressValue = percentage / 100;
                                    });
                                };

                                break;
                            }
                        #endregion

                        // Final page of the remote folder addition dialog: end of the addition wizard.
                        #region Page Finished
                        case PageType.Finished:
                            {
                                // UI elements.

                                Header = CmisSync.Properties_Resources.ResourceManager.GetString("Ready", CultureInfo.CurrentCulture);
                                Description = CmisSync.Properties_Resources.ResourceManager.GetString("YouCanFind", CultureInfo.CurrentCulture);

                                Button finish_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("Finish", CultureInfo.CurrentCulture)
                                };

                                Button open_folder_button = new Button()
                                {
                                    Content = CmisSync.Properties_Resources.ResourceManager.GetString("OpenFolder", CultureInfo.CurrentCulture)
                                };

                                TaskbarItemInfo.ProgressValue = 0.0;
                                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

                                Buttons.Add(open_folder_button);
                                Buttons.Add(finish_button);

                                // Actions.

                                finish_button.Click += delegate
                                {
                                    Controller.FinishPageCompleted();
                                };

                                open_folder_button.Click += delegate
                                {
                                    Controller.OpenFolderClicked();
                                };


                                SystemSounds.Exclamation.Play();

                                break;
                            }
                        #endregion
                    }

                    ShowAll();
                    Logger.Info("Exiting ChangePageEvent.");
                });
            };
            this.Closing += delegate
            {
                Controller.PageCancelled();
            };

            Controller.PageCancelled();
            Logger.Info("Exiting constructor.");
        }
    }

    /// <summary>
    /// Stores the metadata of an item in the folder selection dialog.
    /// </summary>
    public class SelectionTreeItem
    {
        /// <summary>
        /// Whether this item's children have been loaded yet.
        /// </summary>
        public bool childrenLoaded = false;

        /// <summary>
        /// Address of the repository.
        /// Only necessary for repository root nodes.
        /// </summary>
        public string repository;

        /// <summary>
        /// Full path to the item.
        /// </summary>
        public string fullPath;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SelectionTreeItem(string repository, string fullPath)
        {
            this.repository = repository;
            this.fullPath = fullPath;
        }
    }
    #region TreeView

        public class CmisRepo : INotifyPropertyChanged
        {
            private BackgroundWorker worker = new BackgroundWorker();
            private ObservableCollection<Folder> _folder = new ObservableCollection<Folder>();
            private string name;
            public string Name { get{return name;} set { SetField(ref name, value, "Name"); } }
            public string Id { get; set; }
            public string Path { get { return "/"; } }

            private string username;
            private string password;
            private string address;

            private object currentWorkingObject;

            private LoadingStatus status = LoadingStatus.START;
            public LoadingStatus Status { get { return status; } set { SetField(ref status, value, "Status"); } }
            public ObservableCollection<Folder> Folder
            {
                get { return _folder; }
            }

            public CmisRepo(string username, string password, string address) {
                this.username = username;
                this.password = password;
                this.address = address;
                worker.DoWork += new DoWorkEventHandler(DoWork);
                worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Finished);
                currentWorkingObject = this;
            }

            public void asyncSubFolderLoading()
            {
                if (status == LoadingStatus.START)
                {
                    status = LoadingStatus.LOADING;
                    this.worker.RunWorkerAsync();
                }
            }

            private void DoWork(object sender, DoWorkEventArgs e)
            {
                BackgroundWorker worker = sender as BackgroundWorker;
                e.Result = CmisUtils.GetSubfolders(Id, Path, address, username, password);
                
            }

            private void Finished(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    Status = LoadingStatus.REQUEST_FAILURE;
                }
                else if (e.Cancelled) {
                    Status = LoadingStatus.ABORTED;
                }
                else
                {
                    if (currentWorkingObject == this)
                    {
                        string[] subfolder = (string[])e.Result;
                        foreach (string f in subfolder)
                            this.Folder.Add(new Folder() { 
                                Repo = this,
                                Path = f,
                                Name = f.Split('/')[f.Split('/').Length-1]
                            });
                        Status = LoadingStatus.DONE;
                    }
                    else
                    {
                        
                    }
                }

            }


            // boiler-plate
            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
            protected bool SetField<T>(ref T field, T value, string propertyName)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }

        public class Folder : INotifyPropertyChanged
        {
            private CmisRepo repo;
            public CmisRepo Repo { get { return repo; } set { SetField(ref repo, value, "Repo"); } }
            private bool selected = false;
            public bool Selected { get { return selected; } set { SetField(ref selected, value, "Selected"); } }
            private string name;
            public string Name { get { return name; } set { SetField(ref name, value, "Name"); } }
            private string path;
            public string Path { get { return path; } set { SetField(ref path, value, "Path"); } }
            private bool ignored = false;
            public bool IsIgnored { get { return ignored; } set { if(SetField(ref ignored, value, "IsIgnored")&&ignored) _subfolder.Clear(); } }
            private LoadingStatus status = LoadingStatus.START;
            public LoadingStatus Status { get { return status; } set { SetField(ref status, value, "Status"); } }
            private ObservableCollection<Folder> _subfolder = new ObservableCollection<Folder>();
            public ObservableCollection<Folder> SubFolder { get { return _subfolder; } }
            private FolderType folderType = FolderType.REMOTE;
            public FolderType Type{ get {return folderType;} set { SetField(ref folderType, value, "Type"); } }

            public enum FolderType
            {
                LOCAL, REMOTE, BOTH
            }

            // boiler-plate
            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
            protected bool SetField<T>(ref T field, T value, string propertyName)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }

        public enum LoadingStatus
        {
            START, LOADING, ABORTED, REQUEST_FAILURE, DONE
        }

        public class StatusItem : INotifyPropertyChanged
        {
            private LoadingStatus status = LoadingStatus.LOADING;
            public LoadingStatus Status { get { return status; } set { if (SetField(ref status, value, "Status")) OnPropertyChanged("Message"); } }
            public string Message { get { return CmisSync.Properties_Resources.ResourceManager.GetString("LoadingStatus."+ status.ToString(), CultureInfo.CurrentCulture); ; } }
            
            
            // boiler-plate
            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
            protected bool SetField<T>(ref T field, T value, string propertyName)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }

        [ValueConversion(typeof(LoadingStatus), typeof(ImageSource))]
        public class LoadingStatusToImageConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                LoadingStatus status = (LoadingStatus)value;
                switch (status)
                {
                    case LoadingStatus.LOADING:
                        goto case LoadingStatus.START;
                    case LoadingStatus.START:
                        // TODO Load correct Bitmap
                        return null;
                    default:
                        return null;
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        [ValueConversion(typeof(LoadingStatus), typeof(Brush))]
        public class LoadingStatusToBrushConverter : IValueConverter
        {
            private Brush loadingBrush = Brushes.Yellow;
            public Brush LoadingBrush { get { return loadingBrush; } set { loadingBrush = value; } }
            private Brush abortBrush = Brushes.DarkGray;
            public Brush AbortBrush { get { return abortBrush; } set { abortBrush = value; } }
            private Brush failureBrush = Brushes.Red;
            public Brush FailureBrush { get { return failureBrush; } set { failureBrush = value; } }
            private Brush doneBrush = Brushes.Black;
            public Brush DoneBrush { get { return doneBrush; } set { doneBrush = value; } }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                LoadingStatus status = (LoadingStatus)value;
                switch (status)
                {
                    case LoadingStatus.LOADING:
                        return loadingBrush;
                    case LoadingStatus.ABORTED:
                        return abortBrush;
                    case LoadingStatus.REQUEST_FAILURE:
                        return failureBrush;
                    case LoadingStatus.DONE:
                        return doneBrush;
                    default:
                        return Brushes.Black;
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        [ValueConversion(typeof(Folder.FolderType), typeof(Brush))]
        public class ForlderTypeToBrushConverter : IValueConverter
        {
            private Brush localFolderBrush = Brushes.LightGray;
            public Brush LocalFolderBrush { get { return localFolderBrush; } set { localFolderBrush = value; } }
            private Brush remoteFolderBrush = Brushes.LightBlue;
            public Brush RemoteFolderBrush { get { return remoteFolderBrush; } set { remoteFolderBrush = value; } }
            private Brush bothFolderBrush = Brushes.LightGreen;
            public Brush BothFolderBrush { get { return bothFolderBrush; } set { bothFolderBrush = value; } }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Folder.FolderType type = (Folder.FolderType) value;
                switch (type)
                {
                    case Folder.FolderType.LOCAL:
                        return localFolderBrush;
                    case Folder.FolderType.REMOTE:
                        return remoteFolderBrush;
                    case Folder.FolderType.BOTH:
                        return bothFolderBrush;
                    default:
                        return Brushes.White;
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

    #endregion
}
