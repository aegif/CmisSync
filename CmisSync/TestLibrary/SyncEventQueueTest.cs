using log4net;
using log4net.Config;

using System;
using System.Threading.Tasks;
using System.Threading;
namespace TestLibrary
{
    using NUnit.Framework;
    using CmisSync.Lib;
    using CmisSync.Lib.Events;

    [TestFixture]
    public class SyncEventQueueTest
    {
        private class DummyEvent : ISyncEvent 
        {
            public bool called;

            public SyncEventType GetType() {
                return SyncEventType.FileSystem;
            }
        }
        
        private class DummyHandler : ISyncEventHandler
        {
            public bool called;

            public bool Handle(ISyncEvent e){
                called = true;
                return true;
            }

            public int GetPriority() {
                return 5;
            }
        }

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
            using(SyncEventQueue queue = new SyncEventQueue(new SyncEventManager())){
                WaitFor(queue, (q) => { return !q.IsStopped; } );
                Assert.False(queue.IsStopped);
                queue.StopListener();
                WaitFor(queue, (q) => { return q.IsStopped; } );
                Assert.True(queue.IsStopped);
            }
        }

        [Test]
        public void AddEvent(){
            SyncEventManager manager = new SyncEventManager();
            DummyHandler handler = new DummyHandler();
            manager.AddEventHandler(handler);
            using(SyncEventQueue queue = new SyncEventQueue(manager)){
                queue.AddEvent(new DummyEvent());
                queue.StopListener();
                WaitFor(queue, (q) => { return q.IsStopped; } );
                Assert.True(queue.IsStopped);
            }
            Assert.True(handler.called);
        }
        
    }
}
