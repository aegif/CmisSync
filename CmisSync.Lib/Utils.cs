using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using System.IO;

using System.Security;
using System.Security.Permissions;

using System.Text.RegularExpressions;
using System.Reflection;
#if __MonoCS__ && !__COCOA__
using Mono.Unix.Native;
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
        /// Component which will receive notifications intended for the end-user.
        /// A GUI program might raise a pop-up, a CLI program might print a line.
        /// </summary>
        private static UserNotificationListener userNotificationListener;


        /// <summary>
        /// Register the component which will receive notifications intended for the end-user.
        /// </summary>
        public static void SetUserNotificationListener(UserNotificationListener listener)
        {
            userNotificationListener = listener;
        }


        /// <summary>
        /// Send a message to the end user.
        /// </summary>
        public static void NotifyUser(string message)
        {
            userNotificationListener.NotifyUser(message);
        }


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
#if __MonoCS__ && !__COCOA__
                writeAllow = (0 == Syscall.access(path, AccessModes.W_OK));
				//writeAllow = true;
#endif
                #if __COCOA__
                // TODO check directory permissions
                writeAllow = true;
                #endif
            }
            catch(System.UnauthorizedAccessException) {
                var permission = new FileIOPermission(FileIOPermissionAccess.Write, path);
                var permissionSet = new PermissionSet(PermissionState.None);
                permissionSet.AddPermission(permission);
                if (permissionSet.IsSubsetOf(AppDomain.CurrentDomain.PermissionSet))
                {
                    return true;
                }
                else
                    return false;
            }

            return writeAllow && !writeDeny;
        }

        /// Names of files that must be excluded from synchronization.
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
            "|" + "-conflict-version" + // CmisSync conflict
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

            #if __COCOA__ || __MonoCS__
            // TODO Check filename length for OS X
            // * Max "FileName" length: 255 charactors.
            // * FileName encoding is UTF-16 (Modified NFD).

            #else
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
            #endif

            return true;

        }


        /// <summary>
        /// Check whether the directory is worth syncing or not.
        /// Directories that are not worth syncing include ignored, system, and hidden folders.
        /// </summary>
        public static bool IsDirectoryWorthSyncing(string localDirectory, RepoInfo repoInfo)
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
        /// This optionally excludes blank files or files too large.
        /// </summary>
        public static bool IsFileWorthSyncing(string filepath, RepoInfo repoInfo)
        {
            if (File.Exists(filepath))
            {
                bool allowBlankFiles = true; //TODO: add a preference repoInfo.allowBlankFiles
                bool limitFilesize = false; //TODO: add preference for filesize limiting
                long filesizeLimit = 256 * 1024 * 1024; //TODO: add a preference for filesize limit

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
                    Logger.DebugFormat("Skipping {0}: file too large {1}MB", filepath, fileInfo.Length / (1024f * 1024f));
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
        /// Determines whether this instance is valid ISO-8859-1 specified input.
        /// </summary>
        /// <param name="input">If set to <c>true</c> input.</param>
        /// <returns><c>true</c> if this instance is valid ISO-8859-1 specified input; otherwise, <c>false</c>.</returns>
        public static bool IsValidISO88591(string input)
        {
            byte[] bytes = Encoding.GetEncoding(28591).GetBytes(input);
            String result = Encoding.GetEncoding(28591).GetString(bytes);
            return String.Equals(input, result);
        }

        /// <summary>
        /// Check whether the file is worth syncing or not.
        /// Files that are not worth syncing include temp files, locks, etc.
        /// </summary>
        public static Boolean WorthSyncing(string filename)
        {
            if (null == filename)
            {
                return false;
            }

            // TODO: Consider these ones as well:
            // "*~", // gedit and emacs
            // ".~lock.*", // LibreOffice
            // ".*.sw[a-z]", // vi(m)
            // "*(Autosaved).graffle", // Omnigraffle

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
            if (ret)
            {
                Logger.Debug(String.Format("The given file name {0} contains invalid patterns", name));
                return ret;
            }

            return ret;
        }

        /// <summary>
        /// Regular expression to check whether a file name is valid or not.
        /// In particular, CmisSync forbids characters that would not be allowed on Windows:
        /// https://msdn.microsoft.com/en-us/library/windows/desktop/aa365247%28v=vs.85%29.aspx#file_and_directory_names
        /// </summary>
        private static Regex invalidFileNameRegex = new Regex(
            "[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())+"\"?:/\\|<>*") + "]");

        /// <summary>
        /// Check whether a folder name is valid or not.
        /// </summary>
        public static bool IsInvalidFolderName(string name)
        {
            bool ret = invalidFolderNameRegex.IsMatch(name);
            if (ret)
            {
                Logger.Debug(String.Format("The given directory name {0} contains invalid patterns", name));
                return ret;
            }

            return ret;
        }

        /// <summary>
        /// Regular expression to check whether a filename is valid or not.
        /// </summary>
        private static Regex invalidFolderNameRegex = new Regex(
            "[" + Regex.Escape(new string(Path.GetInvalidPathChars())+"\"?:/\\|<>*") + "]");

        /// <summary>
        /// Find an available conflict free filename for this file.
        /// For instance:
        /// - if /dir/file does not exist, return the same path
        /// - if /dir/file exists, return /dir/file (1)
        /// - if /dir/file (1) also exists, return /dir/file (2)
        /// - etc
        /// </summary>
        /// <param name="path">Path of the file in conflict</param>
        /// <param name="user">Local user</param>
        /// <returns></returns>
        public static string CreateConflictFilename(String path, String user)
        {
            if (!File.Exists(path))
            {
                return path;
            }
            else
            {
                string extension = Path.GetExtension(path);
                string filepath = path.Substring(0, path.Length - extension.Length);
                string ret = String.Format("{0}_{1}-conflict-version{2}", filepath, user, extension);
                if (!File.Exists(ret))
                    return ret;
                int index = 1;
                do
                {
                    ret = String.Format("{0}_{1}-conflict-version ({2}){3}", filepath, user, index.ToString(), extension);
                    if (!File.Exists(ret))
                    {
                        return ret;
                    }
                    index++;
                }
                while (true);
            }
        }

        /// <summary>
        /// Format a file size nicely.
        /// Example: 1048576 becomes "1 MB"
        /// </summary>
        public static string FormatSize(double byteCount)
        {
            if (byteCount >= 1099511627776)
                return String.Format("{0:##.##} TB", Math.Round(byteCount / 1099511627776, 1));
            else if (byteCount >= 1073741824)
                return String.Format("{0:##.##} GB", Math.Round(byteCount / 1073741824, 1));
            else if (byteCount >= 1048576)
                return String.Format("{0:##.##} MB", Math.Round(byteCount / 1048576, 0));
            else if (byteCount >= 1024)
                return String.Format("{0:##.##} KB", Math.Round(byteCount / 1024, 0));
            else
                return byteCount.ToString() + " bytes";
        }

        /// <summary>
        /// Formats the bandwidth in typical 10 based calculation
        /// </summary>
        /// <returns>
        /// The bandwidth.
        /// </returns>
        /// <param name='bitsPerSecond'>
        /// Bits per second.
        /// </param>
        public static string FormatBandwidth(double bitsPerSecond)
        {
            if (bitsPerSecond >= (1000d*1000d*1000d*1000d))
                return String.Format("{0:##.##} TBit/s", Math.Round(bitsPerSecond / (1000d*1000d*1000d*1000d), 1));
            else if (bitsPerSecond >= (1000d*1000d*1000d))
                return String.Format("{0:##.##} GBit/s", Math.Round(bitsPerSecond / (1000d*1000d*1000d), 1));
            else if (bitsPerSecond >= (1000d*1000d))
                return String.Format("{0:##.##} MBit/s", Math.Round(bitsPerSecond / (1000d*1000d), 1));
            else if (bitsPerSecond >= 1000d)
                return String.Format("{0:##.##} KBit/s", Math.Round(bitsPerSecond / 1000d, 1));
            else
                return bitsPerSecond.ToString() + " Bit/s";
        }

        /// <summary>
        /// Format a file size nicely.
        /// Example: 1048576 becomes "1 MB"
        /// </summary>
        public static string FormatSize(long byteCount)
        {
            return FormatSize((double) byteCount);
        }

        /// <summary>
        /// Formats the bandwidth in typical 10 based calculation
        /// </summary>
        /// <returns>
        /// The bandwidth.
        /// </returns>
        /// <param name='bitsPerSecond'>
        /// Bits per second.
        /// </param>
        public static string FormatBandwidth(long bitsPerSecond)
        {
            return FormatBandwidth((double) bitsPerSecond);
        }

        /// <summary>
        /// Whether a file or directory is a symbolic link.
        /// </summary>
        public static bool IsSymlink(string path)
        {
            FileInfo fileinfo = new FileInfo(path);
            if(fileinfo.Exists)
                return IsSymlink(fileinfo);
            DirectoryInfo dirinfo = new DirectoryInfo(path);
            if(dirinfo.Exists)
                return IsSymlink(dirinfo);
            return false;
        }

        /// <summary>
        /// Determines whether this instance is a symlink the specified FileSystemInfo.
        /// </summary>
        /// <returns>
        /// <c>true</c> if this instance is a symlink the specified fsi; otherwise, <c>false</c>.
        /// </returns>
        /// <param name='fsi'>
        /// If set to <c>true</c> fsi.
        /// </param>
        public static bool IsSymlink(FileSystemInfo fsi)
        {
            return ((fsi.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint);
        }


        public static void MoveFolderLocally(string origin, string destination)
        {
            //string destinationBase = Path.GetDirectoryName(destination);

            Directory.Move(origin, Path.Combine(destination)); // TODO might be more complex when a folder is moved to a non-yet-existing folder hierarchy
        }
    }
}
