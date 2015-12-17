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
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace CmisSync {

    /// <summary>
    /// Window for the two wizards:
    /// - CmisSync tutorial that appears at firt run,
    /// - wizard to add a new remote folder.
    /// </summary>
    public class SetupWindow : Window {

        /// <summary>
        /// Canvas that contains everything.
        /// </summary>
        public Canvas ContentCanvas  = new Canvas ();

        /// <summary>
        /// Header showing what the wizard step is about.
        /// </summary>
        public string Header;

        /// <summary>
        /// Header explaining what the wizard step is about, with a bit more details than the header.
        /// </summary>
        public string Description;

        /// <summary>
        /// Image that appears on the left of the window, showing the CmisSync logo.
        /// </summary>
        private Image side_splash;

        /// <summary>
        /// Line of buttons that appears at the bottom, usually Cancel, Next, etc.
        /// </summary>
        public List <Button> Buttons = new List <Button> ();

        /// <summary>
        /// Line separation the main content and the line of buttons.
        /// </summary>
        private Rectangle buttonsLine;

        /// <summary>
        /// Background for the bar line of buttons.
        /// </summary>
        private Rectangle buttonsBackground;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SetupWindow ()
        {
            // Window properties.
            Title = CmisSync.Properties_Resources.CmisSync;
            Width = 640;
            Height = 440;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush (Colors.WhiteSmoke);
            Icon = UIHelpers.GetImageSource("cmissync-app", "ico");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Content = ContentCanvas;
            Closing += Close;

            // Taskbar
			TaskbarItemInfo = new TaskbarItemInfo () {
				Description = "A-SaaS Drive"
			};

            // Separation and background for the line of buttons.
            this.buttonsLine = new Rectangle()
            {
                Width = Width,
                Height = 1,
                Fill = new SolidColorBrush(Color.FromRgb(223, 223, 223))
            };
            this.buttonsBackground = new Rectangle()
            {
                Width  = Width,
                Height = 40,
                Fill   = new SolidColorBrush (Color.FromRgb (240, 240, 240))
            };

            // Splash image.
            this.side_splash = new Image () {
                Width  = 150,
                Height = 482
            };
            this.side_splash.Source = UIHelpers.GetImageSource ("side-splash");

            // Components position.

            ContentCanvas.Children.Add (this.buttonsBackground);
            Canvas.SetRight (buttonsBackground, 0);
            Canvas.SetBottom (buttonsBackground, 0);

            ContentCanvas.Children.Add (this.buttonsLine);
            Canvas.SetRight (this.buttonsLine, 0);
            Canvas.SetBottom (this.buttonsLine, 40);

            ContentCanvas.Children.Add (this.side_splash);
            Canvas.SetLeft (this.side_splash, 0);
            Canvas.SetBottom (this.side_splash, 0);
        }


        /// <summary>
        /// Reset the window for use in another step/wizard.
        /// </summary>
        public void Reset ()
        {
            // Remove elements.
            ContentCanvas.Children.Remove (this.buttonsBackground);
            ContentCanvas.Children.Remove (this.buttonsLine);
            ContentCanvas.Children.Remove (this.side_splash);

            // Re-insert components on a new canvas.
            ContentCanvas = new Canvas ();
            Content       = ContentCanvas;
            ContentCanvas.Children.Add (this.buttonsBackground);
            ContentCanvas.Children.Add (this.buttonsLine);
            ContentCanvas.Children.Add (this.side_splash);

            // Reset buttons and labels.
            Buttons       = new List <Button> ();
            Header        = "";
            Description   = "";
        }


        /// <summary>
        /// Show the window's components.
        /// </summary>
        public void ShowAll ()
        {
            // Create header and description.
            Label header_label = new Label () {
                Content    = Header,
                Foreground = new SolidColorBrush (Color.FromRgb (0, 51, 153)),
                FontSize   = 16
            };
            TextBlock description_label = new TextBlock () {
                Text         = Description,
                TextWrapping = TextWrapping.Wrap,
                Width        = 375
            };

            // Labels position.

            ContentCanvas.Children.Add (header_label);
            Canvas.SetLeft (header_label, 180);
            Canvas.SetTop (header_label, 18);

            ContentCanvas.Children.Add (description_label);
            Canvas.SetLeft (description_label, 185);
            Canvas.SetTop (description_label, 60);


            // If there are buttons, position them.
            if (Buttons.Count > 0) {
                Buttons [0].IsDefault = true;
				Buttons.Reverse ();

                int right = 9;

                foreach (Button button in Buttons) {
                    button.Measure (new Size (Double.PositiveInfinity, Double.PositiveInfinity));
                    Rect rect = new Rect (button.DesiredSize);

                    button.Width = rect.Width + 26;

                    if (button.Width < 75)
                        button.Width = 75;

                    ContentCanvas.Children.Add (button);
                    Canvas.SetRight (button, right);
                    Canvas.SetBottom (button, 9);

                    right += (int) button.Width + 9;
                }
            }

            // Enables the window to receive keyboard messages when it is opened modelessly from Windows Forms.
            ElementHost.EnableModelessKeyboardInterop (this);
        }


        /// <summary>
        /// Close the window.
        /// </summary>
        protected virtual void Close (object sender, CancelEventArgs args)
        {
            args.Cancel = true;
        }
    }
}
