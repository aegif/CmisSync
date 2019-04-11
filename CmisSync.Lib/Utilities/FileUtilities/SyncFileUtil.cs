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
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Config;
using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.CmisSyncFolder;

using DotCMIS.Client;

#if __MonoCS__ && !__COCOA__
using Mono.Unix.Native;
#endif

namespace CmisSync.Lib.Utilities.FileUtilities
{
    public static class SyncFileUtil
    {
        
        private static readonly ILog Logger = LogManager.GetLogger (typeof (SyncFileUtil));

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
        private static Regex ignoredFilenamesRegex = new Regex (
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
            "autosave", // Various autosaving apps
            "~lock", // LibreOffice
            "part", "crdownload", // Firefox and Chromium temporary download files
            "un~", "swp", "swo", // vi(m)
            "tmp", // Microsoft Office
            "sync", // CmisSync download
            "cmissync", // CmisSync database
        };


        public static bool IsPathIgnored (string path, CmisSyncFolder cmisSyncFolder)
        {
            if (IsInvalidFolderName (path.Replace ("/", "").Replace ("\"", "")))
                return true;
            return !String.IsNullOrEmpty (cmisSyncFolder.IgnoredPaths.Find (delegate (string ignore) {
                if (String.IsNullOrEmpty (ignore)) {
                    return false;
                }
                return path.StartsWith (ignore);
            }));
        }


        /// <summary>
        /// Check whether the filename is worth syncing or not.
        /// Files that are not worth syncing include temp files, locks, etc.
        /// </summary>
        private static bool IsFilenameWorthSyncing (string filePath)
        {
            if (null == filePath) {
                return false;
            }

            String filename = filePath.Split (Path.DirectorySeparatorChar).Last ().ToLower();

            if (ignoredFilenames.Contains (filename) ||
                ignoredFilenamesRegex.IsMatch (filename)) {
                Logger.DebugFormat ("Skipping {0}: ignored file", filename);
                return false;
            }

            // Check filename extension if there is one.
            if (filename.Contains ('.')) {
                string extension = filename.Split ('.').Last ();

                if (ignoredExtensions.Contains (extension)) {
                    Logger.DebugFormat ("Skipping {0}: ignored file extension", filename);
                    return false;
                }
            }

            // Check resulting file path length
            string fullPath = filePath; //Path.Combine (localDirectory, filename);

#if __COCOA__ || __MonoCS__
            // TODO Check filename length for OS X
            // * Max "FileName" length: 255 charactors.
            // * FileName encoding is UTF-16 (Modified NFD).

#else
            // Get Path.MaxPath
            // It is not a public field so reflection is necessary.

            FieldInfo maxPathField = typeof (Path).GetField ("MaxPath",
                BindingFlags.Static |
                BindingFlags.GetField |
                BindingFlags.NonPublic);

            if (fullPath.Length > (int)maxPathField.GetValue (null)) {
                Logger.WarnFormat ("Skipping {0}: path too long", fullPath);
                return false;
            }
#endif

            return true;
        }

