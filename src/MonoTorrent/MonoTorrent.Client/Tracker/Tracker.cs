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
using System.Diagnostics;
using System.Threading.Tasks;

using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    public abstract class Tracker : ITracker
    {
        public event EventHandler<AnnounceResponseEventArgs> AnnounceComplete;
        public event EventHandler<ScrapeResponseEventArgs> ScrapeComplete;

        public bool CanAnnounce { get; protected set; }
        public bool CanScrape { get; protected set; }
        public int Complete { get; protected set; }
        public int Downloaded { get; protected set; }
        public string FailureMessage { get; protected set; }
        public int Incomplete { get; protected set; }
        Stopwatch LastAnnounced { get; }
        public TimeSpan MinUpdateInterval { get; protected set; }
        public TrackerState Status { get; protected set; }
        public TimeSpan TimeSinceLastAnnounce => LastAnnounced.IsRunning ? LastAnnounced.Elapsed : TimeSpan.MaxValue;
        public TimeSpan UpdateInterval { get; protected set; }
        public Uri Uri { get; }
        public string WarningMessage { get; protected set; }

        protected Tracker (Uri uri)
        {
            LastAnnounced = new Stopwatch ();
            MinUpdateInterval = TimeSpan.FromMinutes(3);
            UpdateInterval = TimeSpan.FromMinutes(30);
            Uri = uri ?? throw new ArgumentNullException (nameof (uri));
            FailureMessage = "";
            WarningMessage = "";
        }

        public abstract Task<List<Peer>> AnnounceAsync (AnnounceParameters parameters);
        public abstract Task ScrapeAsync (ScrapeParameters parameters);

        protected virtual void RaiseAnnounceComplete (AnnounceResponseEventArgs e)
        {
            if (e.Successful)
                LastAnnounced.Restart ();
            AnnounceComplete?.Invoke (this, e);
        }

        protected virtual void RaiseScrapeComplete (ScrapeResponseEventArgs e)
            => ScrapeComplete?.Invoke (this, e);
    }
}
