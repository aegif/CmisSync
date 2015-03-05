using CmisSync.Lib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TestLibrary
{
    [TestFixture]
    class ConfigurationTest
    {

        [Test, Category("Slow")]
        public void TestConfig()
        {
            string configpath = Path.GetFullPath("testconfig.conf");
            try
            {
                //Create new config file with default values
                Config config = new Config(configpath);
                //Notifications should be switched on by default
                Assert.IsTrue(config.Notifications);
                Assert.AreEqual(config.Folders.Count, 0);
                config.Save();
                config = new Config(configpath);
            }
            catch (Exception)
            {
                if (File.Exists(configpath))
                    File.Delete(configpath);
                throw;
            }
            File.Delete(configpath);
        }
    }
}
