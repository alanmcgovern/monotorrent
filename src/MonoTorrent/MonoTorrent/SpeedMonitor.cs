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
using System.Threading;

namespace MonoTorrent
{
    public class SpeedMonitor
    {
        public const int DefaultAveragePeriod = 12;

        ValueStopwatch lastUpdated;
        long rate;
        readonly long[] speeds;
        int speedsIndex;
        long tempRecvCount;
        long total;

        public long Rate => Interlocked.Read (ref rate);

        public long Total => Interlocked.Read (ref total);

        public SpeedMonitor ()
            : this (DefaultAveragePeriod)
        {

        }

        public SpeedMonitor (int averagingPeriod)
        {
            if (averagingPeriod < 0)
                throw new ArgumentOutOfRangeException (nameof (averagingPeriod));

            lastUpdated = ValueStopwatch.StartNew ();
            speeds = new long[Math.Max (1, averagingPeriod)];
            speedsIndex = -speeds.Length;
        }

        /// <summary>
        /// This method is threadsafe and can be called at any point.
        /// </summary>
        /// <param name="speed"></param>
        public void AddDelta (int speed)
        {
            Interlocked.Add (ref total, speed);
            Interlocked.Add (ref tempRecvCount, speed);
        }

        public void Reset ()
        {
            Interlocked.Exchange (ref total, 0);
            Interlocked.Exchange (ref tempRecvCount, 0);

            rate = 0;
            lastUpdated.Restart ();
            speedsIndex = -speeds.Length;
        }

        public void Tick ()
        {
            int difference = (int) lastUpdated.Elapsed.TotalMilliseconds;
            if (difference > 800)
                Tick (difference);
        }

        public void Tick (int difference)
        {
            lastUpdated.Restart ();
            TimePeriodPassed (difference);
        }

        void TimePeriodPassed (int difference)
        {
            long currSpeed = Interlocked.Exchange (ref tempRecvCount, 0);
            currSpeed = (currSpeed * 1000) / difference;

            int speedsCount;
            if (speedsIndex < 0) {
                //speeds array hasn't been filled yet

                int idx = speeds.Length + speedsIndex;

                speeds[idx] = currSpeed;
                speedsCount = idx + 1;

                speedsIndex++;
            } else {
                //speeds array is full, keep wrapping around overwriting the oldest value
                speeds[speedsIndex] = currSpeed;
                speedsCount = speeds.Length;

                speedsIndex = (speedsIndex + 1) % speeds.Length;
            }

            long sumTotal = speeds[0];
            for (int i = 1; i < speedsCount; i++)
                sumTotal += speeds[i];

            Interlocked.Exchange (ref rate, sumTotal / speedsCount);
        }

    }
}
