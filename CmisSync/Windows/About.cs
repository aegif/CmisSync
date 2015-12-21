//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons (hylkebons@gmail.com)
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
//   along with this program. If not, see (http://www.gnu.org/licenses/).


using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xaml;
using System.Globalization;

namespace CmisSync {

    /// <summary>
    /// About dialog.
    /// It shows information such as the CmisSync name and logo, the version, some copyright information.
    /// </summary>
    public class About : Window {

        /// <summary>
        /// Controller.
        /// </summary>
        public AboutController Controller = new AboutController ();

        /// <summary>
        /// Shows a message about software updates.
        /// </summary>
        private Label updates;

        /// <summary>
        /// Constructor.
        /// </summary>
        public About ()
        {
            Title      = Properties_Resources.ResourceManager.GetString("About", CultureInfo.CurrentCulture);
            ResizeMode = ResizeMode.NoResize;
            Height     = 288;
            Width      = 640;
            Icon = UIHelpers.GetImageSource("cmissync-app", "ico");

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Closing += Close;

            CreateAbout ();

            Controller.ShowWindowEvent += delegate {
               Dispatcher.BeginInvoke ((Action) delegate {
                    Show ();
                    Activate ();
                    BringIntoView ();
                });
            };

            Controller.HideWindowEvent += delegate {
                Dispatcher.BeginInvoke((Action)delegate {
                    Hide ();
                });
            };

            Controller.NewVersionEvent += delegate (string new_version) {
                Dispatcher.BeginInvoke((Action)delegate {
                    this.updates.Content = String.Format(Properties_Resources.NewVersionAvailable, new_version);
                    this.updates.UpdateLayout ();
                });
            };

            Controller.VersionUpToDateEvent += delegate {
                Dispatcher.BeginInvoke ((Action) delegate {
                    this.updates.Content = Properties_Resources.RunningLatestVersion;
                    this.updates.UpdateLayout ();
                });
            };

            Controller.CheckingForNewVersionEvent += delegate {
                Dispatcher.BeginInvoke ((Action) delegate {
                    //this.updates.Content = "Checking for updates...";
                    this.updates.UpdateLayout ();
                });
            };
        }

        /// <summary>
        /// Create the GUI.
        /// </summary>
        private void CreateAbout ()
        {
            Image image = new Image () {
                Width  = 640,
                Height = 260
            };

            image.Source = UIHelpers.GetImageSource ("about");


            Label version = new Label () {
                Content = String.Format(Properties_Resources.Version, Controller.RunningVersion),
                FontSize   = 11,
                Foreground = new SolidColorBrush (Color.FromRgb (135, 178, 227))
            };

            this.updates = new Label () {
                Content    = Properties_Resources.ResourceManager.GetString(
                    "PleaseCheckForUpdates", CultureInfo.CurrentCulture), // Previously: "Checking for updates...",
                FontSize   = 11,
                Foreground = new SolidColorBrush (Color.FromRgb (135, 178, 227))
            };

            TextBlock cmisysncInfo = new TextBlock() {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(135, 178, 227)),
                Text = Properties_Resources.ResourceManager.GetString( "PleaseCheckForUpdates", CultureInfo.CurrentCulture),
                TextWrapping = TextWrapping.Wrap,
                Width = 318
            };

            TextBlock credits = new TextBlock () {
                FontSize     = 11,
                Foreground = new SolidColorBrush (Color.FromRgb (135, 178, 227)),
                Text         = "Copyright © 2010–" + DateTime.Now.Year.ToString() + " Aegif and others.\n"
                   /*  + "\n"  + Properties_Resources.ResourceManager.GetString("OpenSource", CultureInfo.CurrentCulture) */,
                TextWrapping = TextWrapping.Wrap,
                Width        = 318
            };

            /*
            Link website_link = new Link(Properties_Resources.ResourceManager.GetString(
                "Website", CultureInfo.CurrentCulture), Controller.WebsiteLinkAddress);
            Link credits_link = new Link(Properties_Resources.ResourceManager.GetString(
                "Credits", CultureInfo.CurrentCulture), Controller.CreditsLinkAddress);
            Link report_problem_link = new Link(Properties_Resources.ResourceManager.GetString(
                "ReportProblem", CultureInfo.CurrentCulture), Controller.ReportProblemLinkAddress);
            */

            Canvas canvas = new Canvas ();

            canvas.Children.Add (image);
            Canvas.SetLeft (image, 0);
            Canvas.SetTop (image, 0);

            canvas.Children.Add (version);
            Canvas.SetLeft (version, 289);
            Canvas.SetTop (version, 92);

            /*
            canvas.Children.Add (this.updates);
            Canvas.SetLeft (this.updates, 289);
            Canvas.SetTop (this.updates, 109);
            */

            canvas.Children.Add(cmisysncInfo);
            Canvas.SetLeft(cmisysncInfo, 294);
            Canvas.SetTop(cmisysncInfo, 34);


            canvas.Children.Add (credits);
            Canvas.SetLeft (credits, 294);
            Canvas.SetTop (credits, 142);

            /*
            canvas.Children.Add (website_link);
            Canvas.SetLeft (website_link, 289);
            Canvas.SetTop (website_link, 222);

            canvas.Children.Add (credits_link);
            Canvas.SetLeft (credits_link, 289 + website_link.ActualWidth + 60);
            Canvas.SetTop (credits_link, 222);

            canvas.Children.Add (report_problem_link);
            Canvas.SetLeft (report_problem_link, 289 + website_link.ActualWidth + credits_link.ActualWidth + 115);
            Canvas.SetTop (report_problem_link, 222);
            */

            Content = canvas;
        }

        /// <summary>
        /// Close the dialog.
        /// </summary>
        private void Close (object sender, CancelEventArgs args)
        {
            Controller.WindowClosed ();
            args.Cancel = true;
        }
    }


    /// <summary>
    /// Hyperlink label that opens an URL in the default browser.
    /// </summary>
    public class Link : Label {

        /// <summary>
        /// Constructor.
        /// </summary>
        public Link (string title, string address)
        {
            FontSize   = 11;
            Cursor     = Cursors.Hand;
            Foreground = new SolidColorBrush (Color.FromRgb (135, 178, 227));

            TextDecoration underline = new TextDecoration () {
                Pen              = new Pen (new SolidColorBrush (Color.FromRgb (135, 178, 227)), 1),
                PenThicknessUnit = TextDecorationUnit.FontRecommended
            };

            TextDecorationCollection collection = new TextDecorationCollection ();
            collection.Add (underline);

            TextBlock text_block = new TextBlock () {
                Text            = title,
                TextDecorations = collection
            };

            Content = text_block;

            MouseUp += delegate {
                Process.Start (new ProcessStartInfo (address));
            };
        }
    }
}
