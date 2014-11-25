using log4net;
using log4net.Config;

using System;
using System.IO;
namespace TestLibrary
{
    using NUnit.Framework;
    using CmisSync.Lib;
    using CmisSync.Lib.Events;

    /// <summary></summary>
    [TestFixture]
    public class DebugLoggingHandlerTest
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(DebugLoggingHandlerTest));

        [TestFixtureSetUp]
        public void ClassInit()
        {
            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());
        }

        [Test]
        public void ToStringTest() {
            var handler = new DebugLoggingHandler();
            Assert.AreEqual("CmisSync.Lib.Events.DebugLoggingHandler with Priority 10000", handler.ToString());
        }
        
        [Test]
        public void PriorityTest() {
            var handler = new DebugLoggingHandler();
            Assert.AreEqual(10000, handler.Priority);
        }
        
    }
}
