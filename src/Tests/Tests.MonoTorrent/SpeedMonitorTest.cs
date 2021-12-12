using System;

using NUnit.Framework;

namespace MonoTorrent
{
    [TestFixture]
    public class SpeedMonitorTest
    {
        [Test]
        public void InvalidConstructor ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => new SpeedMonitor (-1));
        }

        [Test]
        public void VeryFastRate ()
        {
            // We're reporting 500MB every 750ms period
            var speed = 500 * 1024 * 1024;

            // The actual speed averaged over a 1 second period would be this
            var estimatedActualSpeed = speed * 4 / 3;

            var monitor = new SpeedMonitor ();
            for (int i = 0; i < 37; i++) {
                monitor.AddDelta (speed);
                monitor.Tick (750);

                Assert.Greater (monitor.Rate, 0, "#1." + i);
                Assert.Less (monitor.Rate, estimatedActualSpeed + (1 * 1024 * 1024), "#2." + i);
            }

            // Should be somewhere between 499 and 501 MB/sec
            Assert.IsTrue ((monitor.Rate - estimatedActualSpeed) < (1 * 1024 * 1024), "#3");
        }

        [Test]
        public void Tick ()
        {
            var monitor = new SpeedMonitor ();
            monitor.AddDelta (1000);
            monitor.Tick (1000);

            Assert.AreEqual (1000, monitor.Rate, "#1");
        }

        [Test]
        public void TickTwice ()
        {
            // Send 1000 bytes in 2000 milliseconds. Check
            // transfer rate is then 500bytes/second
            var monitor = new SpeedMonitor ();
            monitor.AddDelta (1000);
            monitor.Tick (1000);
            monitor.Tick (1000);

            Assert.AreEqual (500, monitor.Rate, "#1");
        }

        [Test]
        public void TickFull ()
        {
            // Averaging period is 3, tick 3 times.
            var monitor = new SpeedMonitor (3);
            monitor.AddDelta (1200);
            monitor.Tick (1000);
            monitor.Tick (1000);
            monitor.Tick (1000);

            Assert.AreEqual (400, monitor.Rate, "#1");
        }

        [Test]
        public void Tick_AveragingTwo_TickThree ()
        {
            // Send data only in the first tick. When we tick a third time
            // we should have a transfer rate of zero as the data should vanish.
            var monitor = new SpeedMonitor (2);
            monitor.AddDelta (1200);
            monitor.Tick (1000);
            monitor.Tick (1000);
            monitor.Tick (1000);

            Assert.AreEqual (0, monitor.Rate, "#1");
        }

        [Test]
        public void ZeroAveragingPeriod ()
        {
            var monitor = new SpeedMonitor (0);
            monitor.AddDelta (1000);
            monitor.Tick (1000);

            Assert.AreEqual (1000, monitor.Rate, "#1");
        }
    }
}
