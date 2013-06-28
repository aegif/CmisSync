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
using System.Text.RegularExpressions;
using System.Timers;
using System.Collections.Generic;
using System.Globalization;

using Gtk;
using Mono.Unix;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;

namespace CmisSync {

    /**
     * Stores the metadata of an item in the folder selection dialog.
     */
    public class SelectionTreeItem
    {
        public bool childrenLoaded = false;
        public string repository; // Only necessary for repository root nodes.
        public string fullPath;
        public SelectionTreeItem(string repository, string fullPath)
        {
            this.repository = repository;
            this.fullPath = fullPath;
        }
    }

    public class Setup : SetupWindow {

        public SetupController Controller = new SetupController ();

        private ProgressBar progress_bar = new ProgressBar ();

        private static Gdk.Cursor hand_cursor = new Gdk.Cursor(Gdk.CursorType.Hand2);
        private static Gdk.Cursor wait_cursor = new Gdk.Cursor(Gdk.CursorType.Watch);
        private static Gdk.Cursor default_cursor = new Gdk.Cursor(Gdk.CursorType.LeftPtr);

        private string cancelText =
            CmisSync.Properties.Resources.ResourceManager.GetString("Cancel", CultureInfo.CurrentCulture);
        private string continueText =
            CmisSync.Properties.Resources.ResourceManager.GetString("Continue", CultureInfo.CurrentCulture);
        private string backText =
            CmisSync.Properties.Resources.ResourceManager.GetString("Back", CultureInfo.CurrentCulture);

        delegate CmisServer GetRepositoriesFuzzyDelegate(string url, string user, string password);

        delegate string[] GetSubfoldersDelegate(string repositoryId, string path,
            string address, string user, string password);

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
                this.GdkWindow.Cursor = wait_cursor;

                // Try to find the CMIS server (asynchronous using a delegate)
                GetRepositoriesFuzzyDelegate dlgt =
                    new GetRepositoriesFuzzyDelegate(CmisUtils.GetRepositoriesFuzzy);
                IAsyncResult ar = dlgt.BeginInvoke(address_entry.Text, user_entry.Text,
                        password_entry.Text, null, null);
                while (!ar.AsyncWaitHandle.WaitOne(100)) {
                    while (Application.EventsPending()) {
                        Application.RunIteration();
                    }
                }
                CmisServer cmisServer = dlgt.EndInvoke(ar);

                Controller.repositories = cmisServer.repositories;
                address_entry.Text = cmisServer.url;

                // Hide wait cursor
                this.GdkWindow.Cursor = default_cursor;

                if (Controller.repositories == null)
                {
                    // Show warning
                    address_error_label.Markup = "<span foreground=\"red\">" + CmisSync.Properties.Resources.ResourceManager.GetString("Sorry", CultureInfo.CurrentCulture) + "</span>";
                    address_error_label.Show();
                }
                else
                {
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

            AddButton (continue_button);
            AddButton (cancel_button);

            address_entry.GrabFocus();
        }

