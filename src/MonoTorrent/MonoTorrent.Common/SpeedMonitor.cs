using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Common
{
    public class SpeedMonitor
    {
        private const int DefaultAveragePeriod = 12;

        private int averagingPeriod;
        private long total;
        private int speed;
        private List<int> speeds;
        private int lastUpdated;
        private long tempRecvCount;


        public int Rate
        {
            get { return this.speed; }
        }

        public long Total
        {
            get { return this.total; }
        }


        public SpeedMonitor()
            : this(DefaultAveragePeriod)
        {

        }

        public SpeedMonitor(int averagingPeriod)
        {
            this.averagingPeriod = averagingPeriod;
            this.lastUpdated = Environment.TickCount;
            this.speeds = new List<int>(averagingPeriod);
        }


        public void AddDelta(int speed)
        {
            this.total += speed;
            this.tempRecvCount += speed;
        }

		public void AddDelta(long speed)
		{
			this.total += speed;
			this.tempRecvCount += speed;
		}


        public void Reset()
        {
            this.total = 0;
            this.speed = 0;
            this.tempRecvCount = 0;
            this.lastUpdated = Environment.TickCount;
            this.speeds.Clear();
        }


        private void TimePeriodPassed()
        {
            int count = 0;
            long total = 0;

            // Find how many milliseconds have passed since the last update and the current tick count
            int difference = Environment.TickCount - this.lastUpdated;
            if (difference < 0)
                difference = 1000;

            // Take the amount of bytes sent since the last tick and divide it by the number of seconds
            // since the last tick. This gives the calculated bytes/second transfer rate.
            // difference is in miliseconds, so divide by 1000 to get it in seconds
            if (speeds.Count == this.averagingPeriod)
                speeds.RemoveAt (0);
            speeds.Add ((int)(tempRecvCount / (difference / 1000.0)));

            // What we do here is add up all the bytes/second readings held in each array
            // and divide that by the number of non-zero entries. The number of non-zero entries
            // is given by ArraySize - count. This is to avoid the problem where a connection which
            // is just starting would have a lot of zero entries making the speed estimate inaccurate.
            for (int i = 0; i < this.speeds.Count; i++)
            {
                if (speeds.Count != averagingPeriod)
                    count++;

                total += this.speeds[i];
            }

            this.speed = (int)(total / Math.Max (1, speeds.Count - count));
            this.tempRecvCount = 0;
            this.lastUpdated = Environment.TickCount;
        }

        public void Tick()
        {
            long difference = (long)Environment.TickCount - lastUpdated;
            if (difference >= 800 || difference < 0)
                TimePeriodPassed();
        }
    }
}
