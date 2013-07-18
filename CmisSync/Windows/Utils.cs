using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace CmisSync
{
    /// <summary>
    /// Useful Windows-specific methods.
    /// </summary>
    public static class Utils
    {
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
