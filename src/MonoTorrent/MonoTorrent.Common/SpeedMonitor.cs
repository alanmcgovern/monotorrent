using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Common
{
    internal class SpeedMonitor
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
            get
            {
                Tick();
                return this.speed;
            }
        }

        public long Total
        {
            get
            {
                Tick();
                return this.total;
            }
        }


        internal SpeedMonitor()
            : this(DefaultAveragePeriod)
        {

        }

        internal SpeedMonitor(int averagingPeriod)
        {
            this.lastUpdated = Environment.TickCount;
            this.speeds = new int[averagingPeriod];
        }


        internal void AddDelta(int speed)
        {
            Tick();
            this.total += speed;
            this.tempRecvCount += speed;
        }


        internal void Reset()
        {
            this.total = 0;
            this.speed = 0;
            this.speedIndex = 0;
            this.tempRecvCount = 0;
        }


        private void TimePeriodPassed()
        {
            int count = 0;
            int total = 0;

            // Find how many miliseconds have passed since the last update and the current tick count
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

        internal void Tick()
        {
            // Make sure we handle the case where the clock rolls over
            long difference = (long)Environment.TickCount - lastUpdated;
            if (difference >= 800 || difference < 0)
                TimePeriodPassed();
        }
    }
}
