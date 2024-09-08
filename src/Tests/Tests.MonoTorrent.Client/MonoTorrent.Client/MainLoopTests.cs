using System;
using System.Threading;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class MainLoopTests
    {
        private int count;
        MainLoop loop;

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            loop = new MainLoop ("Test Loop");
        }

        [OneTimeTearDown]
        public void FixtureTeardown ()
        {
            //loop.Dispose();
        }

        [SetUp]
        public void Setup ()
        {
            count = 0;
        }

        [Test]
        public void RepeatedTask ()
        {
            //Console.WriteLine("Starting");
            ManualResetEvent handle = new ManualResetEvent (false);
            loop.QueueTimeout (TimeSpan.FromMilliseconds (0), delegate {
                this.count++;
                if (count == 3) {
                    handle.Set ();
                    return false;
                }

                return true;
            });
            Assert.IsTrue (handle.WaitOne (5000, true), "#1: Executed {0} times", count);
            Assert.AreEqual (3, count, "#2");
        }

        [Test]
        public void LongRunningTask ()
        {
            ManualResetEvent handle = new ManualResetEvent (false);
            loop.QueueTimeout (TimeSpan.FromMilliseconds (10), delegate {
                Thread.Sleep (50);
                if (++count == 3) {
                    handle.Set ();
                    return false;
                }

                return true;
            });
            Assert.IsTrue (handle.WaitOne (5000, false), "#1: Executed {0} times", count);
            Assert.AreEqual (3, count, "#2");
        }
    }
}
