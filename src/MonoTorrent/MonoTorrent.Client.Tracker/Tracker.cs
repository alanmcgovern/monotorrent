//
// Tracker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Threading.Tasks;

namespace MonoTorrent.Client.Tracker
{
    abstract class Tracker : ITracker
    {
        public bool CanAnnounce { get; protected set; }
        public bool CanScrape { get; protected set; }
        public int Complete { get; protected set; }
        public int Downloaded { get; protected set; }
        public string FailureMessage { get; protected set; }
        public int Incomplete { get; protected set; }
        ValueStopwatch LastAnnounced;
        public TimeSpan MinUpdateInterval { get; protected set; }
        public TrackerState Status { get; protected set; }
        public TimeSpan TimeSinceLastAnnounce => LastAnnounced.IsRunning ? LastAnnounced.Elapsed : TimeSpan.MaxValue;
        public TimeSpan UpdateInterval { get; protected set; }
        public Uri Uri { get; }
        public string WarningMessage { get; protected set; }

        protected Tracker (Uri uri)
        {
            LastAnnounced = new ValueStopwatch ();
            MinUpdateInterval = TimeSpan.FromMinutes (3);
            UpdateInterval = TimeSpan.FromMinutes (30);
            Uri = uri ?? throw new ArgumentNullException (nameof (uri));
            FailureMessage = "";
            WarningMessage = "";
        }

        public async Task<List<Peer>> AnnounceAsync (AnnounceParameters parameters)
        {
            List<Peer> result = await DoAnnounceAsync (parameters);
            LastAnnounced.Restart ();
            return result;
        }

        protected abstract Task<List<Peer>> DoAnnounceAsync (AnnounceParameters parameters);

        public Task ScrapeAsync (ScrapeParameters parameters)
        {
            return DoScrapeAsync (parameters);
        }

        protected abstract Task DoScrapeAsync (ScrapeParameters parameters);
    }
}
