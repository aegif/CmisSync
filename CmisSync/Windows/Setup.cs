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
using CmisSync.CmisTree;
using System.Windows.Input;

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

                                Header = Properties_Resources.Welcome;
                                Description = Properties_Resources.Intro;

                                Button cancel_button = new Button()
                                {
                                    Content = Properties_Resources.Cancel
                                };

                                Button continue_button = new Button()
                                {
                                    Content = Properties_Resources.Continue,
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

                                            Header = Properties_Resources.WhatsNext;
                                            Description = Properties_Resources.CmisSyncCreates;

                                            WPF.Image slide_image = new WPF.Image()
                                            {
                                                Width = 350,
                                                Height = 200
                                            };

                                            slide_image.Source = UIHelpers.GetImageSource("tutorial-slide-1");

                                            Button skip_tutorial_button = new Button()
                                            {
                                                Content = Properties_Resources.SkipTutorial
                                            };

                                            Button continue_button = new Button()
                                            {
                                                Content = Properties_Resources.Continue
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

                                            Header = Properties_Resources.Synchronization;
                                            Description = Properties_Resources.DocumentsAre;


                                            Button continue_button = new Button()
                                            {
                                                Content = Properties_Resources.Continue
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

                                            Header = Properties_Resources.StatusIcon;
                                            Description = Properties_Resources.StatusIconShows;


                                            Button continue_button = new Button()
                                            {
                                                Content = Properties_Resources.Continue
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

                                            Header = Properties_Resources.AddFolders;
                                            Description = Properties_Resources.YouCan;


                                            Button finish_button = new Button()
                                            {
                                                Content = Properties_Resources.Finish
                                            };

                                            WPF.Image slide_image = new WPF.Image()
                                            {
                                                Width = 350,
                                                Height = 200
                                            };

                                            slide_image.Source = UIHelpers.GetImageSource("tutorial-slide-4");

                                            CheckBox check_box = new CheckBox()
                                            {
                                                Content = Properties_Resources.Startup,
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

                                Header = Properties_Resources.Where;

                                // Address input UI.
                                TextBlock address_label = new TextBlock()
                                {
                                    Text = Properties_Resources.EnterWebAddress,
                                    FontWeight = FontWeights.Bold
                                };

                                TextBox address_box = new TextBox()
                                {
                                    Width = 420,
                                    Text = Controller.PreviousAddress
                                };

                                TextBlock address_help_label = new TextBlock()
                                {
                                    Text = Properties_Resources.Help + ": ",
                                    FontSize = 11,
                                    Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128))
                                };
                                Run run = new Run(Properties_Resources.WhereToFind);
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
                                    Text = Properties_Resources.User + ":",
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
                                    Text = Properties_Resources.Password + ":",
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
                                    Content = Properties_Resources.Cancel
                                };

                                Button continue_button = new Button()
                                {
                                    Content = Properties_Resources.Continue
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
                                        address_error_label.Text = Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
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
                                                warning = Properties_Resources.LoginFailedForbidden;
                                                break;
                                            case "ConnectFailure":
                                                warning = Properties_Resources.ConnectFailure;
                                                break;
                                            case "SendFailure":
                                                if (cmisServer.Url.Scheme.StartsWith("https"))
                                                    warning = Properties_Resources.SendFailureHttps;
                                                else
                                                    goto default;
                                                break;
                                            default:
                                                warning = message + Environment.NewLine + Properties_Resources.Sorry;
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

                                Header = Properties_Resources.Which;

                                // A tree allowing the user to browse CMIS repositories/folders.
                                System.Uri resourceLocater = new System.Uri("/DataSpaceSync;component/TreeView.xaml", System.UriKind.Relative);
                                System.Windows.Controls.TreeView treeView = System.Windows.Application.LoadComponent(resourceLocater) as TreeView;

                                ObservableCollection<CmisRepo> repos = new ObservableCollection<CmisRepo>();

                                // Some CMIS servers hold several repositories (ex:Nuxeo). Show one root per repository.
                                foreach (KeyValuePair<String, String> repository in Controller.repositories)
                                {
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
                                    Content = Properties_Resources.Continue,
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
                                        continue_button.IsEnabled = true;
                                        return;
                                    }

                                    // Folder has been selected
                                    Folder folder = o as Folder;
                                    if (folder != null)
                                    {
                                        Controller.saved_remote_path = folder.Path;
                                        Controller.saved_repository = folder.Repo.Id;
                                        if (folder.IsIgnored)
                                        {
                                            continue_button.IsEnabled = false;
                                        }
                                        else
                                        {
                                            continue_button.IsEnabled = true;
                                        }
                                        return;
                                    }
                                    continue_button.IsEnabled = false;
                                };

                                

                                Button cancel_button = new Button()
                                {
                                    Content = Properties_Resources.Cancel
                                };



                                Button back_button = new Button()
                                {
                                    Content = Properties_Resources.Back
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
                                    List<string> ignored = new List<string>();
                                    CmisRepo repo = treeView.SelectedItem as CmisRepo;
                                    if (repo != null)
                                    {
                                        foreach (Folder child in repo.Folder)
                                            ignored.AddRange(getIgnoredFolder(child));
                                    }
                                    else
                                    {
                                        Folder folder = treeView.SelectedItem as Folder;
                                        if (folder != null)
                                            ignored.AddRange(getIgnoredFolder(folder));
                                    }
                                    Controller.Add2PageCompleted(
                                        Controller.saved_repository, Controller.saved_remote_path, ignored.ToArray());
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

                                Header = Properties_Resources.Customize;

                                // Customize local folder name
                                TextBlock localfolder_label = new TextBlock()
                                {
                                    Text = Properties_Resources.EnterLocalFolderName,
                                    FontWeight = FontWeights.Bold
                                };

                                TextBox localfolder_box = new TextBox()
                                {
                                    Width = 420,
                                    Text = Controller.SyncingReponame
                                };

                                TextBlock localrepopath_label = new TextBlock()
                                {
                                    Text = Properties_Resources.ChangeRepoPath,
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
                                    Content = Properties_Resources.Cancel
                                };

                                Button add_button = new Button()
                                {
                                    Content = Properties_Resources.Add
                                };

                                Button back_button = new Button()
                                {
                                    Content = Properties_Resources.Back
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
                                    localfolder_error_label.Text = Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                                    localfolder_error_label.Visibility = Visibility.Visible;
                                }
                                else localfolder_error_label.Visibility = Visibility.Hidden;

                                localfolder_box.TextChanged += delegate
                                {
                                    error = Controller.CheckRepoPathAndName(localrepopath_box.Text, localfolder_box.Text);
                                    if (!String.IsNullOrEmpty(error))
                                    {
                                        localfolder_error_label.Text = Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                                        localfolder_error_label.Visibility = Visibility.Visible;
                                    }
                                    else localfolder_error_label.Visibility = Visibility.Hidden;
                                };

                                // Repo path validity.

                                error = Controller.CheckRepoPathAndName(localrepopath_box.Text, localfolder_box.Text);
                                if (!String.IsNullOrEmpty(error))
                                {
                                    localfolder_error_label.Text = Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                                    localfolder_error_label.Visibility = Visibility.Visible;
                                }
                                else localfolder_error_label.Visibility = Visibility.Hidden;

                                localrepopath_box.TextChanged += delegate
                                {
                                    error = Controller.CheckRepoPathAndName(localrepopath_box.Text, localfolder_box.Text);
                                    if (!String.IsNullOrEmpty(error))
                                    {
                                        localfolder_error_label.Text = Properties_Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
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

                                Header = Properties_Resources.AddingFolder + " ‘" + Controller.SyncingReponame + "’…";
                                Description = Properties_Resources.MayTakeTime;

                                Button finish_button = new Button()
                                {
                                    Content = Properties_Resources.Finish,
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

                                Header = Properties_Resources.Ready;
                                Description = Properties_Resources.YouCanFind;

                                Button finish_button = new Button()
                                {
                                    Content = Properties_Resources.Finish
                                };

                                Button open_folder_button = new Button()
                                {
                                    Content = Properties_Resources.OpenFolder
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

        private List<string> getIgnoredFolder(Folder f)
        {
            List<string> result = new List<string>();
            if (f.IsIgnored)
            {
                result.Add(f.Path);
            }
            else
            {
                foreach (Folder child in f.SubFolder)
                    result.AddRange(getIgnoredFolder(child));
            }
            return result;
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
    
}
