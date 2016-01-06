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
using log4net;
using CmisSync.Auth;

namespace CmisSync.Lib
{
    /// <summary>
    /// Configuration of a CmisSync synchronized folder.
    /// It can be found in the XML configuration file.
    /// </summary>
    public class Config
    {

        public static readonly string DEFAULT_URL_ADRESS = "https://cloud.nemakiware.com/core/atom";

        /// <summary>
        /// The current config schema version.
        /// </summary>
        public const int SchemaVersion = 1;


        /// <summary>
        /// Chunk size for chunked transfers (not implemented yet)
        /// </summary>
        private const long DefaultChunkSize = 1024 * 1024;


        /// <summary>
        /// Default poll interval.
        /// It is used for any newly created synchronized folder.
        /// In milliseconds.
        /// </summary>
        public static readonly int DEFAULT_POLL_INTERVAL = 5 * 1000; // 3 miniutes.


        /// <summary>
        /// Log.
        /// </summary>
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(Config));


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

        // XML elements.

        /// <summary>
        /// Config schema version.
        /// </summary>
        public int ConfigSchemaVersion { get { return configXml.ConfigSchemaVersion; } set { configXml.ConfigSchemaVersion = value; } }

        /// <summary>
        /// Notifications.
        /// </summary>
        public bool Notifications { get { return configXml.Notifications; } set { configXml.Notifications = value; } }

        /// <summary>
        /// Single Repository Only.
        /// </summary>
        public bool SingleRepository { get { return configXml.SingleRepository; } set { configXml.SingleRepository = value; } }

        /// <summary>
        /// Frozen configuration: The configuration can not be modified from the UI
        /// </summary>
        public bool FrozenConfiguration { get { return configXml.FrozenConfiguration; } set { configXml.FrozenConfiguration = value; } }

        /// <summary>
        /// Folder.
        /// </summary>
        public List<SyncConfig.Folder> Folders { get { return configXml.Folders; } }

        /// <summary>
        /// Get folder based on name.
        /// </summary>
        public SyncConfig.Folder GetFolder(string name)
        {
            foreach (SyncConfig.Folder folder in configXml.Folders)
            {
                if( folder.DisplayName.Equals(name))
                    return folder;
            }
            return null;
        }

