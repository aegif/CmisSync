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
//   along with this program.  If not, see <http://www.gnu.org/licenses/>.


using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;

namespace CmisSync.Lib
{

    public class Config : XmlDocument
    {

        public static Config DefaultConfig;

        public string FullPath;
        public string TmpPath;
        public string LogFilePath;
        private string configpath;

        public string ConfigPath { get { return configpath; } }

        public string HomePath
        {
            get
            {
                if (Backend.Platform == PlatformID.Win32NT)
                    return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                else
                    return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            }
        }


        public string FoldersPath
        {
            get
            {
                return Path.Combine(HomePath, "CmisSync");
            }
        }

        public bool DebugMode
        {
            get
            {
                try
                {
                    XmlNode debugNode = SelectSingleNode(@"/CmisSync/debug");
                    bool debug = false;
                    bool.TryParse(debugNode.InnerText, out debug);
                    return debug;
                }
                catch
                {
                    return false;
                }
            }
        }

        public Config(string config_path, string config_file_name)
        {
            configpath = config_path;
            FullPath = Path.Combine(config_path, config_file_name);
            Console.WriteLine("FullPath:" + FullPath);
            LogFilePath = Path.Combine(config_path, "debug_log.txt");

            if (File.Exists(LogFilePath))
            {
                try
                {
                    File.Delete(LogFilePath);

                }
                catch (Exception)
                {
                    // Don't delete the debug.log if, for example, 'tail' is reading it
                }
            }

            if (!Directory.Exists(config_path))
                Directory.CreateDirectory(config_path);

            try
            {
                Load(FullPath);
            }
            catch (TypeInitializationException)
            {
                CreateInitialConfig();
            }
            catch (FileNotFoundException)
            {
                CreateInitialConfig();
            }
            catch (XmlException)
            {
                FileInfo file = new FileInfo(FullPath);

                if (file.Length == 0)
                {
                    File.Delete(FullPath);
                    CreateInitialConfig();
                }
                else
                {
                    throw new XmlException(FullPath + " does not contain a valid config XML structure.");
                }

            }
            finally
            {
                Load(FullPath);
                //TmpPath = Path.Combine (FoldersPath, ".tmp");
                //Directory.CreateDirectory (TmpPath);
            }
        }


        private void CreateInitialConfig()
        {
            string user_name = "Unknown";

            if (Backend.Platform == PlatformID.Unix ||
                Backend.Platform == PlatformID.MacOSX)
            {

                user_name = Environment.UserName;
                if (string.IsNullOrEmpty(user_name))
                    user_name = "";
                else
                    user_name = user_name.TrimEnd(",".ToCharArray());

            }
            else
            {
                user_name = Environment.UserName;
            }

            if (string.IsNullOrEmpty(user_name))
                user_name = "Unknown";


            AppendChild(CreateXmlDeclaration("1.0", "UTF-8", "yes"));
            AppendChild(CreateElement("CmisSync"));
            XmlNode user = CreateElement("user");
            XmlNode username = CreateElement("name");
            username.InnerText = user_name;
            XmlNode email = CreateElement("email");
            email.InnerText = "Unknown";

            user.AppendChild(username);
            user.AppendChild(email);

            XmlNode folders = CreateElement("folders");
            DocumentElement.AppendChild(user);
            CreateLog4NetDefaultConfig();
            DocumentElement.AppendChild(folders);

            //string n = Environment.NewLine;
            //File.WriteAllText(FullPath,
            //    "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>" + n +
            //    "<CmisSync>" + n +
            //    "  <user>" + n +
            //    "    <name>" + user_name + "</name>" + n +
            //    "    <email>Unknown</email>" + n +
            //    "  </user>" + n +
            //    "  <log4net>" + n +
            //    "  <log4net>" + n +
            //    "</CmisSync>");
        }

        public XmlNode GetLog4NetConfig()
        {
            return SelectSingleNode("/CmisSync/log4net");
        }

