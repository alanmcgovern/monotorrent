//
// RequestMonitor.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//
// Copyright (C) 2006 Gregor Burger
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


namespace MonoTorrent.Tracker
{
    /// <summary>
    /// Tracks the number of Announce and Scrape requests.
    /// </summary>
    public class RequestMonitor
    {
        /// <summary>
        /// This is the number of announce requests handled per second.
        /// </summary>
        public int AnnounceRate => (int) Announces.Rate;

        /// <summary>
        /// This is the number of scrape requests handled per second.
        /// </summary>
        public int ScrapeRate => (int) Scrapes.Rate;

        /// <summary>
        /// The total number of announces handled.
        /// </summary>
        public long TotalAnnounces => Announces.Total;

        /// <summary>
        /// The total number of  scrapes handled.
        /// </summary>
        public long TotalScrapes => Scrapes.Total;

        SpeedMonitor Announces { get; }
        SpeedMonitor Scrapes { get; }

        public RequestMonitor ()
        {
            Announces = new SpeedMonitor ();
            Scrapes = new SpeedMonitor ();
        }

        internal void AnnounceReceived ()
        {
            Announces.AddDelta (1);
        }

        internal void ScrapeReceived ()
        {
            Scrapes.AddDelta (1);
        }

        internal void Tick ()
        {
            Announces.Tick ();
            Scrapes.Tick ();
        }
    }
}
