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
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Timers;
using System.Collections.Generic;
using System.Globalization;

using Gtk;
using Mono.Unix;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;

namespace CmisSync {

    public class Setup : SetupWindow {

        public SetupController Controller = new SetupController ();

        private ProgressBar progress_bar = new ProgressBar ();

        private static Gdk.Cursor hand_cursor = new Gdk.Cursor(Gdk.CursorType.Hand2);

        private string cancelText =
            CmisSync.Properties.Resources.ResourceManager.GetString("Cancel", CultureInfo.CurrentCulture);
        private string continueText =
            CmisSync.Properties.Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture);
        private string backText =
            CmisSync.Properties.Resources.ResourceManager.GetString("Back", CultureInfo.CurrentCulture);

        private void ShowSetupPage()
        {
            Header = CmisSync.Properties.Resources.ResourceManager.GetString("Welcome", CultureInfo.CurrentCulture);
            Description = CmisSync.Properties.Resources.ResourceManager.GetString("Intro", CultureInfo.CurrentCulture);

            Add(new Label("")); // Page must have at least one element in order to show Header and Descripton

            Button cancel_button = new Button (cancelText);
            cancel_button.Clicked += delegate {
                Controller.SetupPageCancelled ();
            };

            Button continue_button = new Button (continueText)
            {
                Sensitive = false
            };

            continue_button.Clicked += delegate (object o, EventArgs args) {
                Controller.SetupPageCompleted ();
            };

            AddButton (cancel_button);
            AddButton (continue_button);

            Controller.UpdateSetupContinueButtonEvent += delegate (bool button_enabled) {
                Application.Invoke (delegate {
                        continue_button.Sensitive = button_enabled;
                        });
            };

            Controller.CheckSetupPage ();
        }

        private void ShowAdd1Page()
        {

            Header = CmisSync.Properties.Resources.ResourceManager.GetString("Where", CultureInfo.CurrentCulture);

            VBox layout_vertical   = new VBox (false, 12);
            HBox layout_fields     = new HBox (true, 12);
            VBox layout_address    = new VBox (true, 0);
            HBox layout_address_help = new HBox(false, 3);
            VBox layout_user       = new VBox (true, 0);
            VBox layout_password   = new VBox (true, 0);

            // Address
            Label address_label = new Label()
            {
                UseMarkup = true,
                          Xalign = 0,
                          Markup = "<b>" + 
                              CmisSync.Properties.Resources.ResourceManager.GetString("EnterWebAddress", CultureInfo.CurrentCulture) +
                              "</b>"
            };

            Entry address_entry = new Entry () {
                Text = Controller.PreviousAddress,
                     ActivatesDefault = false
            };

            Label address_help_label = new Label()
            {
                Xalign = 0,
                       UseMarkup = true,
                       Markup = "<span foreground=\"#808080\" size=\"small\">" +
                           CmisSync.Properties.Resources.ResourceManager.GetString("Help", CultureInfo.CurrentCulture) + ": " +
                           "</span>"
            };
            EventBox address_help_urlbox = new EventBox();
            Label address_help_urllabel = new Label()
            {
                Xalign = 0,
                       UseMarkup = true,
                       Markup = "<span foreground=\"blue\" underline=\"single\" size=\"small\">" +
                           CmisSync.Properties.Resources.ResourceManager.GetString("WhereToFind", CultureInfo.CurrentCulture) +
                           "</span>"
            };
            address_help_urlbox.Add(address_help_urllabel);
            address_help_urlbox.ButtonPressEvent += delegate(object o, ButtonPressEventArgs args) {
                Process process = new Process();
                process.StartInfo.FileName  = "xdg-open";
                process.StartInfo.Arguments = "https://github.com/nicolas-raoul/CmisSync/wiki/What-address";
                process.Start ();
            };
            address_help_urlbox.EnterNotifyEvent += delegate(object o, EnterNotifyEventArgs args) {
                address_help_urlbox.GdkWindow.Cursor = hand_cursor;
            };

            Label address_error_label = new Label()
            {
                Xalign = 0,
                UseMarkup = true,
                Markup = ""
            };
            address_error_label.Hide();

            // User
            Entry user_entry = new Entry () {
                Text = Controller.PreviousPath,
                     //RM Sensitive = (Controller.SelectedPlugin.User == null),
                     ActivatesDefault = false
            };

            // Password
            Entry password_entry = new Entry () {
                Text = Controller.PreviousPath,
                     Visibility = false,
                     //RM Sensitive = (Controller.SelectedPlugin.Password == null),
                     ActivatesDefault = true
            };

            Controller.ChangeAddressFieldEvent += delegate (string text,
                    string example_text, FieldState state) {

                Application.Invoke (delegate {
                        address_entry.Text      = text;
                        address_entry.Sensitive = (state == FieldState.Enabled);
                        });
            };

            Controller.ChangeUserFieldEvent += delegate (string text,
                    string example_text, FieldState state) {

                Application.Invoke (delegate {
                        user_entry.Text      = text;
                        user_entry.Sensitive = (state == FieldState.Enabled);
                        });
            };

            Controller.ChangePasswordFieldEvent += delegate (string text,
                    string example_text, FieldState state) {

                Application.Invoke (delegate {
                        password_entry.Text      = text;
                        password_entry.Sensitive = (state == FieldState.Enabled);
                        });
            };

            address_entry.Changed += delegate {
                string error = Controller.CheckAddPage(address_entry.Text);
                if (!String.IsNullOrEmpty(error)) {
                    address_error_label.Markup = "<span foreground=\"red\">" + CmisSync.Properties.Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture) + "</span>";
                    address_error_label.Show();
                } else {
                    address_error_label.Hide();
                }
            };

