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


using System.Text.RegularExpressions;
using System.Threading;
using System;
using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using System.Text;
using System.Web;

namespace MonoTorrent.Client.Tracker
{
    /// <summary>
    /// Class representing an instance of a Tracker
    /// </summary>
    public abstract class Tracker
    {
        private static Random random = new Random();

        #region Events

        public event EventHandler<AnnounceResponseEventArgs> AnnounceComplete;
        public event EventHandler<ScrapeResponseEventArgs> ScrapeComplete;
        public event EventHandler<TrackerStateChangedEventArgs> StateChanged;


        #endregion


        #region Fields

        private bool canScrape;
        private int complete;
        private int downloaded;
        private string failureMessage;
        private int inComplete;
        private readonly string key;
        private DateTime lastUpdated;
        private int minUpdateInterval;
        private TrackerState state;
        private TrackerTier tier;
        private string trackerId;
        private int updateInterval;
        private bool updateSucceeded;
        private Uri uri;
        private string warningMessage;

        #endregion Fields


        #region Properties

        public bool CanScrape
        {
            get { return this.canScrape; }
            protected set { canScrape = value; }
        }

        public int Complete
        {
            get { return this.complete; }
            protected set { this.complete = value; }
        }

        public int Downloaded
        {
            get { return this.downloaded; }
            protected set { this.downloaded = value; }
        }

        public string FailureMessage
        {
            get { return this.failureMessage; }
            protected set { this.failureMessage = value; }
        }

        public int Incomplete
        {
            get { return this.inComplete; }
            protected set { this.inComplete = value; }
        }

        protected internal string Key
        {
            get { return key; }
        }

        public DateTime LastUpdated
        {
            get { return lastUpdated; }
            protected set { lastUpdated = value; }
        }

        public int MinUpdateInterval
        {
            get { return this.minUpdateInterval; }
            protected set { this.minUpdateInterval = value; }
        }

        public TrackerState State
        {
            get { return this.state; }
        }

        internal TrackerTier Tier
        {
            get { return tier; }
            set { tier = value; }
        }

        public string TrackerId
        {
            get { return this.trackerId; }
            protected set { this.trackerId = value; }
        }

        public int UpdateInterval
        {
            get { return updateInterval; }
            protected set { updateInterval = value; }
        }

        public Uri Uri
        {
            get { return uri; }
        }

        public bool UpdateSucceeded
        {
            get { return this.updateSucceeded; }
            protected set { this.updateSucceeded = value; }
        }

        public string WarningMessage
        {
            get { return this.warningMessage; }
            protected set { this.warningMessage = value; }
        }

        #endregion Properties


        #region Constructors

        protected Tracker(Uri uri)
        {
            this.uri = uri;

            this.state = TrackerState.Unknown;
            this.lastUpdated = DateTime.Now.AddDays(-1);    // Forces an update on the first timertick.
            this.updateInterval = 300;                      // Update every 300 seconds.
            this.minUpdateInterval = 180;                   // Don't update more frequently than this.

            this.warningMessage = string.Empty;
            this.failureMessage = string.Empty;
            byte[] passwordKey = new byte[8];
            lock (random)
                random.NextBytes(passwordKey);
            this.key = HttpUtility.UrlEncode(passwordKey);
        }

        #endregion


        #region Methods

        public abstract WaitHandle Announce(AnnounceParameters parameters);

        public abstract WaitHandle Scrape(ScrapeParameters parameters);

        protected void UpdateState(TrackerState newState)
        {
            if (state == newState)
                return;

            // FIXME: Don't send null!
            TrackerStateChangedEventArgs e = new TrackerStateChangedEventArgs(null, this, State, newState);
            state = newState;

            RaiseStateChanged(e);
        }

        // FIXME: Don't send null. Send torrentmanager
        protected virtual void RaiseAnnounceComplete(AnnounceResponseEventArgs e)
        {
            Toolbox.RaiseAsyncEvent<AnnounceResponseEventArgs>(AnnounceComplete, this, e);
        }

        protected virtual void RaiseScrapeComplete(ScrapeResponseEventArgs e)
        {
            Toolbox.RaiseAsyncEvent<ScrapeResponseEventArgs>(ScrapeComplete, this, e);
        }

        private void RaiseStateChanged(TrackerStateChangedEventArgs e)
        {
            Toolbox.RaiseAsyncEvent<TrackerStateChangedEventArgs>(StateChanged, this, e);
        }

        public override string ToString()
        {
            return uri.ToString();
        }

        #endregion
    }
}
