using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
#if __MonoCS__
//using Mono.Unix.Native;
#endif

namespace CmisSync.Lib
{
    /// <summary>
    /// Static methods that are useful in the context of synchronization.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Log.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Utils));


        /// <summary>
        /// Check whether the current user has write permission to the specified path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool HasWritePermissionOnDir(string path)
        {
            var writeAllow = false;
            var writeDeny = false;
            try
            {
                var accessControlList = Directory.GetAccessControl(path);
                if (accessControlList == null)
                    return false;
                var accessRules = accessControlList.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
                if (accessRules == null)
                    return false;

                foreach (System.Security.AccessControl.FileSystemAccessRule rule in accessRules)
                {
                    if ((System.Security.AccessControl.FileSystemRights.Write & rule.FileSystemRights)
                            != System.Security.AccessControl.FileSystemRights.Write)
                    {
                        continue;
                    }
                    if (rule.AccessControlType == System.Security.AccessControl.AccessControlType.Allow)
                    {
                        writeAllow = true;
                    }
                    else if (rule.AccessControlType == System.Security.AccessControl.AccessControlType.Deny)
                    {
                        writeDeny = true;
                    }
                }
            }
            catch (System.PlatformNotSupportedException)
            {
#if __MonoCS__
//                writeAllow = (0 == Syscall.access(path, AccessModes.W_OK));
#endif
            }

            return writeAllow && !writeDeny;
        }


        /// <summary>
        /// Names of files that must be excluded from synchronization.
        /// </summary>
        private static HashSet<String> ignoredFilenames = new HashSet<String>{
            "~", // gedit and emacs
            "thumbs.db", "desktop.ini", // Windows
            "cvs", ".svn", ".git", ".hg", ".bzr", // Version control local settings
            ".directory", // KDE
            ".ds_store", ".icon\r", ".spotlight-v100", ".trashes", // Mac OS X
            ".cvsignore", ".~cvsignore", ".bzrignore", ".gitignore", // Version control ignore list
            "$~",
            "lock" //Lock file for folder
        };

        /// <summary>
        /// A regular expression to detemine ignored filenames.
        /// </summary>
        private static Regex ignoredFilenamesRegex = new Regex(
            "(" + "^~" + // Microsoft Office temporary files start with ~
            "|" + "^\\._" + // Mac OS X files starting with ._
            "|" + "~$" + // gedit and emacs
            "|" + "^\\.~lock\\." +  // LibreOffice
            "|" + "^\\..*\\.sw[a-z]$" + // vi(m)
            "|" + "\\(autosaved\\).graffle$" + // Omnigraffle
            "|" + "\\(conflict copy \\d\\d\\d\\d-\\d\\d-\\d\\d\\)" + //CmisSync conflict
            ")"
        );

        /// <summary>
        /// Extensions of files that must be excluded from synchronization.
        /// </summary>
        private static HashSet<String> ignoredExtensions = new HashSet<String>{
            ".autosave", // Various autosaving apps
            ".~lock", // LibreOffice
            ".part", ".crdownload", // Firefox and Chromium temporary download files
            ".un~", ".swp", ".swo", // vi(m)
            ".tmp", // Microsoft Office
            ".sync", // CmisSync download
            ".cmissync", // CmisSync database
        };

        /// <summary>
        /// Check whether the filename is worth syncing or not.
        /// Files that are not worth syncing include temp files, locks, etc.
        /// </summary>
        private static bool IsFilenameWorthSyncing(string localDirectory, string filename)
        {
            if (null == filename)
            {
                return false;
            }

            filename = filename.ToLower();

            if (ignoredFilenames.Contains(filename) ||
                ignoredFilenamesRegex.IsMatch(filename))
            {
                Logger.DebugFormat("Skipping {0}: ignored file", filename);
                return false;
            }

            if (ignoredExtensions.Contains(Path.GetExtension(filename)))
            {
                Logger.DebugFormat("Skipping {0}: ignored file extension", filename);
                return false;
            }

            //Check filename length
            String fullPath = Path.Combine(localDirectory, filename);

            // reflection
            FieldInfo maxPathField = typeof(Path).GetField("MaxPath",
                BindingFlags.Static |
                BindingFlags.GetField |
                BindingFlags.NonPublic);

            if (fullPath.Length > (int)maxPathField.GetValue(null))
            {
                Logger.DebugFormat("Skipping {0}: path too long", fullPath);
                return false;
            }

            return true;
        }