        private void ShowAdd2Page()
        {

            Header = CmisSync.Properties.Resources.ResourceManager.GetString("Which", CultureInfo.CurrentCulture);

            VBox layout_vertical   = new VBox (false, 12);

            Button cancel_button = new Button (cancelText);
            cancel_button.Clicked += delegate {
                Controller.PageCancelled ();
            };

            Button continue_button = new Button (continueText)
            {
                Sensitive = false
            };
            continue_button.Clicked += delegate {
                Controller.Add2PageCompleted(
                        Controller.saved_repository, Controller.saved_remote_path);
            };

            Button back_button = new Button (backText)
            {
                Sensitive = true
            };
            back_button.Clicked += delegate {
                Controller.BackToPage1();
            };

            TreeStore repoStore = new Gtk.TreeStore(typeof (string), typeof (SelectionTreeItem));
            TreeIter iter;
            foreach (KeyValuePair<String, String> repository in Controller.repositories)
            {
                iter = repoStore.AppendNode();
                repoStore.SetValues(iter, repository.Value + " [" + repository.Key + "]", new SelectionTreeItem(repository.Key, "/"));
            }
            Gtk.TreeView treeView = new Gtk.TreeView(repoStore);
            treeView.HeadersVisible = false;
            treeView.Selection.Mode = SelectionMode.Single;
            treeView.AppendColumn("Name", new CellRendererText(), "text", 0);
            treeView.CursorChanged += delegate(object o, EventArgs args) {
                TreeSelection selection = (o as TreeView).Selection;
                TreeModel model;
                if (selection.GetSelected(out model, out iter)) {
                    SelectionTreeItem sti = model.GetValue(iter, 1) as SelectionTreeItem;

                    // Identify the selected remote path.
                    Controller.saved_remote_path = sti.fullPath;

                    // Identify the selected repository.
                    TreeIter cnode = iter;
                    TreeIter pnode = iter;
                    while (model.IterParent(out pnode, cnode)) {
                        cnode = pnode;
                    }
                    Controller.saved_repository = (model.GetValue(cnode, 1) as SelectionTreeItem).repository;

                    // Load sub-folders if it has not been done already.
                    // We use each item's Tag to store metadata: whether this item's subfolders have been loaded or not.
                    if (sti.childrenLoaded == false)
                    {
                        this.GdkWindow.Cursor = wait_cursor;

                        // Get list of subfolders asynchronously
                        GetSubfoldersDelegate dlgt = new GetSubfoldersDelegate(CmisUtils.GetSubfolders);
                        IAsyncResult ar = dlgt.BeginInvoke(Controller.saved_repository,
                                Controller.saved_remote_path, Controller.saved_address,
                                Controller.saved_user, Controller.saved_password, null, null);
                        while (!ar.AsyncWaitHandle.WaitOne(100)) {
                            while (Application.EventsPending()) {
                                Application.RunIteration();
                            }
                        }
                        string[] subfolders = dlgt.EndInvoke(ar);

                        TreePath tp = null;
                        // Create a sub-item for each subfolder
                        foreach (string subfolder in subfolders) {
                            TreeIter newchild = repoStore.AppendNode(iter);
                            repoStore.SetValues(newchild, subfolder, new SelectionTreeItem(null, subfolder));
                            if (null == tp) {
                                tp = repoStore.GetPath(newchild);
                            }
                        }
                        sti.childrenLoaded = true;
                        if (null != tp) {
                            treeView.ExpandToPath(tp);
                        }
                        this.GdkWindow.Cursor = default_cursor;
                    }
                    continue_button.Sensitive = true;

                }
            };

            ScrolledWindow sw = new ScrolledWindow() {
                ShadowType = Gtk.ShadowType.In
            };
            sw.Add(treeView);

            layout_vertical.PackStart (new Label(""), false, false, 0);
            layout_vertical.PackStart (sw, true, true, 0);
            Add(layout_vertical);
            AddButton(back_button);
            AddButton(continue_button);
            AddButton(cancel_button);
        }

        private void ShowCustomizePage()
        {
            Header = CmisSync.Properties.Resources.ResourceManager.GetString("Customize", CultureInfo.CurrentCulture);

            Label localfolder_label = new Label() {
                Xalign = 0,
                       UseMarkup = true,
                       Markup = "<b>" + CmisSync.Properties.Resources.ResourceManager.GetString("EnterLocalFolderName", CultureInfo.CurrentCulture) + "</b>"
            };

            Entry localfolder_entry = new Entry() {
                Text = Controller.SyncingReponame,
                     ActivatesDefault = false
            };

            Label localrepopath_label = new Label() {
                Xalign = 0,
                       UseMarkup = true,
                       Markup = "<b>" + CmisSync.Properties.Resources.ResourceManager.GetString("ChangeRepoPath", CultureInfo.CurrentCulture) + "</b>"
            };

            Entry localrepopath_entry = new Entry() {
                Text = System.IO.Path.Combine(Controller.DefaultRepoPath, localfolder_entry.Text)
            };

            localfolder_entry.Changed += delegate {
                localrepopath_entry.Text = System.IO.Path.Combine(Controller.DefaultRepoPath, localfolder_entry.Text);
            };

            Label localfolder_error_label = new Label() {
                Xalign = 0,
                       UseMarkup = true,
                       Markup = ""
            };

            Button cancel_button = new Button(cancelText);

            Button add_button = new Button(
                    CmisSync.Properties.Resources.ResourceManager.GetString("Add", CultureInfo.CurrentCulture)
                    );

            Button back_button = new Button(
                    CmisSync.Properties.Resources.ResourceManager.GetString("Back", CultureInfo.CurrentCulture)
                    );

            Controller.UpdateAddProjectButtonEvent += delegate(bool button_enabled) {
                Gtk.Application.Invoke(delegate {
                        add_button.Sensitive = button_enabled;
                        });
            };

            string error = Controller.CheckRepoName(localfolder_entry.Text);
            if (!String.IsNullOrEmpty(error)) {
                localfolder_error_label.Markup = "<span foreground=\"#ff8080\">" +
                    CmisSync.Properties.Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture) +
                    "</span>";
                localfolder_error_label.Show();
            } else {
                localfolder_error_label.Hide();
            }

