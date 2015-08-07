using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using log4net;
using System.Text.RegularExpressions;
using CmisSync.Lib.Cmis;
using CmisSync.Auth;

namespace CmisSync.Lib.Sync
{
    /// <summary>
    /// Migrate config.xml from past versions.
    /// </summary>
    public static class ConfigMigration
    {
        /// <summary>
        /// Log.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ConfigMigration));

        /// <summary>
        /// Migrate from the config.xml format of CmisSync 0.3.9 to the current format, if necessary.
        /// </summary>
        public static void Migrate()
        {
            // If file does not exist yet, no need for migration.
            if (!File.Exists(ConfigManager.CurrentConfigFile))
                return;

            // Check config file version.
            int configSchemaVersion = ConfigManager.CurrentConfig.ConfigSchemaVersion;

            // Skip migration if up-to-date.
            if (configSchemaVersion == null || configSchemaVersion >= Config.SchemaVersion)
            {
                return;
            }

            // Migrate with various step according to the version.
            Logger.DebugFormat("Current config schema must be updated from {0} to {0}", configSchemaVersion, Config.SchemaVersion);
            switch (configSchemaVersion)
            {
                case 0:
                    // Replace uppercase notification boolean to lower case
                    ReplaceCaseSensitiveNotification();
                    // Replace XML root element from <sparkleshare> to <CmisSync>
                    ReplaceXMLRootElement();
                    CheckForDuplicatedLog4NetElement();
                    ReplaceTrunkByChunk();

                    ReplaceOldAlfrescoURLs();

                    // Update version number.
                    ConfigManager.CurrentConfig.ConfigSchemaVersion = Config.SchemaVersion;
                    ConfigManager.CurrentConfig.Save();
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unexpected config schema version: {0}.", configSchemaVersion));
            }
        }


        /// <summary>
        /// Replaces True by true in the notification to make it possible to deserialize
        /// Xml Config to C# Objects
        /// </summary>
        private static void ReplaceCaseSensitiveNotification()
        {
            var fileContents = System.IO.File.ReadAllText(ConfigManager.CurrentConfigFile);
            if (fileContents.Contains("<notifications>True</notifications>"))
            {
                fileContents = fileContents.Replace("<notifications>True</notifications>", "<notifications>true</notifications>");
                System.IO.File.WriteAllText(ConfigManager.CurrentConfigFile, fileContents);
                System.Console.Out.WriteLine("Migrated old upper case notification to lower case");
            }
        }


        private static void CheckForDuplicatedLog4NetElement()
        {
            XmlElement log4net = ConfigManager.CurrentConfig.GetLog4NetConfig();
            if (log4net.ChildNodes.Item(0).Name.Equals("log4net"))
            {
                ConfigManager.CurrentConfig.SetLog4NetConfig(log4net.ChildNodes.Item(0));
                ConfigManager.CurrentConfig.Save();
            }
        }


        private static void ReplaceTrunkByChunk()
        {
            var fileContents = System.IO.File.ReadAllText(ConfigManager.CurrentConfigFile);
            if (fileContents.Contains("<trunkSize>") || fileContents.Contains("</trunkSize>") )
            {
                fileContents = fileContents.Replace("<trunkSize>", "<chunkSize>");
                fileContents = fileContents.Replace("</trunkSize>", "</chunkSize>");
                System.IO.File.WriteAllText(ConfigManager.CurrentConfigFile, fileContents);
                System.Console.Out.WriteLine("Migrated old trunkSize to chunkSize");
            }
        }


        /// <summary>
        /// Replace XML root element name from sparkleshare to CmisSync
        /// </summary>
        private static void ReplaceXMLRootElement()
        {
            try
            {
                // If log4net element is found, it means that the root element is already correct.
                XmlElement element = ConfigManager.CurrentConfig.GetLog4NetConfig();
                if (element != null)
                    return;
            }
            catch (Exception)
            {
                // Replace root XML element from <sparkleshare> to <CmisSync>
                var fileContents = System.IO.File.ReadAllText(ConfigManager.CurrentConfigFile);
                fileContents = fileContents.Replace("<sparkleshare>", "<CmisSync>");
                fileContents = fileContents.Replace("</sparkleshare>", "</CmisSync>");

                System.IO.File.WriteAllText(ConfigManager.CurrentConfigFile, fileContents);
            }
        }


        private static void ReplaceOldAlfrescoURLs()
        {
            bool modified = false;

            // Loop through all repositories.
            foreach (CmisSync.Lib.Config.SyncConfig.Folder folder in ConfigManager.CurrentConfig.Folders)
            {
                string oldUrl = folder.RemoteUrl.ToString();

                // Replace old pattern from Alfresco 4.0 and 4.1
                if (oldUrl.EndsWith("/alfresco/cmisatom"))
                {
                    string newUrl = oldUrl.Replace("/alfresco/cmisatom", "/alfresco/api/-default-/public/cmis/versions/1.1/atom");

                    if (IsAlfresco42OrLater(folder, newUrl))
                    {
                        folder.RemoteUrl = new CmisSync.Lib.Config.XmlUri(new Uri(newUrl));
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                ConfigManager.CurrentConfig.Save();
            }
        }

        private static bool IsAlfresco42OrLater(CmisSync.Lib.Config.SyncConfig.Folder folder, string newUrl)
        {
            try
            {
                CmisUtils.GetSubfolders(
                    folder.RepositoryId,
                    folder.RemotePath,
                    newUrl,
                    folder.UserName,
                    Crypto.Deobfuscate(folder.ObfuscatedPassword));

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}