using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CmisSync.Lib
{
    /// <summary>
    /// A static class that allows easy access to the configuration of CmisSync.
    /// </summary>
    public static class ConfigManager
    {
        /// <summary>
        /// The CmisSync configuration.
        /// Following the singleton design pattern.
        /// </summary>
        private static Config config;


        /// <summary>
        /// The CmisSync configuration.
        /// Following the singleton design pattern.
        /// </summary>
        public static Config CurrentConfig
        {
            get
            {
                // Load the configuration if it has not been done yet.
                // If no configuration file exists, it will create a default one.
                if (config == null)
                {
                    config = new Config(CurrentConfigFile);
                }

                // return the loaded configuration.
                return config;
            }
        }


        /// <summary>
        /// Get the filesystem path to the XML configuration file.
        /// </summary>
        public static string CurrentConfigFile
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cmissync", "config.xml");
            }
        }


        public static string GetFullPath(string name)
        {
                string custom_path = ConfigManager.CurrentConfig.GetFolderAttribute(name, "path");
                // if (String.IsNullOrEmpty(custom_path)) custom_path = Config.DefaultConfig.FoldersPath;

                if (custom_path != null)
                    return custom_path;
                else
                    return Path.Combine(ConfigManager.CurrentConfig.FoldersPath, name);
                // return Path.Combine(ROOT_FOLDER, Name);
            }
    }
}