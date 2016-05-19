using System;
using System.Threading;
using Xunit;

namespace MonoTorrent.Client
{
    public class MainLoopTests : IDisposable
    {
        public MainLoopTests()
        {
            loop = new MainLoop("Test Loop");
            count = 0;
        }

        public void Dispose()
        {
            //loop.Dispose();
        }

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
        private readonly MainLoop loop;

        [Fact]
        public void LongRunningTask()
        {
            var handle = new ManualResetEvent(false);
            loop.QueueTimeout(TimeSpan.FromMilliseconds(10), delegate
            {
                Thread.Sleep(50);
                if (++count == 3)
                {
                    handle.Set();
                    return false;
                }

                return true;
            });
            Assert.True(handle.WaitOne(5000, false));
            Assert.Equal(3, count);
        }

        [Fact]
        public void RepeatedTask()
        {
            //Console.WriteLine("Starting");
            var handle = new ManualResetEvent(false);
            loop.QueueTimeout(TimeSpan.FromMilliseconds(0), delegate
            {
                count++;
                if (count == 3)
                {
                    handle.Set();
                    return false;
                }

                return true;
            });
            Assert.True(handle.WaitOne(5000, true));
            Assert.Equal(3, count);
        }

        [Fact]
        public void TaskTest()
        {
            Assert.Equal(5, loop.QueueWait(delegate { return 5; }));

            var handle = new ManualResetEvent(false);
            loop.QueueWait(delegate { handle.Set(); });
            Assert.True(handle.WaitOne(5000, true));
        }
    }
}