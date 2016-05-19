using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Tests.Common
{
    public class SpeedMonitorTest
    {
        [Fact]
        public void Tick()
        {
            var monitor = new SpeedMonitor();
            monitor.AddDelta(1000);
            monitor.Tick(1000);

            Assert.Equal(1000, monitor.Rate);
        }

        [Fact]
        public void Tick_AveragingTwo_TickThree()
        {
            // Send data only in the first tick. When we tick a third time
            // we should have a transfer rate of zero as the data should vanish.
            var monitor = new SpeedMonitor(2);
            monitor.AddDelta(1200);
            monitor.Tick(1000);
            monitor.Tick(1000);
            monitor.Tick(1000);

            Assert.Equal(0, monitor.Rate);
        }

        [Fact]
        public void TickFull()
        {
            // Averaging period is 3, tick 3 times.
            var monitor = new SpeedMonitor(3);
            monitor.AddDelta(1200);
            monitor.Tick(1000);
            monitor.Tick(1000);
            monitor.Tick(1000);

            Assert.Equal(400, monitor.Rate);
        }

        [Fact]
        public void TickTwice()
        {
            // Send 1000 bytes in 2000 milliseconds. Check
            // transfer rate is then 500bytes/second
            var monitor = new SpeedMonitor();
            monitor.AddDelta(1000);
            monitor.Tick(1000);
            monitor.Tick(1000);

            Assert.Equal(500, monitor.Rate);
        }

        [Fact]
        public void ZeroAveragingPeriod()
        {
            var monitor = new SpeedMonitor(0);
            monitor.AddDelta(1000);
            monitor.Tick(1000);

            Assert.Equal(1000, monitor.Rate);
        }
    }
}