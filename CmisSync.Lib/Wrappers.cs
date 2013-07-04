//   CmisSync, a collaboration and sharing tool.
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
#if __MonoCS__
using Mono.Unix.Native;
#endif

namespace CmisSync.Lib
{

    public enum CmisChangeType
    {
        Added,
        Edited,
        Deleted,
        Moved
    }


    public class ChangeSet
    {

        public Folder Folder { get; set; }
        public string Revision { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime FirstTimestamp { get; set; }
        public Uri RemoteUrl { get; set; }

        public System.Collections.ObjectModel.Collection<Change> Changes { get; private set; }
    }


    public class Change
    {
        public CmisChangeType Type { get; set; }
        public DateTime Timestamp { get; set; }

        public string Path { get; set; }
        public string MovedToPath { get; set; }
    }


    public class Folder
    {
        public string Name { get; set; }
        public Uri RemoteAddress { get; set; }

        public string FullPath
        {
            get
            {
                string custom_path = ConfigManager.CurrentConfig.GetFolderOptionalAttribute(Name, "path");
                // if (String.IsNullOrEmpty(custom_path)) custom_path = Config.DefaultConfig.FoldersPath;

                if (custom_path != null)
                    return custom_path;
                else
                    return Path.Combine(ConfigManager.CurrentConfig.FoldersPath, Name);
                // return Path.Combine(ROOT_FOLDER, Name);
            }
        }


        public Folder(string name)
        {
            Name = name;
        }

        public static bool HasWritePermissionOnDir(string path)
        {
            var writeAllow = false;
            var writeDeny = false;
            try {
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
            } catch (System.PlatformNotSupportedException) {
#if __MonoCS__
                writeAllow = (0 == Syscall.access(path, AccessModes.W_OK));
#endif
            }

            return writeAllow && !writeDeny;
        }
    }
}