            // Address
            layout_address_help.PackStart(address_help_label, false, false, 0);
            layout_address_help.PackStart(address_help_urlbox, false, false, 0);
            layout_address.PackStart (address_label, true, true, 0);
            layout_address.PackStart (address_entry, true, true, 0);
            layout_address.PackStart (layout_address_help, true, true, 0);
            layout_address.PackStart (address_error_label, true, true, 0);

            // User
            layout_user.PackStart (new Label () {
                    Markup = "<b>" + CmisSync.Properties.Resources.ResourceManager.GetString("User", CultureInfo.CurrentCulture) + ":</b>",
                    Xalign = 0
                    }, true, true, 0);
            layout_user.PackStart (user_entry, false, false, 0);

            // Password
            layout_password.PackStart (new Label () {
                    Markup = "<b>" + CmisSync.Properties.Resources.ResourceManager.GetString("Password", CultureInfo.CurrentCulture) + ":</b>",
                    Xalign = 0
                    }, true, true, 0);
            layout_password.PackStart (password_entry, false, false, 0);

            layout_fields.PackStart (layout_user);
            layout_fields.PackStart (layout_password);

            layout_vertical.PackStart (new Label (""), false, false, 0);
            layout_vertical.PackStart (layout_address, false, false, 0);
            layout_vertical.PackStart (layout_fields, false, false, 0);

            Add (layout_vertical);

            // Cancel button
            Button cancel_button = new Button (cancelText);

            cancel_button.Clicked += delegate {
                Controller.PageCancelled ();
            };

            // Continue button
            Button continue_button = new Button (continueText) {
                Sensitive = false
            };

            continue_button.Clicked += delegate {
                // Show wait cursor
                System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;

                // Try to find the CMIS server
                CmisServer cmisServer = CmisUtils.GetRepositoriesFuzzy(
                   address_entry.Text, user_entry.Text, password_entry.Text);
                Controller.repositories = cmisServer.repositories;
                address_entry.Text = cmisServer.url;

                // Hide wait cursor
                System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;

                if (Controller.repositories == null)
                {
                    // Show warning
                    address_error_label.Markup = "<span foreground=\"red\">" + CmisSync.Properties.Resources.ResourceManager.GetString("Sorry", CultureInfo.CurrentCulture) + "</span>";
                    address_error_label.Show();
                }
                else
                {
                    Console.WriteLine("Add1 successful");
                    // Continue to folder selection
                    Controller.Add1PageCompleted(
                            address_entry.Text, user_entry.Text, password_entry.Text);
                }
            };

