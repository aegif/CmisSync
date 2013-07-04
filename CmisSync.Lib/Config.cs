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

    public class Config
    {
        private XmlDocument configXml = new XmlDocument();
        public string FullPath { get; private set; }
        //public string TmpPath;
        //public string LogFilePath;

        public string ConfigPath { get; private set; }

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
                    XmlNode debugNode = configXml.SelectSingleNode(@"/CmisSync/debug");
                    bool debug = false;
                    bool.TryParse(debugNode.InnerText, out debug);
                    return debug;
                }
                catch (System.Xml.XPath.XPathException)
                {
                    return false;
                }
            }
        }

        public Config(string fullPath)
        {
            FullPath = fullPath;
            ConfigPath = Path.GetDirectoryName(FullPath);
            Console.WriteLine("FullPath:" + FullPath);

            //if (File.Exists(LogFilePath))
            //{
            //    try
            //    {
            //        File.Delete(LogFilePath);

            //    }
            //    catch (Exception)
            //    {
            //        // Don't delete the debug.log if, for example, 'tail' is reading it
            //    }
            //}

            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);

            if (!File.Exists(FullPath))
                CreateInitialConfig();

            try
            {
                configXml.Load(FullPath);
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
                configXml.Load(FullPath);
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
                    user_name = String.Empty;
                else
                    user_name = user_name.TrimEnd(",".ToCharArray());

            }
            else
            {
                user_name = Environment.UserName;
            }

            if (string.IsNullOrEmpty(user_name))
            {
                user_name = "Unknown";
            }

            configXml.LoadXml(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<CmisSync>
  <user>
    <name>" + user_name + @"</name>
    <email>Unknown</email>
  </user>
  <log4net>
    <appender name=""CmisSyncFileAppender"" type=""log4net.Appender.RollingFileAppender"">
      <file value=""" + GetLogFilePath() + @""" />
      <appendToFile value=""true"" />
      <rollingStyle value=""Size"" />
      <maxSizeRollBackups value=""10"" />
      <maximumFileSize value=""5MB"" />
      <staticLogFileName value=""true"" />
      <layout type=""log4net.Layout.PatternLayout"">
        <conversionPattern value=""%date [%thread] %-5level %logger [%property{NDC}] - %message%newline"" />
      </layout>
    </appender>
    <root>
      <level value=""DEBUG"" />
      <appender-ref ref=""CmisSyncFileAppender"" />
    </root>
  </log4net>
  <folders>
  </folders>
  <notifications>True</notifications>
</CmisSync>");

            Save();
        }

        public XmlElement GetLog4NetConfig()
        {
            return (XmlElement)configXml.SelectSingleNode("/CmisSync/log4net");
        }

        public System.Collections.ObjectModel.Collection<string> Folders
        {
            get
            {
                List<string> folders = new List<string>();

                foreach (XmlNode node_folder in configXml.SelectNodes("/CmisSync/folders/folder"))
                    folders.Add(node_folder["name"].InnerText);

                return new System.Collections.ObjectModel.Collection<string>(folders);
            }
        }

        public void AddFolder(RepoInfo repoInfo)
        {
            if (null == repoInfo)
            {
                return;
            }

            this.AddFolder(repoInfo.Name, repoInfo.TargetDirectory, repoInfo.Address, repoInfo.RepoID, repoInfo.RemotePath, repoInfo.User, repoInfo.Password, repoInfo.PollInterval);
        }

        private void AddFolder(string name, string path, Uri url, string repository,
            string remoteFolder, string user, string password, double pollinterval)
        {
            XmlNode node_name = configXml.CreateElement("name");
            XmlNode node_path = configXml.CreateElement("path");
            XmlNode node_url = configXml.CreateElement("url");
            XmlNode node_repository = configXml.CreateElement("repository");
            XmlNode node_remoteFolder = configXml.CreateElement("remoteFolder");
            XmlNode node_user = configXml.CreateElement("user");
            XmlNode node_password = configXml.CreateElement("password");
            XmlNode node_pollinterval = configXml.CreateElement("pollinterval");

            node_name.InnerText = name;
            node_path.InnerText = path;
            node_url.InnerText = url.ToString();
            node_repository.InnerText = repository;
            node_remoteFolder.InnerText = remoteFolder;
            node_user.InnerText = user;
            node_password.InnerText = password;
            node_pollinterval.InnerText = pollinterval.ToString();

            XmlNode node_folder = configXml.CreateNode(XmlNodeType.Element, "folder", null);

            node_folder.AppendChild(node_name);
            node_folder.AppendChild(node_path);
            node_folder.AppendChild(node_url);
            node_folder.AppendChild(node_repository);
            node_folder.AppendChild(node_remoteFolder);
            node_folder.AppendChild(node_user);
            node_folder.AppendChild(node_password);
            node_folder.AppendChild(node_pollinterval);

            XmlNode node_root = configXml.SelectSingleNode("/CmisSync/folders");
            if (node_root == null)
            {
                node_root = configXml.CreateElement("folders");
                configXml.SelectSingleNode("/CmisSync").AppendChild(node_root);
            }
            node_root.AppendChild(node_folder);

            Save();
        }


        public void RemoveFolder(string name)
        {
            foreach (XmlNode node_folder in configXml.SelectNodes("/CmisSync/folders/folder"))
            {
                if (node_folder["name"].InnerText.Equals(name))
                    configXml.SelectSingleNode("/CmisSync/folders").RemoveChild(node_folder);
            }

            Save();
        }


        public void RenameFolder(string identifier, string name)
        {
            XmlNode node_folder = configXml.SelectSingleNode(
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


        public Uri GetUrlForFolder(string name)
        {
            return new Uri(GetFolderValue(name, "url"));
        }


        public bool IdentifierExists(string identifier)
        {
            if (identifier == null)
                throw new ArgumentNullException("identifier");

            foreach (XmlNode node_folder in configXml.SelectNodes("/CmisSync/folders/folder"))
            {
                XmlElement folder_id = node_folder["identifier"];

                if (folder_id != null && identifier.Equals(folder_id.InnerText))
                    return true;
            }

            return false;
        }


        public bool SetFolderOptionalAttribute(string folderName, string key, string value)
        {
            XmlNode folder = GetFolder(folderName);

            if (folder == null)
                return false;

            if (folder[key] != null)
            {
                folder[key].InnerText = value;

            }
            else
            {
                XmlNode new_node = configXml.CreateElement(key);
                new_node.InnerText = value;
                folder.AppendChild(new_node);
            }

            Save();

            return true;
        }


        public string GetFolderOptionalAttribute(string folderName, string key)
        {
            XmlNode folder = GetFolder(folderName);

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

        public RepoInfo GetRepoInfo(string folderName)
        {
            RepoInfo repoInfo = new RepoInfo(folderName, ConfigPath);

            repoInfo.User = GetFolderOptionalAttribute(folderName, "user");
            repoInfo.Password = GetFolderOptionalAttribute(folderName, "password");
            repoInfo.Address = GetUrlForFolder(folderName);
            repoInfo.RepoID = GetFolderOptionalAttribute(folderName, "repository");
            repoInfo.RemotePath = GetFolderOptionalAttribute(folderName, "remoteFolder");
            repoInfo.TargetDirectory = GetFolderOptionalAttribute(folderName, "path");
            
            double pollinterval = 0;
            double.TryParse(GetFolderOptionalAttribute(folderName, "pollinterval"), out pollinterval);
            if (pollinterval < 1) pollinterval = 5000;
            repoInfo.PollInterval = pollinterval;

            if (String.IsNullOrEmpty(repoInfo.TargetDirectory))
            {
                repoInfo.TargetDirectory = Path.Combine(FoldersPath, folderName);
            }

            return repoInfo;
        }

        private XmlNode GetFolder(string name)
        {
            return configXml.SelectSingleNode(string.Format("/CmisSync/folders/folder[name=\"{0}\"]", name));
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
            XmlNode node = configXml.SelectSingleNode("/CmisSync/" + name);

            if (node != null)
                return node.InnerText;
            else
                return null;
        }


        public void SetConfigOption(string name, string content)
        {
            XmlNode node = configXml.SelectSingleNode("/CmisSync/" + name);

            if (node != null)
            {
                node.InnerText = content;

            }
            else
            {
                node = configXml.CreateElement(name);
                node.InnerText = content;

                XmlNode node_root = configXml.SelectSingleNode("/CmisSync");
                node_root.AppendChild(node);
            }

            Save();
        }


        private void Save()
        {
            //if (!File.Exists(FullPath))
            //    throw new FileNotFoundException(FullPath + " does not exist");

            configXml.Save(FullPath);
        }


        public string GetLogFilePath()
        {
            return Path.Combine(ConfigPath, "debug_log.txt");
        }
    }
}
