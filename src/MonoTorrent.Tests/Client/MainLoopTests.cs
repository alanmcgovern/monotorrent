using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using MonoTorrent.Client;
using System.Threading;

namespace MonoTorrent.Client
{
    
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

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            loop = new MainLoop("Test Loop");
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            //loop.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            count = 0;
        }

        [Fact]
        public void TaskTest()
        {
            Assert.Equal(5, loop.QueueWait((MainLoopJob) delegate { return 5; }));

            ManualResetEvent handle = new ManualResetEvent(false);
            loop.QueueWait((MainLoopTask)delegate { handle.Set(); });
            Assert.True(handle.WaitOne(5000, true));
        }

        [Fact]
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
            Assert.True(handle.WaitOne(5000, true), "#1: Executed {0} times", count);
            Assert.Equal(3, count);
        }

        [Fact]
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
            Assert.True(handle.WaitOne(5000, false), "#1: Executed {0} times", count);
            Assert.Equal(3, count);
        }
    }
}
