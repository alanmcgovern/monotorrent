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
using System.Text;

namespace MonoTorrent.Client.Tracker
{
    public abstract class Tracker : ITracker
    {
        public event EventHandler<AnnounceResponseEventArgs> AnnounceComplete;
        public event EventHandler<ScrapeResponseEventArgs> ScrapeComplete;

        bool canAnnounce;
        bool canScrape;
        int complete;
        int downloaded;
        string failureMessage;
        int incomplete;
        TimeSpan minUpdateInterval;
        TimeSpan updateInterval;
        Uri uri;
        string warningMessage;

        public bool CanAnnounce
        {
            get { return canAnnounce; }
            protected set { canAnnounce = value; }
        }
        public bool CanScrape
        {
            get { return canScrape; }
            set { canScrape = value; }
        }
        public int Complete
        {
            get { return complete; }
            protected set { complete = value; }
        }
        public int Downloaded
        {
            get { return downloaded; }
            protected set { downloaded = value; }
        }
        public string FailureMessage
        {
            get { return failureMessage ?? ""; }
            protected set { failureMessage = value; }
        }
        public int Incomplete
        {
            get { return incomplete; }
            protected set { incomplete = value; }
        }
        public TimeSpan MinUpdateInterval
        {
            get { return minUpdateInterval; }
            protected set { minUpdateInterval = value; }
        }
        public TimeSpan UpdateInterval
        {
            get { return updateInterval; }
            protected set { updateInterval = value; }
        }
        public Uri Uri
        {
            get { return uri; }
        }
        public string WarningMessage
        {
            get { return warningMessage ?? ""; }
            protected set { warningMessage = value; }
        }

        protected Tracker(Uri uri)
        {
            Check.Uri(uri);
            MinUpdateInterval = TimeSpan.FromMinutes(3);
            UpdateInterval = TimeSpan.FromMinutes(30);
            this.uri = uri;
        }

        public abstract void Announce(AnnounceParameters parameters, object state);
        public abstract void Scrape(ScrapeParameters parameters, object state);

        protected virtual void RaiseAnnounceComplete(AnnounceResponseEventArgs e)
        {
            EventHandler<AnnounceResponseEventArgs> h = AnnounceComplete;
            if (h != null)
                h(this, e);
        }
        protected virtual void RaiseScrapeComplete(ScrapeResponseEventArgs e)
        {
            EventHandler<ScrapeResponseEventArgs> h = ScrapeComplete;
            if (h != null)
                h(this, e);
        }
    }
}
