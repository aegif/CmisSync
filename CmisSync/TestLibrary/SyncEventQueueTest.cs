using log4net;
using log4net.Config;

using System;
using System.Threading.Tasks;
using System.Threading;
namespace TestLibrary
{
    using NUnit.Framework;
    using Moq;
    using CmisSync.Lib;
    using CmisSync.Lib.Events;

    /// <summary></summary>
    [TestFixture]
    public class SyncEventQueueTest
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SyncEventQueueTest));

        [TestFixtureSetUp]
        public void ClassInit()
        {
            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());
        }

        private static void WaitFor<T>(T obj, Func<T,bool> check){
            for(int i = 0; i < 50; i++){
                if (check(obj)) {
                    return;
                }
                Thread.Sleep(100);
            }
            Logger.Error("Timeout exceeded!");
        }

        [Test]
        public void EventlessStartStop() {
            using(SyncEventQueue queue = new SyncEventQueue(new Mock<SyncEventManager>().Object)){
                WaitFor(queue, (q) => { return !q.IsStopped; } );
                Assert.False(queue.IsStopped);
                queue.StopListener();
                WaitFor(queue, (q) => { return q.IsStopped; } );
                Assert.True(queue.IsStopped);
            }
        }

        [Test]
        public void AddEvent() {
            var managerMock = new Mock<SyncEventManager>();
            var eventMock = new Mock<ISyncEvent>();
            using(SyncEventQueue queue = new SyncEventQueue(managerMock.Object)){
                queue.AddEvent(eventMock.Object);
                queue.AddEvent(eventMock.Object);
                queue.StopListener();
                WaitFor(queue, (q) => { return q.IsStopped; } );
                Assert.True(queue.IsStopped);
            }
            managerMock.Verify(foo => foo.Handle(eventMock.Object), Times.Exactly(2));
        }

        [Test]
        [ExpectedException( typeof( InvalidOperationException ) )]
        public void AddEventToStoppedQueue() {
            using(SyncEventQueue queue = new SyncEventQueue(new Mock<SyncEventManager>().Object)){
                queue.StopListener();
                WaitFor(queue, (q) => { return q.IsStopped; } );
                queue.AddEvent(new Mock<ISyncEvent>().Object);
            }
        }
        
    }
}
