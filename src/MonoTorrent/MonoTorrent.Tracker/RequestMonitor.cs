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
    public class RequestMonitor
    {
        #region Member Variables

        private SpeedMonitor announces;
        private SpeedMonitor scrapes;

        #endregion Member Variables


        #region Properties

        public int AnnounceRate
        {
            get { return announces.Rate; }
        }

        public int ScrapeRate
        {
            get { return scrapes.Rate; }
        }

        public int TotalAnnounces
        {
            get { return (int)announces.Total; }
        }

        public int TotalScrapes
        {
            get { return (int)scrapes.Total; }
        }


        #endregion Properties


        #region Constructors

        public RequestMonitor()
        {
            announces = new SpeedMonitor();
            scrapes = new SpeedMonitor();
        }

        #endregion Constructors


        #region Methods

        internal void AnnounceReceived()
        {
            lock (announces)
                announces.AddDelta(1);
        }

        internal void ScrapeReceived()
        {
            lock (scrapes)
                scrapes.AddDelta(1);
        }

        #endregion Methods

        internal void Tick()
        {
            lock (announces)
                this.announces.Tick();
            lock (scrapes)
                this.scrapes.Tick();
        }
    }
}