        /// <summary>
        /// Get a folder based on RemoteUrl, UserName, and RepositoryId.
        /// </summary>
        public SyncConfig.Folder GetFolder(string RemoteUrl, string UserName, string RepositoryId)
        {
            foreach (SyncConfig.Folder folder in configXml.Folders)
            {
                Uri RemoteUri = folder.RemoteUrl;
                if (RemoteUri.ToString().Equals(RemoteUrl) &&
                    folder.UserName.Equals(UserName) &&
                    folder.RepositoryId.Equals(RepositoryId))
                {
                    return folder;
                }
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
                return Path.Combine(HomePath, "ASaaSDrive");
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
                CreateInitialConfigFile();

            // Load the XML configuration.
            try
            {
                Load();
            }
            catch (TypeInitializationException)
            {
                CreateInitialConfigFile();
            }
            catch (FileNotFoundException)
            {
                CreateInitialConfigFile();
            }
            catch (XmlException)
            {
                FileInfo file = new FileInfo(FullPath);

                // If the XML configuration file exists but with file size zero, then recreate it.
                if (file.Length == 0)
                {
                    File.Delete(FullPath);
                    CreateInitialConfigFile();
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
        /// Create the initial XML configuration file.
        /// </summary>
        private void CreateInitialConfigFile()
        {
            CreateConfigFromScratch();

            // Save it as an XML file.
            Save();
        }


        /// <summary>
        /// Create an initial XML configuration with default settings and zero remote folders.
        /// </summary>
        private void CreateConfigFromScratch()
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
                ConfigSchemaVersion = Config.SchemaVersion,
                Notifications = true,
                SingleRepository = false, // Multiple repository for CmisSync, but some CmisSync-derived products have different defaults.
                FrozenConfiguration = false,
                Log4Net = createDefaultLog4NetElement(),
                Folders = new List<SyncConfig.Folder>(),
                User = new User()
                {
                    EMail = "Unknown",
                    Name = userName
                }
            };
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
                IsSuspended = repoInfo.IsSuspended,
                SyncAtStartup = repoInfo.SyncAtStartup,
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
        /// Remove a synchronized folder from the configuration.
        /// </summary>
        public void RemoveFolder(string repoName)
        {
            this.configXml.Folders.Remove(GetFolder(repoName));
            Logger.Info("Removed sync config: " + repoName);
            Save();
        }


        /// <summary>
        /// Get the configured path to the log file.
        /// </summary>
        public string GetLogFilePath()
        {
            return Path.Combine(ConfigPath, "debug_log.txt");
        }

        private string GetLogLevel()
        {
#if (DEBUG)
            return "DEBUG";
#else
            return "INFO";
#endif
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
      <maxSizeRollBackups value=""5"" />
      <maximumFileSize value=""1MB"" />
      <staticLogFileName value=""true"" />
      <layout type=""log4net.Layout.PatternLayout"">
        <conversionPattern value=""%date [%thread] %-5level %logger [%property{NDC}] - %message%newline"" />
      </layout>
    </appender>
    <root>
      <level value=""" + GetLogLevel() + @""" />
      <appender-ref ref=""CmisSyncFileAppender"" />
    </root>
  </log4net>"))
            {
                XmlElement result = (XmlElement)deserializer.Deserialize(textReader);
                return result;
            }
        }

        /// <summary>
        /// Sync configuration.
        /// </summary>
        [XmlRoot("CmisSync", Namespace = null)]
        public class SyncConfig {

            /// <summary>
            /// Config schema version.
            /// </summary>
            [XmlElement("configSchemaVersion")]
            public Int32 ConfigSchemaVersion { get; set; }
            
            /// <summary>
            /// Notifications.
            /// </summary>
            [XmlElement("notifications")]
            public Boolean Notifications { get; set; }
            
            /// <summary>
            /// Single repository.
            /// </summary>
            [XmlElement("singleRepository")]
            public Boolean SingleRepository { get; set; }
            
            /// <summary>
            /// Frozen configuration: The configuration can not be modified from the UI.
            /// </summary>
            [XmlElement("frozenConfiguration")]
            public Boolean FrozenConfiguration { get; set; }
            
            /// <summary>
            /// Logging config.
            /// </summary>
            [XmlAnyElement("log4net")]
            public XmlNode Log4Net { get; set; }
            
            /// <summary>
            /// List of the CmisSync synchronized folders.
            /// </summary>
            [XmlArray("folders")]
            [XmlArrayItem("folder")]
            public List<SyncConfig.Folder> Folders { get; set; }
            
            /// <summary>
            /// User.
            /// </summary>
            [XmlElement("user", typeof(User))]
            public User User { get; set; }

            /// <summary>
            /// Folder definition.
            /// </summary>
            public class Folder
            {

                /// <summary>
                /// Name.
                /// </summary>
                [XmlElement("name")]
                public string DisplayName { get; set; }

                /// <summary>
                /// Path.
                /// </summary>
                [XmlElement("path")]
                public string LocalPath { get; set; }

                /// <summary>
                /// URL.
                /// </summary>
                [XmlElement("url")]
                public XmlUri RemoteUrl { get; set; }

                /// <summary>
                /// Repository ID.
                /// </summary>
                [XmlElement("repository")]
                public string RepositoryId { get; set; }

                /// <summary>
                /// Remote path.
                /// </summary>
                [XmlElement("remoteFolder")]
                public string RemotePath { get; set; }

                /// <summary>
                /// Username.
                /// </summary>
                [XmlElement("user")]
                public string UserName { get; set; }

                /// <summary>
                /// Password.
                /// </summary>
                [XmlElement("password")]
                public string ObfuscatedPassword { get; set; }

                /// <summary>
                /// IsSuspended
                /// </summary>
                [XmlElement("issuspended")]
                public bool IsSuspended { get; set; }

                /// <summary></summary>
                [XmlElement("syncatstartup")]
                public bool SyncAtStartup { get; set; }

                private double pollInterval = DEFAULT_POLL_INTERVAL;

                /// <summary>
                /// Poll interval.
                /// </summary>
                [XmlElement("pollinterval")]
                public double PollInterval {
                    get { return pollInterval; }
                    set {
                        if (value <= 0)
                        {
                            Logger.Warn("Poll interval value is invalid, "
                                + "using default poll interval: " + DEFAULT_POLL_INTERVAL);
                            pollInterval = DEFAULT_POLL_INTERVAL;
                        }
                        else
                        {
                            pollInterval = value;
                        }
                    }
                }

                private int uploadRetries = 2;

                /// <summary></summary>
                [XmlElement("maxUploadRetries", IsNullable=true)]
                public int? UploadRetries
                {
                    get { return uploadRetries; }
                    set {
                        if( value==null || value < 0 )
                            uploadRetries = 2;
                        else
                            uploadRetries = (int) value;
                    }
                }

                private int downloadRetries = 2;

                /// <summary></summary>
                [XmlElement("maxDownloadRetries", IsNullable=true)]
                public int? DownLoadRetries
                {
                    get { return downloadRetries; }
                    set {
                        if( value == null || value < 0 )
                            downloadRetries = 2;
                        else
                            downloadRetries = (int) value;
                    }
                }

                private int deletionRetries = 2;

                /// <summary></summary>
                [XmlElement("maxDeletionRetries", IsNullable=true)]
                public int? DeletionRetries
                {
                    get { return deletionRetries; }
                    set {
                        if ( value == null || value < 0 )
                            deletionRetries = 2;
                        else
                            deletionRetries = (int) value;
                    }
                }

                /// <summary></summary>
                [XmlElement("features", IsNullable=true)]
                public Feature SupportedFeatures { get; set;}

                /// <summary>
                /// Ignored folders.
                /// </summary>
                [XmlElement("ignoreFolder", IsNullable = true)]
                public List<IgnoredFolder> IgnoredFolders { get; set; }

                private long chunkSize = DefaultChunkSize;

                /// <summary></summary>
                [XmlElement("chunkSize"), System.ComponentModel.DefaultValue(DefaultChunkSize)]
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
                    repoInfo.Password = new Password();
                    repoInfo.Password.ObfuscatedPassword = ObfuscatedPassword;
                    repoInfo.Address = RemoteUrl;
                    repoInfo.RepoID = RepositoryId;
                    repoInfo.RemotePath = RemotePath;
                    repoInfo.TargetDirectory = LocalPath;
                    repoInfo.MaxUploadRetries = uploadRetries;
                    repoInfo.MaxDownloadRetries = downloadRetries;
                    repoInfo.MaxDeletionRetries = deletionRetries;
                    if (PollInterval < 1) PollInterval = Config.DEFAULT_POLL_INTERVAL;
                    repoInfo.PollInterval = PollInterval;
                    repoInfo.IsSuspended = IsSuspended;
                    repoInfo.SyncAtStartup = SyncAtStartup;

                    foreach (IgnoredFolder ignoredFolder in IgnoredFolders)
                    {
                        repoInfo.addIgnorePath(ignoredFolder.Path);
                    }

                    if(SupportedFeatures != null && SupportedFeatures.ChunkedSupport != null && SupportedFeatures.ChunkedSupport == true)
                    {
                        repoInfo.ChunkSize = ChunkSize;
                        repoInfo.DownloadChunkSize = ChunkSize;
                    }
                    else
                    {
                        repoInfo.ChunkSize = 0;
                        repoInfo.DownloadChunkSize = 0;
                    }
                    if(SupportedFeatures != null && SupportedFeatures.ChunkedDownloadSupport!=null && SupportedFeatures.ChunkedDownloadSupport == true)
                        repoInfo.DownloadChunkSize = ChunkSize;

                    return repoInfo;
                }
            }
        }

        /// <summary>
        /// Ignored folder.
        /// </summary>
        public class IgnoredFolder
        {
            /// <summary>
            /// Folder path.
            /// </summary>
            [XmlAttribute("path")]
            public string Path { get; set; }
        }

        /// <summary>
        /// User details.
        /// </summary>
        public class User
        {
            /// <summary>
            /// Name.
            /// </summary>
            [XmlElement("name")]
            public string Name { get; set; }
            /// <summary>
            /// Email.
            /// </summary>
            [XmlElement("email")]
            public string EMail { get; set; }
        }

        /// <summary></summary>
        public class Feature {
            /// <summary></summary>
            [XmlElement("getFolderTree", IsNullable=true)]
            public bool? GetFolderTreeSupport {get; set;}
            /// <summary></summary>
            [XmlElement("getDescendants", IsNullable=true)]
            public bool? GetDescendantsSupport {get; set;}
            /// <summary></summary>
            [XmlElement("getContentChanges", IsNullable=true)]
            public bool? GetContentChangesSupport {get; set;}
            /// <summary></summary>
            [XmlElement("fileSystemWatcher", IsNullable=true)]
            public bool? FileSystemWatcherSupport {get; set;}
            /// <summary></summary>
            [XmlElement("maxContentChanges", IsNullable=true)]
            public int? MaxNumberOfContentChanges {get; set;}
            /// <summary></summary>
            [XmlElement("chunkedSupport", IsNullable=true)]
            public bool? ChunkedSupport {get;set;}
            /// <summary></summary>
            [XmlElement("chunkedDownloadSupport", IsNullable=true)]
            public bool? ChunkedDownloadSupport {get;set;}
        }

        /// <summary>
        /// XML URI.
        /// </summary>
        public class XmlUri : IXmlSerializable
        {
            private Uri _Value;

            /// <summary>
            /// Constructor.
            /// </summary>
            public XmlUri() { }

            /// <summary>
            /// Constructor.
            /// </summary>
            public XmlUri(Uri source) { _Value = source; }

            /// <summary>
            /// implicit.
            /// </summary>
            public static implicit operator Uri(XmlUri o)
            {
                return o == null ? null : o._Value;
            }

            /// <summary>
            /// implicit.
            /// </summary>
            public static implicit operator XmlUri(Uri o)
            {
                return o == null ? null : new XmlUri(o);
            }

            /// <summary>
            /// Get schema.
            /// </summary>
            public System.Xml.Schema.XmlSchema GetSchema()
            {
                return null;
            }

            /// <summary>
            /// Read XML.
            /// </summary>
            public void ReadXml(XmlReader reader)
            {
                _Value = new Uri(reader.ReadElementContentAsString());
            }

            /// <summary>
            /// Write XML.
            /// </summary>
            public void WriteXml(XmlWriter writer)
            {
                writer.WriteValue(_Value.ToString());
            }

            /// <summary>
            /// String representation of the URI.
            /// </summary>
            public string ToString()
            {
                return _Value.ToString();
            }
        }
    }
}
