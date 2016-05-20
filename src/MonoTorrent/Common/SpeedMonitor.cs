using System;

namespace MonoTorrent.Common
{
    public class SpeedMonitor
    {
        private const int DefaultAveragePeriod = 12;
        private readonly int[] speeds;
        private DateTime lastUpdated;
        private int speedsIndex;
        private long tempRecvCount;


        public SpeedMonitor()
            : this(DefaultAveragePeriod)
        {
        }

        public SpeedMonitor(int averagingPeriod)
        {
            if (averagingPeriod < 0)
                throw new ArgumentOutOfRangeException("averagingPeriod");

            lastUpdated = DateTime.UtcNow;
            speeds = new int[Math.Max(1, averagingPeriod)];
            speedsIndex = -speeds.Length;
        }


        public int Rate { get; private set; }

        public long Total { get; private set; }


        public void AddDelta(int speed)
        {
            Total += speed;
            tempRecvCount += speed;
        }

        public void AddDelta(long speed)
        {
            Total += speed;
            tempRecvCount += speed;
        }

        public void Reset()
        {
            Total = 0;
            Rate = 0;
            tempRecvCount = 0;
            lastUpdated = DateTime.UtcNow;
            speedsIndex = -speeds.Length;
        }

        private void TimePeriodPassed(int difference)
        {
            var currSpeed = (int) (tempRecvCount*1000/difference);
            tempRecvCount = 0;

            int speedsCount;
            if (speedsIndex < 0)
            {
                //speeds array hasn't been filled yet

                var idx = speeds.Length + speedsIndex;

                speeds[idx] = currSpeed;
                speedsCount = idx + 1;

                speedsIndex++;
            }
            else
            {
                //speeds array is full, keep wrapping around overwriting the oldest value
                speeds[speedsIndex] = currSpeed;
                speedsCount = speeds.Length;

                speedsIndex = (speedsIndex + 1)%speeds.Length;
            }

            var total = speeds[0];
            for (var i = 1; i < speedsCount; i++)
                total += speeds[i];

            Rate = total/speedsCount;
        }


        public void Tick()
        {
            var old = lastUpdated;
            lastUpdated = DateTime.UtcNow;
            var difference = (int) (lastUpdated - old).TotalMilliseconds;

            if (difference > 800)
                TimePeriodPassed(difference);
        }

        // Used purely for unit testing purposes.
        internal void Tick(int difference)
        {
            lastUpdated = DateTime.UtcNow;
            TimePeriodPassed(difference);
        }
    }
}