using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Common
{
    public class SpeedMonitor
    {
        private const int DefaultAveragePeriod = 12;

        private long total;
        private int speed;
        private int speedIndex;
        private int[] speeds;
        private int lastUpdated;
        private int tempRecvCount;


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
            this.lastUpdated = Environment.TickCount;
            this.speeds = new int[averagingPeriod];
        }


        public void AddDelta(int speed)
        {
            this.total += speed;
            this.tempRecvCount += speed;
        }


        public void Reset()
        {
            this.total = 0;
            this.speed = 0;
            this.speedIndex = 0;
            this.tempRecvCount = 0;
            this.lastUpdated = Environment.TickCount;
            for (int i = 0; i < this.speeds.Length; i++)
                speeds[i] = 0;
        }


        private void TimePeriodPassed()
        {
            int count = 0;
            int total = 0;

            // Find how many milliseconds have passed since the last update and the current tick count
            int difference = Environment.TickCount - this.lastUpdated;
            if (difference < 0)
                difference = 1000;

            // Take the amount of bytes sent since the last tick and divide it by the number of seconds
            // since the last tick. This gives the calculated bytes/second transfer rate.
            // difference is in miliseconds, so divide by 1000 to get it in seconds
            this.speeds[this.speedIndex++] = (int)(tempRecvCount / (difference / 1000.0));

            // If we've gone over the array bounds, reset to the first index
            // to start overwriting the old values
            if (this.speedIndex == speeds.Length)
                this.speedIndex = 0;

            // What we do here is add up all the bytes/second readings held in each array
            // and divide that by the number of non-zero entries. The number of non-zero entries
            // is given by ArraySize - count. This is to avoid the problem where a connection which
            // is just starting would have a lot of zero entries making the speed estimate inaccurate.
            for (int i = 0; i < this.speeds.Length; i++)
            {
                if (this.speeds[i] == 0)
                    count++;

                total += this.speeds[i];
            }
            if (count == speeds.Length)
                count--;

            this.speed = (total / (speeds.Length - count));
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
