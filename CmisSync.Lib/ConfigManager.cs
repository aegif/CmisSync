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
        /// Lock to provide threadsafe singleton creation
        /// </summary>
        private static Object configlock = new Object();

        /// <summary>
        /// Path to a custom configuration file, specified from command line.
        /// </summary>
        private static string customConfigFile;

        /// <summary>
        /// The CmisSync configuration.
        /// Following the singleton design pattern.
        /// </summary>
        public static Config CurrentConfig
        {
            get
            {
                if (config == null)
                {
                    lock (configlock)
                    {
                        // Load the configuration if it has not been done yet.
                        // If no configuration file exists, it will create a default one.
                        if (config == null)
                        {
                            config = new Config(CurrentConfigFile);
                        }
                    }
                }
                // return the loaded configuration.
                return config;
            }
        }


        /// <summary>
        /// Default path to use when creating "configuration" files such as config.xml, databases, and logs.
        /// Please note that some CmisSync customized versions store their "configuration" in a different path,
        /// so only use this method when no configuration exist, and never to retrieve the path of existing configuration.
        /// </summary>
        public static string DefaultConfigPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cmissync");
        }

        
        /// <summary>
        /// Get the filesystem path to the XML configuration file.
        /// </summary>
        public static string CurrentConfigFile
        {
            get
            {
                if (customConfigFile != null)
                {
                    return customConfigFile;
                }
                else
                {
                    return Path.Combine(DefaultConfigPath(), "config.xml");
                }
            }

            set
            {
                customConfigFile = value;
            }
        }
    }
}
