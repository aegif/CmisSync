using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace CmisSync
{
    public static class Utils
    {
        // Format a file size nicely with small caps.
        // Example: 1048576 becomes "1 ᴍʙ"
        public static string FormatSize(double byte_count)
        {
            if (byte_count >= 1099511627776)
                return String.Format("{0:##.##} ᴛʙ", Math.Round(byte_count / 1099511627776, 1));
            else if (byte_count >= 1073741824)
                return String.Format("{0:##.##} ɢʙ", Math.Round(byte_count / 1073741824, 1));
            else if (byte_count >= 1048576)
                return String.Format("{0:##.##} ᴍʙ", Math.Round(byte_count / 1048576, 0));
            else if (byte_count >= 1024)
                return String.Format("{0:##.##} ᴋʙ", Math.Round(byte_count / 1024, 0));
            else
                return byte_count.ToString() + " bytes";
        }


        /// <summary>
        /// Open a folder in Windows Explorer.
        /// </summary>
        /// <param name="path">Path to open</param>
        public static void OpenFolder(string path)
        {
            Process process = new Process();
            process.StartInfo.FileName = "explorer";
            process.StartInfo.Arguments = path;

            process.Start();
        }



    }
}
