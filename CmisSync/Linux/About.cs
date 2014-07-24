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

using Gtk;
using Mono.Unix;

namespace CmisSync {

    public class About : Window {

        public AboutController Controller = new AboutController ();

        private Label updates;


        public About () : base ("")
        {
            DeleteEvent += delegate (object o, DeleteEventArgs args) {
                Controller.WindowClosed ();
                args.RetVal = true;
            };

            DefaultSize    = new Gdk.Size (600, 260);
            Resizable      = false;
            BorderWidth    = 0;
            IconName       = "folder-cmissync";
            WindowPosition = WindowPosition.Center;
            Title          = Properties_Resources.About;
            AppPaintable   = true;

            string image_path = System.IO.Path.Combine(GUI.AssetsPath, "pixmaps", "about.png");

            Realize ();
            Gdk.Pixbuf buf = new Gdk.Pixbuf (image_path);
            Gdk.Pixmap map, map2;
            buf.RenderPixmapAndMask (out map, out map2, 255);
            GdkWindow.SetBackPixmap (map, false);

            CreateAbout ();

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

            Controller.NewVersionEvent += delegate (string new_version) {
                Application.Invoke (delegate {
                        this.updates.Markup = String.Format ("<span font_size='small' fgcolor='#729fcf'>{0}</span>",
                            string.Format (Properties_Resources.NewVersionAvailable, new_version));

                        this.updates.ShowAll ();
                        });
            };

            Controller.VersionUpToDateEvent += delegate {
                Application.Invoke (delegate {
                        this.updates.Markup = String.Format ("<span font_size='small' fgcolor='#729fcf'>{0}</span>",
                            Properties_Resources.RunningLatestVersion);

                        this.updates.ShowAll ();
                        });
            };

            Controller.CheckingForNewVersionEvent += delegate {
                Application.Invoke (delegate {
                        // this.updates.Markup = String.Format ("<span font_size='small' fgcolor='#729fcf'>{0}</span>",
                        //    "Checking for updates...");

                        this.updates.ShowAll ();
                        });
            };
        }


        private void CreateAbout ()
        {
            Gdk.Color fgcolor = new Gdk.Color();
            Gdk.Color.Parse("red", ref fgcolor);
            Label version = new Label () {
                Markup = string.Format ("<span font_size='small' fgcolor='#729fcf'>{0}</span>",
                        String.Format(Properties_Resources.Version, Controller.RunningVersion)),
                       Xalign = 0
            };

            this.updates = new Label () {
                Markup = "<span font_size='small' fgcolor='#729fcf'><b>Please check for updates at CmisSync.com</b></span>",
                       Xalign = 0
            };

            Label credits = new Label () {
                LineWrap     = true,
                             LineWrapMode = Pango.WrapMode.Word,
                             Markup = "<span font_size='small' fgcolor='#729fcf'>" +
                                 "Copyright © 2014–" + DateTime.Now.Year.ToString() + " GRAU DATA AG, Aegif and others.\n" +
                                 "\n" +
                                 "CmisSync is Open Source software. You are free to use, modify, " +
                                 "and redistribute it under the GNU General Public License version 3 or later." +
                                 "</span>",
                             WidthRequest = 330,
                             Wrap         = true,
                             Xalign = 0
            };

            LinkButton website_link = new LinkButton (Controller.WebsiteLinkAddress, Properties_Resources.Website);
            website_link.ModifyFg(StateType.Active, fgcolor);
            LinkButton credits_link = new LinkButton (Controller.CreditsLinkAddress, Properties_Resources.Credits);
            LinkButton report_problem_link = new LinkButton (Controller.ReportProblemLinkAddress, Properties_Resources.ReportProblem);

            HBox layout_links = new HBox (false, 0);
            layout_links.PackStart (website_link, false, false, 0);
            layout_links.PackStart (credits_link, false, false, 0);
            layout_links.PackStart (report_problem_link, false, false, 0);

            VBox layout_vertical = new VBox (false, 0);
            layout_vertical.PackStart (new Label (""), false, false, 42);
            layout_vertical.PackStart (version, false, false, 0);
            //layout_vertical.PackStart (this.updates, false, false, 0);
            layout_vertical.PackStart (credits, false, false, 9);
            layout_vertical.PackStart (new Label (""), false, false, 0);
            layout_vertical.PackStart (layout_links, false, false, 0);

            HBox layout_horizontal = new HBox (false, 0) {
                BorderWidth   = 0,
                              HeightRequest = 260,
                              WidthRequest  = 640
            };
            layout_horizontal.PackStart (new Label (""), false, false, 150);
            layout_horizontal.PackStart (layout_vertical, false, false, 0);

            Add (layout_horizontal);
        }
    }
}
