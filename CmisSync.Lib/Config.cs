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
    /// <summary>
    /// Configuration of a CmisSync synchronized folder.
    /// It can be found in the XML configuration file.
    /// </summary>
    public class Config
    {
        /// <summary>
        /// XML document storing the configuration.
        /// </summary>
        private XmlDocument configXml = new XmlDocument();


        /// <summary>
        /// Full path to the XML configuration file.
        /// </summary>
        public string FullPath { get; private set; }


        /// <summary>
        /// Path of the folder where configuration files are.
        /// These files are in particular the XML configuration file, the database files, and the log file.
        /// </summary>
        public string ConfigPath { get; private set; }


        /// <summary>
        /// Path to the user's home folder.
        /// </summary>
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


        /// <summary>
        /// Path where the synchronized folders are stored by default.
        /// </summary>
        public string FoldersPath
        {
            get
            {
                return Path.Combine(HomePath, "DataSpace Sync");
            }
        }


        /// <summary>
        /// Constructor.
        /// </summary>
        public Config(string fullPath)
        {
            FullPath = fullPath;
            ConfigPath = Path.GetDirectoryName(FullPath);
            Console.WriteLine("FullPath:" + FullPath);

            // Create configuration folder if it does not exist yet.
            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);

            // Create an empty XML configuration file if none is present yet.
            if (!File.Exists(FullPath))
                CreateInitialConfig();

            // Load the XML configuration.
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

                // If the XML configuration file exists but with file size zero, then recreate it.
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


        /// <summary>
        /// Create an initial XML configuration file with default settings and zero remote folders.
        /// </summary>
        private void CreateInitialConfig()
        {
            // Get the user name.
            string userName = "Unknown";
            if (Backend.Platform == PlatformID.Unix ||
                Backend.Platform == PlatformID.MacOSX)
            {
                userName = Environment.UserName;
                if (string.IsNullOrEmpty(userName))
                {
                    userName = String.Empty;
                }
                else
                {
                    userName = userName.TrimEnd(",".ToCharArray());
                }
            }
            else
            {
                userName = Environment.UserName;
            }

            if (string.IsNullOrEmpty(userName))
            {
                userName = "Unknown";
            }

            // Define the default XML configuration file.
            configXml.LoadXml(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<CmisSync>
  <user>
    <name>" + userName + @"</name>
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

            // Save it as an XML file.
            Save();
        }


        /// <summary>
        /// Log4net configuration, as an XML tree readily usable by Log4net.
        /// </summary>
        /// <returns></returns>
        public XmlElement GetLog4NetConfig()
        {
            return (XmlElement)configXml.SelectSingleNode("/CmisSync/log4net");
        }


        /// <summary>
        /// List of the CmisSync synchronized folders.
        /// </summary>
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


        /// <summary>
        /// Add a synchronized folder to the configuration.
        /// </summary>
        public void AddFolder(RepoInfo repoInfo)
        {
            if (null == repoInfo)
            {
                return;
            }

            this.AddFolder(repoInfo.Name, repoInfo.TargetDirectory, repoInfo.Address, repoInfo.RepoID, repoInfo.RemotePath, repoInfo.User, repoInfo.Password, repoInfo.PollInterval, repoInfo.getIgnoredPaths());
        }


        /// <summary>
        /// Add a synchronized folder to the configuration.
        /// </summary>
        private void AddFolder(string name, string path, Uri url, string repository,
            string remoteFolder, string user, string password, double pollinterval, string[] ignoredPaths)
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
            foreach(string ignoredPath in ignoredPaths)
            {
                XmlNode ignoreNode = configXml.CreateElement("ignoredFolder");
                XmlNode attr = configXml.CreateAttribute("path");
                attr.Value = ignoredPath;
                ignoreNode.Attributes.SetNamedItem(attr);
                node_folder.AppendChild(ignoreNode);
            }


            XmlNode node_root = configXml.SelectSingleNode("/CmisSync/folders");
            if (node_root == null)
            {
                node_root = configXml.CreateElement("folders");
                configXml.SelectSingleNode("/CmisSync").AppendChild(node_root);
            }
            node_root.AppendChild(node_folder);

            Save();
        }


        /// <summary>
        /// Remove a synchronized folder from the configuration.
        /// </summary>
        public void RemoveFolder(string name)
        {
            foreach (XmlNode node_folder in configXml.SelectNodes("/CmisSync/folders/folder"))
            {
                if (node_folder["name"].InnerText.Equals(name))
                    configXml.SelectSingleNode("/CmisSync/folders").RemoveChild(node_folder);
            }

            Save();
        }


        /// <summary>
        /// Get the remote CMIS endpoint URL for a particular synchronized folder.
        /// </summary>
        public Uri GetUrlForFolder(string name)
        {
            return new Uri(GetFolderAttribute(name, "url"));
        }


        /// <summary>
        /// Get an attribute of a particular synchronized folder.
        /// </summary>
        public string GetFolderAttribute(string folderName, string attribute)
        {
            XmlNode folder = GetFolder(folderName);

            if (folder != null)
            {
                if (folder[attribute] != null)
                    return folder[attribute].InnerText;
                else
                    return null;

            }
            else
            {
                return null;
            }
        }

        public LinkedList<string> getIgnoredFolders(string folderName)
        {
            LinkedList<string> result = new LinkedList<string>();
            XmlNode folder = GetFolder(folderName);
            if( folder != null)
            {
                if(folder.HasChildNodes)
                {
                    foreach (XmlNode node in folder.ChildNodes) {
                        if(node.Name.Equals("ignoredFolder"))
                        {
                            if(node.Attributes["path"]!=null)
                            {
                                result.AddLast(node.Attributes["path"].InnerText);
                            }
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Get all the configured info about a synchronized folder.
        /// </summary>
        public RepoInfo GetRepoInfo(string folderName)
        {
            RepoInfo repoInfo = new RepoInfo(folderName, ConfigPath);

            repoInfo.User = GetFolderAttribute(folderName, "user");
            repoInfo.Password = GetFolderAttribute(folderName, "password");
            repoInfo.Address = GetUrlForFolder(folderName);
            repoInfo.RepoID = GetFolderAttribute(folderName, "repository");
            repoInfo.RemotePath = GetFolderAttribute(folderName, "remoteFolder");
            repoInfo.TargetDirectory = GetFolderAttribute(folderName, "path");
            
            double pollinterval = 0;
            double.TryParse(GetFolderAttribute(folderName, "pollinterval"), out pollinterval);
            if (pollinterval < 1) pollinterval = 5000;
            repoInfo.PollInterval = pollinterval;

            if (String.IsNullOrEmpty(repoInfo.TargetDirectory))
            {
                repoInfo.TargetDirectory = Path.Combine(FoldersPath, folderName);
            }
            LinkedList<string> ignoredFolders = getIgnoredFolders(folderName);
            foreach (string ignoredFolder in ignoredFolders)
            {
                repoInfo.addIgnorePath(ignoredFolder);
            }
            return repoInfo;
        }


        /// <summary>
        /// Get the XML node containing the configuration of a particular synchronized folder.
        /// </summary>
        private XmlNode GetFolder(string name)
        {
            return configXml.SelectSingleNode(string.Format("/CmisSync/folders/folder[name=\"{0}\"]", name));
        }


        /// <summary>
        /// Get a general configuration option (not about a particular synchronized folder), per its name.
        /// </summary>
        public string GetConfigOption(string name)
        {
            XmlNode node = configXml.SelectSingleNode("/CmisSync/" + name);

            if (node != null)
                return node.InnerText;
            else
                return null;
        }


        /// <summary>
        /// Set a general configuration option (not about a particular synchronized folder), per its name.
        /// </summary>
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


        /// <summary>
        /// Get the configured path to the log file.
        /// </summary>
        public string GetLogFilePath()
        {
            return Path.Combine(ConfigPath, "debug_log.txt");
        }


        /// <summary>
        /// Save the currently loaded (in memory) configuration back to the XML file.
        /// </summary>
        private void Save()
        {
            configXml.Save(FullPath);
        }
    }
}
