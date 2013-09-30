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
using System.Xml.Serialization;
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
        /// data structure storing the configuration.
        /// </summary>
        private SyncConfig configXml;


        /// <summary>
        /// Full path to the XML configuration file.
        /// </summary>
        public string FullPath { get; private set; }


        /// <summary>
        /// Path of the folder where configuration files are.
        /// These files are in particular the XML configuration file, the database files, and the log file.
        /// </summary>
        public string ConfigPath { get; private set; }

        public bool Notifications { get { return configXml.Notifications; } set { configXml.Notifications = value; } }

        public List<SyncConfig.Folder> Folder { get { return configXml.Folders; } }

        public SyncConfig.Folder getFolder(string name)
        {
            foreach (SyncConfig.Folder folder in configXml.Folders)
            {
                if( folder.DisplayName.Equals(name))
                    return folder;
            }
            return null;
        }

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
                return Path.Combine(HomePath, "DataSpace");
            }
        }


        /// <summary>
        /// Constructor.
        /// </summary>
        public Config(string fullPath)
        {
            FullPath = fullPath;
            ConfigPath = Path.GetDirectoryName(FullPath);
            Console.WriteLine("FullPath: " + FullPath);

            // Create configuration folder if it does not exist yet.
            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);

            // Create an empty XML configuration file if none is present yet.
            if (!File.Exists(FullPath))
                CreateInitialConfig();

            // Load the XML configuration.
            try
            {
                Load();
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
                Load();
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
            configXml = new SyncConfig()
            {
                Folders = new List<SyncConfig.Folder>(),
                User = new User()
                {
                    EMail = "Unknown",
                    Name = userName
                },
                Notifications = true,
                Log4Net = createDefaultLog4NetElement()
            };

            // Save it as an XML file.
            Save();
        }


        /// <summary>
        /// Log4net configuration, as an XML tree readily usable by Log4net.
        /// </summary>
        /// <returns></returns>
        public XmlElement GetLog4NetConfig()
        {
            return configXml.Log4Net as XmlElement;
        }

        /// <summary>
        /// Sets a new XmlNode as Log4NetConfig. Is useful for config migration
        /// </summary>
        /// <param name="node"></param>
        public void SetLog4NetConfig(XmlNode node)
        {
            this.configXml.Log4Net = node;
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
            SyncConfig.Folder folder = new SyncConfig.Folder() {
                DisplayName = repoInfo.Name,
                LocalPath = repoInfo.TargetDirectory,
                IgnoredFolders = new List<IgnoredFolder>(),
                RemoteUrl = repoInfo.Address,
                RepositoryId = repoInfo.RepoID,
                RemotePath = repoInfo.RemotePath,
                UserName = repoInfo.User,
                ObfuscatedPassword = repoInfo.Password.ObfuscatedPassword,
                PollInterval = repoInfo.PollInterval,
                SupportedFeatures = null
            };
            foreach (string ignoredFolder in repoInfo.getIgnoredPaths())
            {
                folder.IgnoredFolders.Add(new IgnoredFolder(){Path = ignoredFolder});
            }
            this.configXml.Folders.Add(folder);

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
        public void Save()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(SyncConfig));
            using (TextWriter textWriter = new StreamWriter(FullPath))
            {
                serializer.Serialize(textWriter, this.configXml);
            }
        }


        private void Load()
        {
            XmlSerializer deserializer = new XmlSerializer(typeof(SyncConfig));
            using (TextReader textReader = new StreamReader(FullPath))
            {
                this.configXml = (SyncConfig)deserializer.Deserialize(textReader);
            }
        }

        private XmlElement createDefaultLog4NetElement()
        {
            XmlSerializer deserializer = new XmlSerializer(typeof(XmlElement));
            using (TextReader textReader = new StringReader(@"
  <log4net>
    <appender name=""CmisSyncFileAppender"" type=""log4net.Appender.RollingFileAppender"">
      <file value=""" + GetLogFilePath() + @""" />
      <appendToFile value=""true"" />
      <rollingStyle value=""Size"" />
      <maxSizeRollBackups value=""10"" />
      <maximumFileSize value=""5MB"" />
      <staticLogFileName value=""true"" />
      <layout type=""log4net.Layout.PatternLayout"">
        <conversionPattern value=""%date [%thread] %-5level %logger - %message%newline"" />
      </layout>
    </appender>
    <root>
      <level value=""INFO"" />
      <appender-ref ref=""CmisSyncFileAppender"" />
    </root>
  </log4net>"))
            {
                XmlElement result = (XmlElement)deserializer.Deserialize(textReader);
                return result;
            }
        }

        [XmlRoot("CmisSync", Namespace=null)]
        public class SyncConfig {
            [XmlElement("notifications")]
            public Boolean Notifications { get; set; }
            [XmlAnyElement("log4net")]
            public XmlNode Log4Net { get; set; }
            /// <summary>
            /// List of the CmisSync synchronized folders.
            /// </summary>
            [XmlArray("folders")]
            [XmlArrayItem("folder")]
            public List<SyncConfig.Folder> Folders { get; set; }
            [XmlElement("user", typeof(User))]
            public User User { get; set; }

            public class Folder {
            
                [XmlElement("name")]
                public string DisplayName { get; set; }

                [XmlElement("path")]
                public string LocalPath { get; set; }
 
                [XmlElement("url")]
                public XmlUri RemoteUrl { get; set; }
                
                [XmlElement("repository")]
                public string RepositoryId { get; set; }
                
                [XmlElement("remoteFolder")]
                public string RemotePath { get; set; }

                [XmlElement("user")]
                public string UserName { get; set; } 
                
                [XmlElement("password")]
                public string ObfuscatedPassword { get; set; }

                private double pollInterval = 5000;
                [XmlElement("pollinterval"), System.ComponentModel.DefaultValue(5000)]
                public double PollInterval
                {
                    get { return pollInterval; }
                    set {
                        if (value <= 0)
                        {
                            pollInterval = 5000;
                        }
                        else
                        {
                            pollInterval = value;
                        }
                } }

                [XmlElement("features", IsNullable=true)]
                public Feature SupportedFeatures { get; set;}

                [XmlElement("ignoreFolder", IsNullable=true)]
                public List<IgnoredFolder> IgnoredFolders { get; set; }

                private long chunkSize = 1024 * 1024;
                [XmlElement("chunkSize"), System.ComponentModel.DefaultValue(1024 * 1024)]
                public long ChunkSize
                {
                    get { return chunkSize; }
                    set
                    {
                        if (value < 0)
                        {
                            chunkSize = 0;
                        }
                        else
                        {
                            chunkSize = value;
                        }
                    }
                }

                /// <summary>
                /// Get all the configured info about a synchronized folder.
                /// </summary>
                public RepoInfo GetRepoInfo()
                {
                    RepoInfo repoInfo = new RepoInfo(DisplayName, ConfigManager.CurrentConfig.ConfigPath);
                    repoInfo.User = UserName;
                    repoInfo.Password = new CmisSync.Lib.RepoInfo.CmisPassword();
                    repoInfo.Password.ObfuscatedPassword = ObfuscatedPassword;
                    repoInfo.Address = RemoteUrl;
                    repoInfo.RepoID = RepositoryId;
                    repoInfo.RemotePath = RemotePath;
                    repoInfo.TargetDirectory = LocalPath;
                    if (PollInterval < 1) PollInterval = 5000;
                        repoInfo.PollInterval = PollInterval;
                    foreach (IgnoredFolder ignoredFolder in IgnoredFolders)
                    {
                        repoInfo.addIgnorePath(ignoredFolder.Path);
                    }
                    if(SupportedFeatures != null && SupportedFeatures.ChunkedSupport != null && SupportedFeatures.ChunkedSupport == true)
                        repoInfo.ChunkSize = ChunkSize;
                    else
                        repoInfo.ChunkSize = 0;
                    return repoInfo;
                }
            }
        }

        public class IgnoredFolder
        {
            [XmlAttribute("path")]
            public string Path { get; set; }
        }

        public class User {
            [XmlElement("name")]
            public string Name { get; set; }
            [XmlElement("email")]
            public string EMail { get; set; }
        }

        public class Feature {
            [XmlElement("getFolderTree", IsNullable=true)]
            public bool? GetFolderTreeSupport {get; set;}
            [XmlElement("getDescendants", IsNullable=true)]
            public bool? GetDescendantsSupport {get; set;}
            [XmlElement("getContentChanges", IsNullable=true)]
            public bool? GetContentChangesSupport {get; set;}
            [XmlElement("fileSystemWatcher", IsNullable=true)]
            public bool? FileSystemWatcherSupport {get; set;}
            [XmlElement("maxContentChanges", IsNullable=true)]
            public int? MaxNumberOfContentChanges {get; set;}
            [XmlElement("tunkedSupport", IsNullable=true)]
            public bool? ChunkedSupport {get;set;}
        }

        public class XmlUri : IXmlSerializable
        {
            private Uri _Value;

            public XmlUri() { }
            public XmlUri(Uri source) { _Value = source; }

            public static implicit operator Uri(XmlUri o)
            {
                return o == null ? null : o._Value;
            }

            public static implicit operator XmlUri(Uri o)
            {
                return o == null ? null : new XmlUri(o);
            }

            public System.Xml.Schema.XmlSchema GetSchema()
            {
                return null;
            }

            public void ReadXml(XmlReader reader)
            {
                _Value = new Uri(reader.ReadElementContentAsString());
            }

            public void WriteXml(XmlWriter writer)
            {
                writer.WriteValue(_Value.ToString());
            }
        }
    }
}