        /// <summary>
        /// Check whether the directory is worth syncing or not.
        /// Directories that are not worth syncing include ignored, system, and hidden folders.
        /// </summary>
        private static bool IsDirectoryWorthSyncing(string localDirectory, RepoInfo repoInfo)
        {
            if (!localDirectory.StartsWith(repoInfo.TargetDirectory))
            {
                Logger.WarnFormat("Local directory is outside repo target directory.  local={0}, repo={1}", localDirectory, repoInfo.TargetDirectory);
                return false;
            }

            //Check for ignored path...
            string path = localDirectory.Substring(repoInfo.TargetDirectory.Length).Replace("\\", "/");
            if (repoInfo.isPathIgnored(path))
            {
                Logger.DebugFormat("Skipping {0}: hidden folder", localDirectory);
                return false;
            }

            //Check system/hidden
            DirectoryInfo directoryInfo = new DirectoryInfo(localDirectory);
            if (directoryInfo.Exists)
            {
                if (directoryInfo.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    Logger.DebugFormat("Skipping {0}: hidden folder", localDirectory);
                    return false;
                }
                if (directoryInfo.Attributes.HasFlag(FileAttributes.System))
                {
                    Logger.DebugFormat("Skipping {0}: system folder", localDirectory);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check whether the file is worth syncing or not.
        /// Files that are not worth syncing include blank, .
        /// </summary>
        private static bool IsFileWorthSyncing(string filepath, RepoInfo repoInfo)
        {
            if (File.Exists(filepath))
            {
                bool allowBlankFiles = false; //TODO: add a preference repoInfo.allowBlankFiles
                bool limitFilesize = true; //TODO: add preference for filesize limiting
                long filesizeLimit = 256 * 1024 * 1024; //TODO: add a preference for filesize limit?

                FileInfo fileInfo = new FileInfo(filepath);

                //Check permissions
                if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    Logger.DebugFormat("Skipping {0}: hidden file", filepath);
                    return false;
                }
                if (fileInfo.Attributes.HasFlag(FileAttributes.System))
                {
                    Logger.DebugFormat("Skipping {0}: system file", filepath);
                    return false;
                }

                //Check filesize
                if (!allowBlankFiles && fileInfo.Length <= 0)
                {
                    Logger.DebugFormat("Skipping {0}: blank file", filepath);
                    return false;
                }
                if (limitFilesize && fileInfo.Length > filesizeLimit)
                {
                    Logger.DebugFormat("Skipping {0}: file too large {1}mb", filepath, fileInfo.Length / (1024f * 1024f));
                    return false;
                }

            }
            else if (Directory.Exists(filepath))
            {
                return IsDirectoryWorthSyncing(filepath, repoInfo);
            }
            return true;
        }

        /// <summary>
        /// Check whether the file is worth syncing or not.
        /// Files that are not worth syncing include temp files, locks, etc.
        /// </summary>
        public static Boolean WorthSyncing(string localDirectory, string filename, RepoInfo repoInfo)
        {
            return IsFilenameWorthSyncing(localDirectory, filename) &&
                IsDirectoryWorthSyncing(localDirectory, repoInfo) &&
                IsFileWorthSyncing(Path.Combine(localDirectory, filename), repoInfo);
        }


        /// <summary>
        /// Check whether a file name is valid or not.
        /// </summary>
        public static bool IsInvalidFileName(string name)
        {
            bool ret = invalidFileNameRegex.IsMatch(name);
            if (ret)
            {
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
            if (ret)
            {
                Logger.Debug("Invalid dirname: " + name);
            }
            return ret;
        }


        /// <summary>
        /// Regular expression to check whether a filename is valid or not.
        /// </summary>
        private static Regex invalidFolderNameRegex = new Regex(
            "[" + Regex.Escape(new string(Path.GetInvalidPathChars())) + "]");

        /// <summary>
        /// Get the name of the conflicted file.
        /// </summary>
        public static string ConflictPath(String filePath)
        {
            String path = Path.GetDirectoryName(filePath);
            String filename = Path.GetFileNameWithoutExtension(filePath) + " (Conflict Copy " + DateTime.Today.ToString("yyyy-MM-dd") + ")";
            String ext = Path.GetExtension(filePath);
            return SuffixIfExists(path, filename, ext);
        }

        /// <summary>
        /// Find an available name (potentially suffixed) for this file.
        /// For instance:
        /// - if /dir/file does not exist, return the same path
        /// - if /dir/file exists, return /dir/file (1)
        /// - if /dir/file (1) also exists, return /dir/file (2)
        /// - etc
        /// </summary>
        private static string SuffixIfExists(String path, String filename, String extension)
        {
            string fullPath = Path.Combine(path, filename + extension);

            if (!File.Exists(fullPath))
            {
                return fullPath;
            }
            else
            {
                int index = 1;
                do
                {
                    fullPath = Path.Combine(path, filename + " (" + index.ToString() + ")" + extension);
                    if (!File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                    index++;
                }
                while (true);
            }
        }

        /// <summary>
        /// Format a file size nicely with small caps.
        /// Example: 1048576 becomes "1 ᴍʙ"
        /// </summary>
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
