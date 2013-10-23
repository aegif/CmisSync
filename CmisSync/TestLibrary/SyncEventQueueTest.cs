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
            for(int i = 0; i < 5; i++){
                if (check(obj)) {
                    break;
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
            //stoping of not initialized Listener
            queue.StopListener();
            //starting and stopping
            Assert.True(queue.IsStopped);
            queue.StartListener();
            WaitFor(queue, (q) => { return !q.IsStopped; } );
            Assert.False(queue.IsStopped);
            queue.StopListener();
            WaitFor(queue, (q) => { return q.IsStopped; } );
            Assert.True(queue.IsStopped);
            //restartable?
            queue.StartListener();
            Assert.False(queue.IsStopped);
            queue.StopListener();
            WaitFor(queue, (q) => { return q.IsStopped; } );
            Assert.True(queue.IsStopped);
            //stopping of initialized but stopped Listener
            queue.StopListener();
            Assert.True(queue.IsStopped);
        }

    }
}