            Controller.UpdateAddProjectButtonEvent += delegate (bool button_enabled) {
                Application.Invoke (delegate {
                        continue_button.Sensitive = button_enabled;                            
                        });
            };

            AddButton (cancel_button);
            AddButton (continue_button);
        }

        private void ShowAdd2Page()
        {

            Header = CmisSync.Properties.Resources.ResourceManager.GetString("Which", CultureInfo.CurrentCulture);

            Gtk.ListStore repoListStore = new Gtk.ListStore(typeof (string), typeof (string));
            foreach (KeyValuePair<String, String> repository in Controller.repositories)
            {
                repoListStore.AppendValues(repository.Key, repository.Value + " [" + repository.Key + "]");
            }
            Gtk.TreeView treeView = new Gtk.TreeView(repoListStore);
            treeView.HeadersVisible = false;
            treeView.Selection.Mode = SelectionMode.Single;
            treeView.AppendColumn("Name", new CellRendererText(), "text", 1);
            treeView.CursorChanged += delegate(object o, EventArgs args) {
                TreeSelection selection = (o as TreeView).Selection;
                TreeModel model;
                TreeIter iter;
                if (selection.GetSelected(out model, out iter)) {
                    Console.WriteLine("sel '{0}'", model.GetPath(iter));
                }
            };


            ScrolledWindow sw = new ScrolledWindow();
            sw.Add(treeView);
            Add(sw);

            Button cancel_button = new Button (cancelText);
            cancel_button.Clicked += delegate {
                Controller.PageCancelled ();
            };

            Button continue_button = new Button (continueText)
            {
                Sensitive = false
            };
            continue_button.Clicked += delegate (object o, EventArgs args) {
                Controller.SetupPageCompleted ();
            };

            Button back_button = new Button (backText)
            {
                Sensitive = true
            };
            back_button.Clicked += delegate (object o, EventArgs args) {
                Controller.BackToPage1();
            };

            AddButton(back_button);
            AddButton(continue_button);
            AddButton(cancel_button);
        }

        private void ShowCustomizePage()
        {
            Header = CmisSync.Properties.Resources.ResourceManager.GetString("Customize", CultureInfo.CurrentCulture);
            /*
            // Customize local folder name
            TextBlock localfolder_label = new TextBlock()
            {
            Text = CmisSync.Properties.Resources.ResourceManager.GetString("EnterLocalFolderName", CultureInfo.CurrentCulture),
            FontWeight = FontWeights.Bold
            };

            TextBox localfolder_box = new TextBox()
            {
            Width = 420,
            Text = Controller.SyncingReponame
            };

            TextBlock localrepopath_label = new TextBlock()
            {
            Text = CmisSync.Properties.Resources.ResourceManager.GetString("ChangeRepoPath", CultureInfo.CurrentCulture),
            FontWeight = FontWeights.Bold
            };

            TextBox localrepopath_box = new TextBox()
            {
            Width = 420,
            Text = Path.Combine(Controller.DefaultRepoPath, localfolder_box.Text)
            };

            localfolder_box.TextChanged += delegate
            {
            localrepopath_box.Text = Path.Combine(Controller.DefaultRepoPath, localfolder_box.Text);
            };

            TextBlock localfolder_error_label = new TextBlock()
            {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 128, 128)),
            Visibility = Visibility.Hidden
            };

            Button cancel_button = new Button(cancelText);

            Button add_button = new Button()
            {
            Content = CmisSync.Properties.Resources.ResourceManager.GetString("Add", CultureInfo.CurrentCulture)
            };

            Button back_button = new Button()
            {
            Content = CmisSync.Properties.Resources.ResourceManager.GetString("Back", CultureInfo.CurrentCulture)
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

            ContentCanvas.Children.Add(localfolder_error_label);
            Canvas.SetTop(localfolder_error_label, 275);
            Canvas.SetLeft(localfolder_error_label, 185);

            TaskbarItemInfo.ProgressValue = 0.0;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

            localfolder_box.Focus();
            localfolder_box.Select(localfolder_box.Text.Length, 0);

            Controller.UpdateAddProjectButtonEvent += delegate(bool button_enabled)
            {
                Dispatcher.BeginInvoke((Action)delegate
                        {
                        add_button.IsEnabled = button_enabled;
                        });
            };

            string error = Controller.CheckRepoName(localfolder_box.Text);

            if (!String.IsNullOrEmpty(error))
            {
                localfolder_error_label.Text = CmisSync.Properties.Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                localfolder_error_label.Visibility = Visibility.Visible;
            }
            else localfolder_error_label.Visibility = Visibility.Hidden;

            localfolder_box.TextChanged += delegate
            {
                error = Controller.CheckRepoName(localfolder_box.Text);
                if (!String.IsNullOrEmpty(error))
                {
                    localfolder_error_label.Text = CmisSync.Properties.Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                    localfolder_error_label.Visibility = Visibility.Visible;
                }
                else localfolder_error_label.Visibility = Visibility.Hidden;
            };

            error = Controller.CheckRepoPath(localrepopath_box.Text);
            if (!String.IsNullOrEmpty(error))
            {
                localfolder_error_label.Text = CmisSync.Properties.Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                localfolder_error_label.Visibility = Visibility.Visible;
            }
            else localfolder_error_label.Visibility = Visibility.Hidden;

            localrepopath_box.TextChanged += delegate
            {
                error = Controller.CheckRepoPath(localrepopath_box.Text);
                if (!String.IsNullOrEmpty(error))
                {
                    localfolder_error_label.Text = CmisSync.Properties.Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture);
                    localfolder_error_label.Visibility = Visibility.Visible;
                }
                else localfolder_error_label.Visibility = Visibility.Hidden;
            };

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
            */
        }

        private void ShowSyncingPage()
        {
            Header = CmisSync.Properties.Resources.ResourceManager.GetString("AddingFolder", CultureInfo.CurrentCulture)
                + " ‘" + Controller.SyncingReponame + "’…";
            Description = CmisSync.Properties.Resources.ResourceManager.GetString("MayTakeTime", CultureInfo.CurrentCulture);

            this.progress_bar.Fraction = Controller.ProgressBarPercentage / 100;

            Button finish_button = new Button () {
                Sensitive = false,
                          Label = "Finish"
            };

            Button cancel_button = new Button (cancelText);
            cancel_button.Clicked += delegate {
                Controller.SyncingCancelled ();
            };

            AddButton (cancel_button);
            AddButton (finish_button);

            Controller.UpdateProgressBarEvent += delegate (double percentage) {
                Application.Invoke (delegate {
                        this.progress_bar.Fraction = percentage / 100;
                        });
            };

            if (this.progress_bar.Parent != null) {
                (this.progress_bar.Parent as Container).Remove (this.progress_bar);
            }

            VBox bar_wrapper = new VBox (false, 0);
            bar_wrapper.PackStart (this.progress_bar, false, false, 15);

            Add (bar_wrapper);

        }

        private void ShowErrorPage(string [] warnings)
        {
            Header = "Oops! Something went wrong" + "…";

            VBox points = new VBox (false, 0);
            Image list_point_one   = new Image (UIHelpers.GetIcon ("go-next", 16));
            Image list_point_two   = new Image (UIHelpers.GetIcon ("go-next", 16));
            Image list_point_three = new Image (UIHelpers.GetIcon ("go-next", 16));

            Label label_one = new Label () {
                Markup = "<b>" + Controller.PreviousUrl + "</b> is the address we've compiled. " +
                    "Does this look alright?",
                    Wrap   = true,
                    Xalign = 0
            };

            Label label_two = new Label () {
                Text   = "Do you have access rights to this remote project?",
                       Wrap   = true,
                       Xalign = 0
            };

            points.PackStart (new Label ("Please check the following:") { Xalign = 0 }, false, false, 6);

            HBox point_one = new HBox (false, 0);
            point_one.PackStart (list_point_one, false, false, 0);
            point_one.PackStart (label_one, true, true, 12);
            points.PackStart (point_one, false, false, 12);

            HBox point_two = new HBox (false, 0);
            point_two.PackStart (list_point_two, false, false, 0);
            point_two.PackStart (label_two, true, true, 12);
            points.PackStart (point_two, false, false, 12);

            if (warnings.Length > 0) {
                string warnings_markup = "";

                foreach (string warning in warnings)
                    warnings_markup += "\n<b>" + warning + "</b>";

                Label label_three = new Label () {
                    Markup = "Here's the raw error message:" + warnings_markup,
                           Wrap   = true,
                           Xalign = 0
                };

                HBox point_three = new HBox (false, 0);
                point_three.PackStart (list_point_three, false, false, 0);
                point_three.PackStart (label_three, true, true, 12);
                points.PackStart (point_three, false, false, 12);
            }

            points.PackStart (new Label (""), true, true, 0);

            Button cancel_button = new Button (cancelText);

            cancel_button.Clicked += delegate {
                Controller.PageCancelled ();
            };

            Button try_again_button = new Button ("Try Again…") {
                Sensitive = true
            };

            try_again_button.Clicked += delegate {
                Controller.ErrorPageCompleted ();
            };

            AddButton (cancel_button);
            AddButton (try_again_button);
            Add (points);

        }

        private void ShowFinishedPage()
        {
            UrgencyHint = true;
            //RM
            /*
               if (!HasToplevelFocus) {
               string title   = "Your documents are ready!";
               string subtext = "You can find them in your CmisSync folder.";

               Program.UI.Bubbles.Controller.ShowBubble (title, subtext, null);
               }
               */

            Header = CmisSync.Properties.Resources.ResourceManager.GetString("Ready", CultureInfo.CurrentCulture);
            Description = CmisSync.Properties.Resources.ResourceManager.GetString("YouCanFind", CultureInfo.CurrentCulture);

            // A button that opens the synced folder
            Button open_folder_button = new Button (string.Format ("Open {0}",
                        System.IO.Path.GetFileName (Controller.PreviousPath)));

            open_folder_button.Clicked += delegate {
                Controller.OpenFolderClicked ();
            };

            Button finish_button = new Button ("Finish");

            finish_button.Clicked += delegate {
                Controller.FinishPageCompleted ();
            };

            //RM
            /*
               if (warnings.Length > 0) {
               Image warning_image = new Image (
               UIHelpers.GetIcon ("dialog-information", 24)
               );

               Label warning_label = new Label (warnings [0]) {
               Xalign = 0,
               Wrap   = true
               };

               HBox warning_layout = new HBox (false, 0);
               warning_layout.PackStart (warning_image, false, false, 15);
               warning_layout.PackStart (warning_label, true, true, 0);

               VBox warning_wrapper = new VBox (false, 0);
               warning_wrapper.PackStart (warning_layout, false, false, 0);

               Add (warning_wrapper);

               } else {
               Add (null);
               }
               */

            AddButton (open_folder_button);
            AddButton (finish_button);

        }

        private void ShowTutorialPage()
        {
            switch (Controller.TutorialPageNumber) {
                case 1:
                    {
                        Header      = "What's happening next?";
                        Description = "CmisSync creates a special folder on your computer " +
                            "that will keep track of your folders.";

                        Button skip_tutorial_button = new Button ("Skip Tutorial");
                        skip_tutorial_button.Clicked += delegate {
                            Controller.TutorialSkipped ();
                        };

                        Button continue_button = new Button (continueText);
                        continue_button.Clicked += delegate {
                            Controller.TutorialPageCompleted ();
                        };

                        Image slide = UIHelpers.GetImage ("tutorial-slide-1.png");

                        Add (slide);

                        AddButton (skip_tutorial_button);
                        AddButton (continue_button);

                    }
                    break;

                case 2:
                    {
                        Header      = "Sharing files with others";
                        Description = "All files added to the server are automatically synced to your " +
                            "local folder.";

                        Button continue_button = new Button (continueText);
                        continue_button.Clicked += delegate {
                            Controller.TutorialPageCompleted ();
                        };

                        Image slide = UIHelpers.GetImage ("tutorial-slide-2.png");

                        Add (slide);
                        AddButton (continue_button);

                    }
                    break;

                case 3:
                    {
                        Header      = "The status icon is here to help";
                        Description = "It shows the syncing progress, provides easy access to " +
                            "your folders and let's you view recent changes.";

                        Button continue_button = new Button (continueText);
                        continue_button.Clicked += delegate {
                            Controller.TutorialPageCompleted ();
                        };

                        Image slide = UIHelpers.GetImage ("tutorial-slide-3.png");

                        Add (slide);
                        AddButton (continue_button);

                    }
                    break;

                case 4:
                    {
                        Header      = "Adding repository folders to CmisSync";
                        Description = "           " +
                            "           ";

                        Image slide = UIHelpers.GetImage ("tutorial-slide-4.png");

                        Button finish_button = new Button ("Finish");
                        finish_button.Clicked += delegate {
                            Controller.TutorialPageCompleted ();
                        };


                        CheckButton check_button = new CheckButton ("Add CmisSync to startup items") {
                            Active = true
                        };

                        check_button.Toggled += delegate {
                            Controller.StartupItemChanged (check_button.Active);
                        };

                        Add (slide);
                        AddOption (check_button);
                        AddButton (finish_button);

                    }
                    break;
            }

        }

        public Setup () : base ()
        {
            Controller.HideWindowEvent += delegate {
                Application.Invoke (delegate {
                        HideAll ();
                        });
            };

            Controller.ShowWindowEvent += delegate {
                Application.Invoke (delegate {
                        ShowAll ();
                        Present ();
                        });
            };

            Controller.ChangePageEvent += delegate (PageType ptype, string [] warnings) {
                Application.Invoke (delegate {
                        Reset ();

                        switch (ptype) {
                        case PageType.Setup:
                        ShowSetupPage();
                        break;

                        case PageType.Add1:
                        ShowAdd1Page();
                        break;

                        case PageType.Add2:
                        ShowAdd2Page();
                        break;

                        case PageType.Customize:
                        ShowCustomizePage();
                        break;

                        case PageType.Syncing:
                        ShowSyncingPage();
                        break;

                        case PageType.Error:
                        ShowErrorPage(warnings);
                        break;

                        case PageType.Finished:
                        ShowFinishedPage();
                        break;


                        case PageType.Tutorial:
                        ShowTutorialPage();
                        break;
                        }

                        ShowAll ();
                });
            };
        }

        private void RenderServiceColumn (TreeViewColumn column, CellRenderer cell,
                TreeModel model, TreeIter iter)
        {
            string markup           = (string) model.GetValue (iter, 1);
            TreeSelection selection = (column.TreeView as TreeView).Selection;

            if (selection.IterIsSelected (iter)) {
                if (column.TreeView.HasFocus)
                    markup = markup.Replace (SecondaryTextColor, SecondaryTextColorSelected);
                else
                    markup = markup.Replace (SecondaryTextColorSelected, SecondaryTextColor);
            } else {
                markup = markup.Replace (SecondaryTextColorSelected, SecondaryTextColor);
            }

            (cell as CellRendererText).Markup = markup;
        }
    }

}