        /// <summary>
        /// Check whether the file is worth syncing or not.
        /// This optionally excludes blank files or files too large.
        /// </summary>
        public static bool IsFileWorthSyncing (string filepath, CmisSyncFolder cmisSyncFolder)
        {
            if (!IsFilenameWorthSyncing (filepath)) return false;

            bool allowBlankFiles = true; //TODO: add a preference repoInfo.allowBlankFiles
            bool limitFilesize = false; //TODO: add preference for filesize limiting
            long filesizeLimit = 256 * 1024 * 1024; //TODO: add a preference for filesize limit
            
            FileInfo fileInfo = new FileInfo (filepath);

            // Exclude files that been deleted.
            if (fileInfo.Exists) {
                //Check permissions
                if (fileInfo.Attributes.HasFlag (FileAttributes.Hidden)) {
                    Logger.InfoFormat ("Skipping {0}: hidden file", filepath);
                    return false;
                }
                if (fileInfo.Attributes.HasFlag (FileAttributes.System)) {
                    Logger.InfoFormat ("Skipping {0}: system file", filepath);
                    return false;
                }

                //Check filesize
                if (!allowBlankFiles && fileInfo.Length <= 0) {
                    Logger.InfoFormat ("Skipping {0}: blank file", filepath);
                    return false;
                }
                if (limitFilesize && fileInfo.Length > filesizeLimit) {
                    Logger.InfoFormat ("Skipping {0}: file too large {1}MB", filepath, fileInfo.Length / (1024f * 1024f));
                    return false;
                }

                //Ignore Symbol Link
                if (Utils.IsSymlink (fileInfo)) {
                    Logger.InfoFormat ("Skipping {0}: symbolic link", filepath);
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// In refactor demo, fodler might be -conflict-version
        /// </summary>
        /// <returns><c>true</c>, if directory name worth syncing was ised, <c>false</c> otherwise.</returns>
        /// <param name="localDirectory">Local directory.</param>
        private static bool IsDirectoryNameWorthSyncing ( string localDirectory ) {
            return !localDirectory.Contains ("-conflict-version");
        }

        /// <summary>
        /// Check whether the directory is worth syncing or not.
        /// Directories that are not worth syncing include ignored, system, and hidden folders.
        /// </summary>
        public static bool IsDirectoryWorthSyncing (string localDirectory, CmisSyncFolder cmisSyncFolder)
        {
            if (!IsDirectoryNameWorthSyncing (localDirectory)) return false;

            if (!localDirectory.StartsWith (cmisSyncFolder.LocalPath)) {
                Logger.WarnFormat ("Local directory is outside repo target directory.  local={0}, repo={1}", localDirectory, cmisSyncFolder.LocalPath);
                return false;
            }

            //Check for ignored path...
            string path = localDirectory.Substring (cmisSyncFolder.LocalPath.Length).Replace ("\\", "/");
                if (IsPathIgnored (path, cmisSyncFolder)) {
                Logger.DebugFormat ("Skipping {0}: hidden folder", localDirectory);
                return false;

            }

            //Check system/hidden
            DirectoryInfo directoryInfo = new DirectoryInfo (localDirectory);
            if (directoryInfo.Exists) {
                if (directoryInfo.Attributes.HasFlag (FileAttributes.Hidden)) {
                    Logger.DebugFormat ("Skipping {0}: hidden folder", localDirectory);
                    return false;
                }
                if (directoryInfo.Attributes.HasFlag (FileAttributes.System)) {
                    Logger.DebugFormat ("Skipping {0}: system folder", localDirectory);
                    return false;
                }

            }

            return true;
        }

        public static bool WorthSyncing (string filepath, CmisSyncFolder cmisSyncFolder)
        {
            if (File.Exists (filepath)) {
                return IsFileWorthSyncing (filepath, cmisSyncFolder);
            } else if (Directory.Exists (filepath)) {
                return IsDirectoryWorthSyncing (filepath, cmisSyncFolder);
            }
            return false;
        }

        public static bool WorthSyncing (string fullPath, bool isFolder, CmisSyncFolder cmisSyncFolder)
        {
            if (!isFolder) return IsFileWorthSyncing (fullPath, cmisSyncFolder);
            else return IsDirectoryWorthSyncing (fullPath, cmisSyncFolder);
        }

        /// <summary>
        /// Determines whether this instance is valid ISO-8859-1 specified input.
        /// </summary>
        /// <param name="input">If set to <c>true</c> input.</param>
        /// <returns><c>true</c> if this instance is valid ISO-8859-1 specified input; otherwise, <c>false</c>.</returns>
        public static bool IsValidISO88591 (string input)
        {
            byte [] bytes = Encoding.GetEncoding (28591).GetBytes (input);
            String result = Encoding.GetEncoding (28591).GetString (bytes);
            return String.Equals (input, result);
        }


        /// <summary>
        /// Check whether the file is worth syncing or not.
        /// Files that are not worth syncing include temp files, locks, etc.
        /// </summary>
        public static Boolean WorthSyncing (string filename)
        {
            if (null == filename) {
                return false;
            }

            // TODO: Consider these ones as well:
            // "*~", // gedit and emacs
            // ".~lock.*", // LibreOffice
            // ".*.sw[a-z]", // vi(m)
            // "*(Autosaved).graffle", // Omnigraffle

            filename = filename.ToLower ();

            if (ignoredFilenames.Contains (filename)
                || ignoredExtensions.Contains (Path.GetExtension (filename))
                || filename [0] == '~' // Microsoft Office temporary files start with ~
                || filename [0] == '.' && filename [1] == '_') // Mac OS X files starting with ._
            {
                Logger.Debug ("Unworth syncing: " + filename);
                return false;
            }

            //Logger.Info("SynchronizedFolder | Worth syncing:" + filename);
            return true;
        }


        /// <summary>
        /// Check whether a file name is valid or not.
        /// </summary>
        public static bool IsInvalidFileName (string name)
        {
            bool ret = invalidFileNameRegex.IsMatch (name);
            if (ret) {
                Logger.Debug (String.Format ("The given file name {0} contains invalid patterns", name));
                return ret;
            }

            return ret;
        }


        /// <summary>
        /// Regular expression to check whether a file name is valid or not.
        /// In particular, CmisSync forbids characters that would not be allowed on Windows:
        /// https://msdn.microsoft.com/en-us/library/windows/desktop/aa365247%28v=vs.85%29.aspx#file_and_directory_names
        /// </summary>
        private static Regex invalidFileNameRegex = new Regex (
            "[" + Regex.Escape (new string (Path.GetInvalidFileNameChars ()) + "\"?:/\\|<>*") + "]");


        /// <summary>
        /// Check whether a folder name is valid or not.
        /// </summary>
        public static bool IsInvalidFolderName (string name)
        {
            bool ret = invalidFolderNameRegex.IsMatch (name);
            if (ret) {
                Logger.Debug (String.Format ("The given directory name {0} contains invalid patterns", name));
                return ret;
            }

            return ret;
        }


        /// <summary>
        /// Regular expression to check whether a filename is valid or not.
        /// </summary>
        private static Regex invalidFolderNameRegex = new Regex (
            "[" + Regex.Escape (new string (Path.GetInvalidPathChars ()) + "\"?:/\\|<>*") + "]");



        /// <summary>
        /// Gets the applicable path.
        /// </summary>
        /// <returns>The applicable path.</returns>
        /// <param name="remoteDoc">Remote document.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static string GetApplicablePath(IDocument remoteDoc, CmisSyncFolder cmisSyncFolder) {
            foreach (IFolder folder in remoteDoc.Parents) {
                if (PathIsApplicable(folder.Path, cmisSyncFolder)) {
                    return folder.Path;
                } 
            }
            return null;
        }

        /// <summary>
        /// Paths the is applicable.
        /// </summary>
        /// <returns><c>true</c>, if is applicable was pathed, <c>false</c> otherwise.</returns>
        /// <param name="remotePath">Remote path.</param>
        private static bool PathIsApplicable(string remotePath, CmisSyncFolder cmisSyncFolder)
        {
            // Ignore the change if not in a synchronized folder.
            if ( ! remotePath.StartsWith(cmisSyncFolder.RemotePath))
            {
                Logger.Info("Ignore change as it is not in the synchronized folder's path: " + remotePath);
                return false;
            }

            // Ignore if configured to be ignored.
            if (IsPathIgnored(remotePath, cmisSyncFolder))
            {
                Logger.Info("Ignore change as it is in a path configured to be ignored: " + remotePath);
                return false;
            }

            // In other case, the change is probably applicable.
            return true;
        }


        /// <summary>
        /// Is the remote object worth syncing.
        /// </summary>
        /// <returns><c>true</c>, if remote object worth syncing was ised, <c>false</c> otherwise.</returns>
        /// <param name="obj">Object.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static bool IsRemoteObjectInCmisSyncPath (ICmisObject obj, CmisSyncFolder cmisSyncFolder)
        {
            if (obj is IFolder) {
                if (((IFolder)obj).Path.StartsWith (cmisSyncFolder.RemotePath)) return true;
            } else if (obj is IDocument) {
                string path = SyncFileUtil.GetApplicablePath ((IDocument)obj, cmisSyncFolder);
                if (path != null && path.StartsWith (cmisSyncFolder.RemotePath)) return true;
            }
            return false; 
        }

    }
}
