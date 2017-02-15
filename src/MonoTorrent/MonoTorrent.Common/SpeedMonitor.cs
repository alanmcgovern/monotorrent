//
// SpeedMonitor.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2010 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

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
        private int[] speeds;
        private int speedsIndex;
        private DateTime lastUpdated;
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
            if (averagingPeriod < 0)
                throw new ArgumentOutOfRangeException ("averagingPeriod");

            this.lastUpdated = DateTime.UtcNow;
            this.speeds = new int [Math.Max (1, averagingPeriod)];
            this.speedsIndex = -speeds.Length;
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
            this.lastUpdated = DateTime.UtcNow;
            this.speedsIndex = -speeds.Length;
        }

        private void TimePeriodPassed(int difference)
        {
            int currSpeed = (int)(tempRecvCount * 1000 / difference);
            tempRecvCount = 0;

            int speedsCount;
            if( speedsIndex < 0 )
            {
                //speeds array hasn't been filled yet

                int idx = speeds.Length + speedsIndex;

                speeds[idx] = currSpeed;
                speedsCount = idx + 1;

                speedsIndex++;
            }
            else
            {
                //speeds array is full, keep wrapping around overwriting the oldest value
                speeds[speedsIndex] = currSpeed;
                speedsCount = speeds.Length;
        
                speedsIndex = (speedsIndex + 1) % speeds.Length;
            }
        
            int total = speeds[0];
            for( int i = 1; i < speedsCount; i++ )
                total += speeds[i];

            speed = total / speedsCount;
        }


        public void Tick()
        {
            DateTime old = lastUpdated;
            DateTime now = DateTime.UtcNow;
            int difference = (int) (now - old).TotalMilliseconds;

            if (difference > 800)
            {
                lastUpdated = now;
                TimePeriodPassed(difference);
            }
        }

        // Used purely for unit testing purposes.
        internal void Tick (int difference)
        {
            lastUpdated = DateTime.UtcNow;
            TimePeriodPassed (difference);
        }
    }
}