        private void CreateLog4NetDefaultConfig()
        {
            XmlNode log4net = CreateElement("log4net");

            XmlNode appender = CreateElement("appender");
            appender.Attributes["name"].Value = "CmisSyncFileAppender";
            appender.Attributes["type"].Value = "log4net.Appender.RollingFileAppender";

            XmlNode file = CreateElement("file");
            file.Attributes["value"].Value = "log.txt";
            appender.AppendChild(file);

            XmlNode appendToFile = CreateElement("appendToFile");
            appendToFile.Attributes["value"].Value = "true";
            appender.AppendChild(appendToFile);

            XmlNode rollingStyle = CreateElement("rollingStyle");
            rollingStyle.Attributes["value"].Value = "Size";
            appender.AppendChild(rollingStyle);

            XmlNode maxSizeRollBackups = CreateElement("maxSizeRollBackups");
            maxSizeRollBackups.Attributes["value"].Value = "10";
            appender.AppendChild(maxSizeRollBackups);

            XmlNode maximumFileSize = CreateElement("maximumFileSize");
            maximumFileSize.Attributes["value"].Value = "100KB";
            appender.AppendChild(maximumFileSize);

            XmlNode staticLogFileName = CreateElement("staticLogFileName");
            staticLogFileName.Attributes["value"].Value = "true";
            appender.AppendChild(staticLogFileName);

            XmlNode layout = CreateElement("layout");
            layout.Attributes["type"].Value = "log4net.Layout.PatternLayout";

            XmlNode conversionPattern = CreateElement("conversionPattern");
            conversionPattern.Attributes["value"].Value = "%date [%thread] %-5level %logger [%property{NDC}] - %message%newline";
            layout.AppendChild(conversionPattern);
            appender.AppendChild(layout);
            log4net.AppendChild(appender);

            XmlNode root = CreateElement("root");
            XmlNode level = CreateElement("level");
            level.Attributes["value"].InnerText = "DEBUG";
            root.AppendChild(level);

            XmlNode appenderref = CreateElement("appender-ref");
            appenderref.Attributes["ref"].Value = "CmisSyncFileAppender";
            root.AppendChild(appenderref);

            log4net.AppendChild(root);

            DocumentElement.AppendChild(log4net);
        }


        public User User
        {
            get
            {
                XmlNode name_node = SelectSingleNode("/CmisSync/user/name/text()");
                string name = name_node.Value;

                XmlNode email_node = SelectSingleNode("/CmisSync/user/email/text()");
                string email = email_node.Value;

                string pubkey_file_path = Path.Combine(
                    Path.GetDirectoryName(FullPath), "CmisSync." + email + ".key.pub");

                User user = new User(name, email);

                if (File.Exists(pubkey_file_path))
                    user.PublicKey = File.ReadAllText(pubkey_file_path);

                return user;
            }

            set
            {
                User user = (User)value;

                XmlNode name_node = SelectSingleNode("/CmisSync/user/name/text()");
                name_node.InnerText = user.Name;

                XmlNode email_node = SelectSingleNode("/CmisSync/user/email/text()");
                email_node.InnerText = user.Email;

                Save();
            }
        }


        public List<string> Folders
        {
            get
            {
                List<string> folders = new List<string>();

                foreach (XmlNode node_folder in SelectNodes("/CmisSync/folders/folder"))
                    folders.Add(node_folder["name"].InnerText);

                return folders;
            }
        }

        public void AddFolder(RepoInfo repoInfo)
        {
            this.AddFolder(repoInfo.Name, repoInfo.TargetDirectory, repoInfo.Identifier, repoInfo.Address.ToString(), repoInfo.Backend, repoInfo.RepoID, repoInfo.RemotePath, repoInfo.User, repoInfo.Password);
        }

        public void AddFolder(string name, string path, string identifier, string url, string backend,
            string repository, string remoteFolder, string user, string password)
        {
            XmlNode node_name = CreateElement("name");
            XmlNode node_path = CreateElement("path");
            XmlNode node_identifier = CreateElement("identifier");
            XmlNode node_url = CreateElement("url");
            XmlNode node_backend = CreateElement("backend");
            XmlNode node_repository = CreateElement("repository");
            XmlNode node_remoteFolder = CreateElement("remoteFolder");
            XmlNode node_user = CreateElement("user");
            XmlNode node_password = CreateElement("password");

            node_name.InnerText = name;
            node_path.InnerText = path;
            node_identifier.InnerText = identifier;
            node_url.InnerText = url;
            node_backend.InnerText = backend;
            node_repository.InnerText = repository;
            node_remoteFolder.InnerText = remoteFolder;
            node_user.InnerText = user;
            node_password.InnerText = password;

            XmlNode node_folder = CreateNode(XmlNodeType.Element, "folder", null);

            node_folder.AppendChild(node_name);
            node_folder.AppendChild(node_path);
            node_folder.AppendChild(node_identifier);
            node_folder.AppendChild(node_url);
            node_folder.AppendChild(node_backend);
            node_folder.AppendChild(node_repository);
            node_folder.AppendChild(node_remoteFolder);
            node_folder.AppendChild(node_user);
            node_folder.AppendChild(node_password);

            XmlNode node_root = SelectSingleNode("/CmisSync/folders");
            node_root.AppendChild(node_folder);

            Save();
        }


