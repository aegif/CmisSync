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

    [TestFixture]
    public class SyncEventManagerTest
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SyncEventManagerTest));

        [TestFixtureSetUp]
        public void ClassInit()
        {
            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());
        }


        [Test]
        public void AddHandlerTest() {
            var handlerMock = new Mock<SyncEventHandler>();
            var eventMock = new Mock<ISyncEvent>();

            SyncEventManager manager = new SyncEventManager();
            manager.AddEventHandler(handlerMock.Object);
            manager.Handle(eventMock.Object);

            handlerMock.Verify(foo => foo.Handle(eventMock.Object), Times.Once());
        }
    }
}
