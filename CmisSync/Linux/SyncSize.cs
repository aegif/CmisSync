//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2015  Momar DIENE <diene.momar@gmail.com>
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
using CmisSync.Lib;
using System.IO;
using System.Collections.Generic;

namespace CmisSync {

	public class SyncSize : Window {

		public SyncSizeController Controller = new SyncSizeController ();

		private Label reponame;


		public SyncSize () : base ("")
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
			Title          = "Syncing Size";
			AppPaintable   = true;

			string image_path = System.IO.Path.Combine(GUI.AssetsPath, "pixmaps", "about.png");

			Realize ();
			Gdk.Pixbuf buf = new Gdk.Pixbuf (image_path);
			Gdk.Pixmap map, map2;
			buf.RenderPixmapAndMask (out map, out map2, 255);
			GdkWindow.SetBackPixmap (map, false);


			CreateSyncSize ();

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

		}


		private void CreateSyncSize ()
		{

			VBox layout_vertical = new VBox (false, 0);
			double totalsize = 0;

			foreach (Config.SyncConfig.Folder f in ConfigManager.CurrentConfig.Folders) {
				//Lrepobase.Add(new CmisSync.Lib.Sync.CmisRepo(f.GetRepoInfo (),new ActivityListenerAggregator(Program.Controller)));

				double size = 0;
				size = SyncSizeController.GetDirectorySize (new DirectoryInfo (f.LocalPath), true);
				totalsize += size;
				reponame = new Label () {
					Markup = string.Format("{0,-10}",f.DisplayName.ToString())+string.Format("{0,10}",CmisSync.Lib.Utils.FormatSizeExact(size).ToString()),
					Xalign = 0.5f
				};

				layout_vertical.PackStart (new Label (""), false, false, 0);
				layout_vertical.PackStart (reponame, false, false, 0);

			}


			layout_vertical.PackStart (new Label ("========================="), false, false, 20);
			layout_vertical.PackStart (new Label ("Total                     "+CmisSync.Lib.Utils.FormatSizeExact(totalsize)), false, false, 0);


			HBox layout_horizontal = new HBox (false, 0) {
				BorderWidth   = 0,
				HeightRequest = 260,
				WidthRequest  = 640
			};
			layout_horizontal.PackStart (new Label (""), false, false, 140);
			layout_horizontal.PackStart (createScrolledWindow(layout_vertical), true, true, 0);

			Add (layout_horizontal);
		}

		private static Widget createScrolledWindow(Widget child)
		{
			ScrolledWindow scrolledWindow = new ScrolledWindow();
			scrolledWindow.SetPolicy(PolicyType.Never, PolicyType.Automatic);

			scrolledWindow.AddWithViewport(child);
			scrolledWindow.ShadowType=ShadowType.None;

			return scrolledWindow;
		}
	}
}
