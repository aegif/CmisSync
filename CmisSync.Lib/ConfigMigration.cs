using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace CmisSync.Lib.Sync
{
    // Migrate config.xml from past versions.
    public static class ConfigMigration
    {

        // Migrate from the config.xml format of CmisSync 0.3.9 to the current format, if necessary.
        public static void Migrate()
        {
            // Replace XML root element from <sparkleshare> to <CmisSync>
            ReplaceXMLRootElement();
        }

        private static void ReplaceXMLRootElement()
        {
            // If file does not exist yet, no need for migration.
            if( ! File.Exists(ConfigManager.CurrentConfigFile))
                return;

            // If log4net element is found, it means that the root element is already correct.
            XmlElement element = (XmlElement)ConfigManager.CurrentConfig.GetLog4NetConfig();
            if (element != null)
                return;
            
            // Replace root XML element from <sparkleshare> to <CmisSync>
            var fileContents = System.IO.File.ReadAllText(ConfigManager.CurrentConfigFile);

            fileContents = fileContents.Replace("<sparkleshare>", "<CmisSync>");
            fileContents = fileContents.Replace("</sparkleshare>", "</CmisSync>");

            System.IO.File.WriteAllText(ConfigManager.CurrentConfigFile, fileContents);
        }
    }
}
