using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client;
using System.Threading;

namespace MonoTorrent.Client.Tests
{
    [TestFixture]
    public class MainLoopTests
    {
        //static void Main(string[] args)
        //{
        //    MainLoopTests t = new MainLoopTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.TaskTest();
        //    for (int i = 0; i < 1000; i++)
        //    {
        //        t.Setup();
        //        t.RepeatedTask();
        //    }
        //    t.Setup();
        //    t.LongRunningTask();
        //}
        private int count;
        MainLoop loop;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            loop = new MainLoop("Test Loop");
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            loop.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            count = 0;
        }

        [Test]
        public void TaskTest()
        {
            Assert.AreEqual(5, loop.QueueWait(delegate { return 5; }), "#1");

            ManualResetEvent handle = new ManualResetEvent(false);
            loop.QueueWait(delegate { handle.Set(); });
            Assert.IsTrue(handle.WaitOne(5000, true), "#2");
        }

        [Test]
        public void RepeatedTask()
        {
            //Console.WriteLine("Starting");
            ManualResetEvent handle = new ManualResetEvent(false);
            loop.QueueTimeout(TimeSpan.FromMilliseconds(0), delegate {
                this.count++;
                if (count == 3)
                {
                    handle.Set();
                    return false;
                }

                return true;
            });
            Assert.IsTrue(handle.WaitOne(5000, true), "#1: Executed {0} times", count);
            Assert.AreEqual(3, count, "#2");
        }

        [Test]
        public void LongRunningTask()
        {
            ManualResetEvent handle = new ManualResetEvent(false);
            loop.QueueTimeout(TimeSpan.FromMilliseconds(10), delegate {
                System.Threading.Thread.Sleep(50);
                if (++count == 3)
                {
                    handle.Set();
                    return false;
                }

                return true;
            });
            Assert.IsTrue(handle.WaitOne(5000, false), "#1: Executed {0} times", count);
            Assert.AreEqual(3, count, "#2");
        }
    }
}
