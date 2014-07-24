
using CmisSync.Lib.Cmis;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace CmisSync
{
	namespace CmisTree
	{
		/// <summary>
		/// Data Model Utilities for the WPF UI representing a CMIS repository
		/// </summary>
		public static class NodeModelUtils
		{
			/// <summary>
			/// Get the ignored folder list
			/// </summary>
			/// <returns></returns>
			public static List<string> GetIgnoredFolder(RootFolder repo)
			{
				List<string> result = new List<string>();
				foreach (Folder child in repo.Children)
				{
					result.AddRange(Folder.GetIgnoredFolder(child));
				}
				return result;
			}

			/// <summary>
			/// Get the selected folder list
			/// </summary>
			/// <returns></returns>
			public static List<string> GetSelectedFolder(RootFolder repo)
			{
				List<string> result = new List<string>();
				foreach (Folder child in repo.Children)
				{
					result.AddRange(Folder.GetSelectedFolder(child));
				}
				return result;
			}
		}
	}
}