        public void RemoveFolder(string name)
        {
            foreach (XmlNode node_folder in SelectNodes("/CmisSync/folders/folder"))
            {
                if (node_folder["name"].InnerText.Equals(name))
                    SelectSingleNode("/CmisSync/folders").RemoveChild(node_folder);
            }

            Save();
        }


        public void RenameFolder(string identifier, string name)
        {
            XmlNode node_folder = SelectSingleNode(
                string.Format("/CmisSync/folders/folder[identifier=\"{0}\"]", identifier));

            node_folder["name"].InnerText = name;
            Save();
        }


        public string GetBackendForFolder(string name)
        {
            return "Cmis"; // TODO GetFolderValue (name, "backend");
        }


        public string GetIdentifierForFolder(string name)
        {
            return GetFolderValue(name, "identifier");
        }


        public string GetUrlForFolder(string name)
        {
            return GetFolderValue(name, "url");
        }


        public bool IdentifierExists(string identifier)
        {
            if (identifier == null)
                throw new ArgumentNullException();

            foreach (XmlNode node_folder in SelectNodes("/CmisSync/folders/folder"))
            {
                XmlElement folder_id = node_folder["identifier"];

                if (folder_id != null && identifier.Equals(folder_id.InnerText))
                    return true;
            }

            return false;
        }


        public bool SetFolderOptionalAttribute(string folder_name, string key, string value)
        {
            XmlNode folder = GetFolder(folder_name);

            if (folder == null)
                return false;

            if (folder[key] != null)
            {
                folder[key].InnerText = value;

            }
            else
            {
                XmlNode new_node = CreateElement(key);
                new_node.InnerText = value;
                folder.AppendChild(new_node);
            }

            Save();

            return true;
        }


        public string GetFolderOptionalAttribute(string folder_name, string key)
        {
            XmlNode folder = GetFolder(folder_name);

            if (folder != null)
            {
                if (folder[key] != null)
                    return folder[key].InnerText;
                else
                    return null;

            }
            else
            {
                return null;
            }
        }

        public RepoInfo GetRepoInfo(string FolderName)
        {
            RepoInfo repoInfo = new RepoInfo(FolderName, ConfigPath);

            repoInfo.User = GetFolderOptionalAttribute(FolderName, "user");
            repoInfo.Password = GetFolderOptionalAttribute(FolderName, "password");
            repoInfo.Address = new Uri(GetUrlForFolder(FolderName));
            repoInfo.RepoID = GetFolderOptionalAttribute(FolderName, "repository");
            repoInfo.RemotePath = GetFolderOptionalAttribute(FolderName, "remoteFolder");
            repoInfo.TargetDirectory = GetFolderOptionalAttribute(FolderName, "path");
            if (String.IsNullOrEmpty(repoInfo.TargetDirectory))
            {
                repoInfo.TargetDirectory = Path.Combine(FoldersPath, FolderName);
            }

            return repoInfo;
        }

        private XmlNode GetFolder(string name)
        {
            return SelectSingleNode(string.Format("/CmisSync/folders/folder[name=\"{0}\"]", name));
        }


        private string GetFolderValue(string name, string key)
        {
            XmlNode folder = GetFolder(name);

            if ((folder != null) && (folder[key] != null))
                return folder[key].InnerText;
            else
                return null;
        }


        public string GetConfigOption(string name)
        {
            XmlNode node = SelectSingleNode("/CmisSync/" + name);

            if (node != null)
                return node.InnerText;
            else
                return null;
        }


        public void SetConfigOption(string name, string content)
        {
            XmlNode node = SelectSingleNode("/CmisSync/" + name);

            if (node != null)
            {
                node.InnerText = content;

            }
            else
            {
                node = CreateElement(name);
                node.InnerText = content;

                XmlNode node_root = SelectSingleNode("/CmisSync");
                node_root.AppendChild(node);
            }

            Save();
            Logger.LogInfo("Config", "Updated option " + name + ":" + content);
        }


        private void Save()
        {
            if (!File.Exists(FullPath))
                throw new FileNotFoundException(FullPath + " does not exist");

            Save(FullPath);
            Logger.LogInfo("Config", "Wrote to '" + FullPath + "'");
        }
    }
}
