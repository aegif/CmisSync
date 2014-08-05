using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace CmisSync.Lib.Sync
{
    /// <summary>
    /// Migrate config.xml from past versions.
    /// </summary>
    public static class ConfigMigration
    {
        /// <summary>
        /// Migrate from the config.xml format of CmisSync 0.3.9 to the current format, if necessary.
        /// </summary>
        public static void Migrate()
        {
            // If file does not exist yet, no need for migration.
            if (!File.Exists(ConfigManager.CurrentConfigFile))
                return;
            // Replace uppercase notification boolean to lower case
            ReplaceCaseSensitiveNotification();
            // Replace XML root element from <sparkleshare> to <CmisSync>
            ReplaceXMLRootElement();
            CheckForDuplicatedLog4NetElement();
            ReplaceTrunkByChunk();
            CheckForDatabaseSchema();
        }

        private static void CheckForDatabaseSchema()
        {
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
    }
}
