//   SparkleShare, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
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
using System.IO;
using System.Collections.Generic;

namespace SparkleLib {

    public enum SparkleChangeType {
        Added,
        Edited,
        Deleted,
        Moved
    }


    public class SparkleChangeSet {

        public SparkleUser User = new SparkleUser ("Unknown", "Unknown");

        public SparkleFolder Folder;
        public string Revision;
        public DateTime Timestamp;
        public DateTime FirstTimestamp;
        public Uri RemoteUrl;

        public List<SparkleChange> Changes = new List<SparkleChange> ();
    }


    public class SparkleChange {

        public SparkleChangeType Type;
		public DateTime Timestamp;
		
        public string Path;
        public string MovedToPath;
    }


    public class SparkleFolder {

        //public static string ROOT_FOLDER =
        //    (Environment.OSVersion.Platform == PlatformID.Unix || 
        //    Environment.OSVersion.Platform == PlatformID.MacOSX) ?
        //           Environment.GetEnvironmentVariable("HOME") :
        //           Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%")
        //    + Path.DirectorySeparatorChar
        //    + "CmisSync";

        // public static string APPDATA_FOLDER =


        public string Name;
        public Uri RemoteAddress;

        public string FullPath {
            get {
                string custom_path = SparkleConfig.DefaultConfig.GetFolderOptionalAttribute(Name, "path");

                if (custom_path != null)
                    return Path.Combine(custom_path, Name);
                else
                    return Path.Combine(SparkleConfig.DefaultConfig.FoldersPath, Name);
                // return Path.Combine(ROOT_FOLDER, Name);
            }
        }


        public SparkleFolder (string name)
        {
            Name = name;
        }

        public static bool HasWritePermissionOnDir(string path)
        {
            var writeAllow = false;
            var writeDeny = false;
            var accessControlList = Directory.GetAccessControl(path);
            if (accessControlList == null)
                return false;
            var accessRules = accessControlList.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
            if (accessRules == null)
                return false;

            foreach (System.Security.AccessControl.FileSystemAccessRule rule in accessRules)
            {
                if ((System.Security.AccessControl.FileSystemRights.Write & rule.FileSystemRights) != System.Security.AccessControl.FileSystemRights.Write) continue;

                if (rule.AccessControlType == System.Security.AccessControl.AccessControlType.Allow)
                    writeAllow = true;
                else if (rule.AccessControlType == System.Security.AccessControl.AccessControlType.Deny)
                    writeDeny = true;
            }

            return writeAllow && !writeDeny;
        }
    }


    public class SparkleAnnouncement {

        public readonly string FolderIdentifier;
        public readonly string Message;


        public SparkleAnnouncement (string folder_identifier, string message)
        {
            FolderIdentifier = folder_identifier;
            Message          = message;
        }
    }
}
