using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

[assembly: ComVisible (false)]
namespace CmisSync.Lib
{
    public static class Utils
    {

        private static readonly ILog Logger = LogManager.GetLogger(typeof(Utils));

        /// <summary>
        /// <para>Creates a log-string from the Exception.</para>
        /// <para>The result includes the stacktrace, innerexception et cetera, separated by <seealso cref="Environment.NewLine"/>.</para>
        /// <para>Code from http://www.extensionmethod.net/csharp/exception/tologstring</para>
        /// </summary>
        /// <param name="ex">The exception to create the string from.</param>
        /// <param name="additionalMessage">Additional message to place at the top of the string, maybe be empty or null.</param>
        /// <returns></returns>
        public static string ToLogString(this Exception ex)
        {
            StringBuilder msg = new StringBuilder();
 
            if (ex != null)
            {
                try
                {
                    string newline = Environment.NewLine;

                    Exception orgEx = ex;
 
                    msg.Append("Exception:");
        
                    msg.Append(newline);
                    while (orgEx != null)
                    {
                        msg.Append(orgEx.Message);
                        msg.Append(newline);
                        orgEx = orgEx.InnerException;
                    }
 
                    if (ex.Data != null)
                    {
                        foreach (object i in ex.Data)
                        {
                            msg.Append("Data :");
                            msg.Append(i.ToString());
                            msg.Append(newline);
                        }
                    }
 
                    if (ex.StackTrace != null)
                    {
                        msg.Append("StackTrace:");
                        msg.Append(newline);
                        msg.Append(ex.StackTrace);
                        msg.Append(newline);
                    }
 
                    if (ex.Source != null)
                    {
                        msg.Append("Source:");
                        msg.Append(newline);
                        msg.Append(ex.Source);
                        msg.Append(newline);
                    }
 
                    if (ex.TargetSite != null)
                    {
                        msg.Append("TargetSite:");
                        msg.Append(newline);
                        msg.Append(ex.TargetSite.ToString());
                        msg.Append(newline);
                    }
 
                    Exception baseException = ex.GetBaseException();
                    if (baseException != null)
                    {
                        msg.Append("BaseException:");
                        msg.Append(newline);
                        msg.Append(ex.GetBaseException());
                    }
                }
                finally
                {
                }
            }
            return msg.ToString();
        }


        private static HashSet<String> ignoredFilenames = new HashSet<String>{
            "~", // gedit and emacs
            "thumbs.db", "desktop.ini", // Windows
            "cvs", ".svn", ".git", ".hg", ".bzr", // Version control local settings
            ".directory", // KDE
            ".ds_store", ".icon\r", ".spotlight-V100", ".trashes", // Mac OS X
            ".cvsignore", ".~cvsignore", ".bzrignore", ".gitignore", // Version control ignore list
            "$~"
        };

        private static HashSet<String> ignoredExtensions = new HashSet<String>{
            ".autosave", // Various autosaving apps
            ".~lock", // LibreOffice
            ".part", ".crdownload", // Firefox and Chromium temporary download files
            ".un~", ".swp", ".swo", // vi(m)
            ".tmp", // Microsoft Office
            ".sync", // CmisSync download
            ".cmissync" // CmisSync database
        };

         /**
         * Check whether the file is worth syncing or not.
         * Files that are not worth syncing include temp files, locks, etc.
         * */
        public static Boolean WorthSyncing(string filename)
        {
            if (null == filename)
            {
                return false;
            }

            // TODO: Consider these ones as well:
            //    "*~", // gedit and emacs
            //    ".~lock.*", // LibreOffice
            //    ".*.sw[a-z]", // vi(m)
            //    "*(Autosaved).graffle", // Omnigraffle

            filename = filename.ToLower();

            if (ignoredFilenames.Contains(filename)
                || ignoredExtensions.Contains(Path.GetExtension(filename))
                || filename[0] == '~' // Microsoft Office temporary files start with ~
                || filename[0] == '.' && filename[1] == '_') // Mac OS X files starting with ._
            {
                Logger.Debug("Unworth syncing: " + filename);
                return false;
            }

            //Logger.Info("SynchronizedFolder | Worth syncing:" + filename);
            return true;
        }


        /// <summary>
        /// Check whether a file name is valid or not.
        /// </summary>
        public static bool IsInvalidFileName(string name)
        {
            bool ret = invalidFileNameRegex.IsMatch(name);
            if (ret) {
                Logger.Debug("Invalid filename: " + name);
            }
            return ret;
        }


        /// <summary>
        /// Regular expression to check whether a file name is valid or not.
        /// </summary>
        private static Regex invalidFileNameRegex = new Regex(
            "[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())) + "]");


        /// <summary>
        /// Check whether a folder name is valid or not.
        /// </summary>
        public static bool IsInvalidFolderName(string name)
        {
            bool ret = invalidFolderNameRegex.IsMatch(name);
            if (ret) {
                Logger.Debug("Invalid dirname: " + name);
            }
            return ret;
        }


        /// <summary>
        /// Regular expression to check whether a filename is valid or not.
        /// </summary>
        private static Regex invalidFolderNameRegex = new Regex(
            "[" + Regex.Escape(new string(Path.GetInvalidPathChars())) + "]");


        /**
         * Find an available name (potentially suffixed) for this file.
         * For instance:
         * - if /dir/file does not exist, return the same path
         * - if /dir/file exists, return /dir/file (1)
         * - if /dir/file (1) also exists, return /dir/file (2)
         * - etc
         */
        public static string SuffixIfExists(String path)
        {
            if (!File.Exists(path))
            {
                return path;
            }
            else
            {
                int index = 1;
                do
                {
                    string ret = path + " (" + index.ToString() + ")";
                    if (!File.Exists(ret))
                    {
                        return ret;
                    }
                    index++;
                }
                while (true);
            }
        }

        // Format a file size nicely with small caps.
        // Example: 1048576 becomes "1 ᴍʙ"
        public static string FormatSize(double byteCount)
        {
            if (byteCount >= 1099511627776)
                return String.Format("{0:##.##} ᴛʙ", Math.Round(byteCount / 1099511627776, 1));
            else if (byteCount >= 1073741824)
                return String.Format("{0:##.##} ɢʙ", Math.Round(byteCount / 1073741824, 1));
            else if (byteCount >= 1048576)
                return String.Format("{0:##.##} ᴍʙ", Math.Round(byteCount / 1048576, 0));
            else if (byteCount >= 1024)
                return String.Format("{0:##.##} ᴋʙ", Math.Round(byteCount / 1024, 0));
            else
                return byteCount.ToString() + " bytes";
        }
    }
}
