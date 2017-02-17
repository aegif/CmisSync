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
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Net;
using System.Threading;
using System.IO;
using System.Linq;

using CmisSync.Lib;
using System.Collections.Generic;

namespace CmisSync {

	/// <summary>
	/// Controller for the SyncSize dialog.
	/// </summary>
	public class SyncSizeController {

		//===== Actions =====
		/// <summary>
		/// Show SyncSize Windows Action
		/// </summary>
		public event Action ShowWindowEvent = delegate { };

		/// <summary>
		/// Hide SyncSize Windows Action
		/// </summary>
		public event Action HideWindowEvent = delegate { };


		/// <summary>
		/// Constructor.
		/// </summary>
		public SyncSizeController()
		{
			Program.Controller.ShowSyncSizeWindowEvent += delegate
			{
				ShowWindowEvent();
			};

		}


		/// <summary>
		/// Closing the dialog.
		/// </summary>
		public void WindowClosed ()
		{
			HideWindowEvent ();
		}


		public static long GetDirectorySize(DirectoryInfo dInfo, bool includeSubDir)
		{
			// Enumerate all the files
			long totalSize = dInfo.EnumerateFiles()
				.Sum(file => file.Length);

			// If Subdirectories are to be included
			if (includeSubDir)
			{
				// Enumerate all sub-directories
				totalSize += dInfo.EnumerateDirectories()
					.Sum(dir => GetDirectorySize(dir, true));
			}
			return totalSize;
		}
	}
}