            localfolder_entry.Changed += delegate {
                error = Controller.CheckRepoName(localfolder_entry.Text);
                if (!String.IsNullOrEmpty(error)) {
                    localfolder_error_label.Markup = "<span foreground=\"#ff8080\">" +
                        CmisSync.Properties.Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture) +
                        "</span>";
                    localfolder_error_label.Show();
                } else {
                    localfolder_error_label.Hide();
                }
            };

            error = Controller.CheckRepoPath(localrepopath_entry.Text);
            if (!String.IsNullOrEmpty(error)) {
                localfolder_error_label.Markup = "<span foreground=\"#ff8080\">" +
                    CmisSync.Properties.Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture) +
                "</span>";
                localfolder_error_label.Show();
            } else {
                localfolder_error_label.Hide();
            }

            localrepopath_entry.Changed += delegate {
                error = Controller.CheckRepoPath(localrepopath_entry.Text);
                if (!String.IsNullOrEmpty(error)) {
                    localfolder_error_label.Markup = "<span foreground=\"#ff8080\">" +
                        CmisSync.Properties.Resources.ResourceManager.GetString(error, CultureInfo.CurrentCulture) +
                        "</span>";
                    localfolder_error_label.Show();
                } else {
                    localfolder_error_label.Hide();
                }
            };

            cancel_button.Clicked += delegate {
                Controller.PageCancelled();
            };

            back_button.Clicked += delegate {
                Controller.BackToPage2();
            };

            add_button.Clicked += delegate {
                Controller.CustomizePageCompleted(localfolder_entry.Text, localrepopath_entry.Text);
            };

            VBox layout_vertical   = new VBox (false, 12);

            layout_vertical.PackStart (new Label(""), false, false, 0);
            layout_vertical.PackStart (localfolder_label, true, true, 0);
            layout_vertical.PackStart (localfolder_entry, true, true, 0);
            layout_vertical.PackStart (localrepopath_label, true, true, 0);
            layout_vertical.PackStart (localrepopath_entry, true, true, 0);
            layout_vertical.PackStart (localfolder_error_label, true, true, 0);
            Add(layout_vertical);
            AddButton(back_button);
            AddButton(add_button);
            AddButton(cancel_button);

            // add_button.GrabFocus();
            localfolder_entry.GrabFocus();
            localfolder_entry.SelectRegion(0, localfolder_entry.Text.Length);

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
            AddButton (cancel_button);
            AddButton (finish_button);

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

            Add(new Label(""));
            AddButton (open_folder_button);
            AddButton (finish_button);

            System.Media.SystemSounds.Exclamation.Play();
        }

        private void ShowTutorialPage()
        {
            switch (Controller.TutorialPageNumber) {
                case 1:
                    {
                        Header = CmisSync.Properties.Resources.ResourceManager.GetString("WhatsNext", CultureInfo.CurrentCulture);
                        Description = CmisSync.Properties.Resources.ResourceManager.GetString("CmisSyncCreates", CultureInfo.CurrentCulture);

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
