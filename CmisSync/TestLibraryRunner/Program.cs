using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestLibrary;
using Newtonsoft.Json;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;

using CmisSync.Lib;
using CmisSync.Lib.Sync;

using log4net;
using log4net.Config;

// Useful to debug unit tests.
namespace TestLibraryRunner
{
    class TrustAlways : ICertificatePolicy
    {
        public bool CheckValidationResult (ServicePoint sp, X509Certificate certificate, WebRequest request, int error)
        {
            // For testing, always accept any certificate
            return true;
        }

    }

    class Program
    {

        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
        static int serverId = 0; // Which server in the JSON file (first=0)

        static void test(string path)
        {
            IEnumerable<object[]> servers = JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText(path));
            object[] server = servers.ElementAt(serverId);

            new CmisSyncTests().SyncWhileModifyingFile((string)server[0], (string)server[1],
                (string)server[2], (string)server[3], (string)server[4], (string)server[5], (string)server[6]);
        }

        static void testFuzzy()
        {
            IEnumerable<object[]> servers = JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText("../../../TestLibrary/test-servers-fuzzy.json"));
            object[] server = servers.ElementAt(serverId);
            new CmisSyncTests().GetRepositoriesFuzzy((string)server[0], (string)server[1], (string)server[2]);
        }

        static void Main(string[] args)
        {
            ServicePointManager.CertificatePolicy = new TrustAlways();
            bool firstRun = ! File.Exists(ConfigManager.CurrentConfigFile);

            // Migrate config.xml from past versions, if necessary.
            if ( ! firstRun )
                ConfigMigration.Migrate();

            // Clear log file.
            File.Delete(ConfigManager.CurrentConfig.GetLogFilePath());

            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());
            Logger.Info("Starting.");
            string path = null;

            foreach (string arg in args)
            {
                 if (File.Exists(arg))
                 {
                     path = arg;
                     break;
                 }
            }

            test(path == null ? "../../../TestLibrary/test-servers.json" : path);
            //testFuzzy();

            // Removed Console read - This should be handled by the caller. Otherwise
            // tests cannot be run in an automated environment (Continuous Integration).
        }
    }
}
