using log4net;
using log4net.Config;

using System;
using System.IO;
namespace TestLibrary
{
    using NUnit.Framework;
    using Moq;
    using CmisSync.Lib;
    using CmisSync.Lib.Events;
    using CmisSync.Lib.Cmis;
    using DotCMIS.Client;
    using CmisSync.Lib.Database;

    /// <summary></summary>
    [TestFixture]
    public class FSDeletionHandlerTest
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(FSDeletionHandlerTest));

        [TestFixtureSetUp]
        public void ClassInit()
        {
            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());
        }

        [Test]
        public void ToStringTest() {
            var handler = new FSDeletionHandler(new Mock<Database>().Object, new Mock<ISession>().Object);
            Assert.AreEqual("CmisSync.Lib.Events.FSDeletionHandler with Priority 100", handler.ToString());
        }
        
        [Test]
        public void PriorityTest() {
            var handler = new FSDeletionHandler(new Mock<Database>().Object, new Mock<ISession>().Object);
            Assert.AreEqual(100, handler.Priority); 
        }
        
        [Test]
        public void IgnoresNonFSEvent() {
            var handler = new FSDeletionHandler(new Mock<Database>().Object, new Mock<ISession>().Object);
            bool handled = handler.Handle(new Mock<ISyncEvent>().Object);
            Assert.False(handled);            
        }

        [Test]
        public void IgnoresFSNonDeleteEvent() {
            var handler = new FSDeletionHandler(new Mock<Database>().Object, new Mock<ISession>().Object);
            bool handled = handler.Handle(new Mock<FSEvent>(WatcherChangeTypes.Created, "").Object);
            Assert.False(handled);            
        }
    }
}
