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
        public void CorrectStateAfterConstrution() {
            SyncEventQueue queue = new SyncEventQueue(null);
            Assert.True(queue.IsStopped);
        }  

        [Test]
        public void EventlessStartStop() {
            SyncEventQueue queue = new SyncEventQueue(null);
            Logger.Info("starting and stopping");
            Assert.True(queue.IsStopped);
            queue.StartListener();
            WaitFor(queue, (q) => { return !q.IsStopped; } );
            Assert.False(queue.IsStopped);
            queue.StopListener();
            WaitFor(queue, (q) => { return q.IsStopped; } );
            Assert.True(queue.IsStopped);
            Logger.Info("stopping of initialized but stopped Listener");
            queue.StopListener();
            Assert.True(queue.IsStopped);
        }
        
        [Test]
        [ExpectedException( typeof( InvalidOperationException ) )]
        public void PreventRestart() {
            SyncEventQueue queue = new SyncEventQueue(null);
            queue.StartListener();
            WaitFor(queue, (q) => { return !q.IsStopped; } );
            queue.StopListener();
            WaitFor(queue, (q) => { return q.IsStopped; } );
            queue.StartListener();
        }

        [Test]
        [ExpectedException( typeof( InvalidOperationException ) )]
        public void PreventStopWithoutStart() {
            SyncEventQueue queue = new SyncEventQueue(null);
            queue.StopListener();
        }
    }
}
