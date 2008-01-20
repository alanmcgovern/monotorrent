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

namespace MonoTorrent.Client
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
        private string warningMessage;

        #endregion Fields


        #region Properties

        /// <summary>
        /// True if the tracker supports scrape requests
        /// </summary>
        public bool CanScrape
        {
            get { return this.canScrape; }
            protected internal set { canScrape = value; }
        }


        /// <summary>
        /// The number of seeders downloading the torrent
        /// </summary>
        public int Complete
        {
            get { return this.complete; }
            protected internal set { this.complete = value; }
        }


        /// <summary>
        /// The number of times the torrent was downloaded
        /// </summary>
        public int Downloaded
        {
            get { return this.downloaded; }
            protected internal set { this.downloaded = value; }
        }


        /// <summary>
        /// The error message returned by the tracker
        /// </summary>
        public string FailureMessage
        {
            get { return this.failureMessage; }
            protected internal set { this.failureMessage = value; }
        }


        /// <summary>
        /// The number of peers downloading the torrent who are not seeders.
        /// </summary>
        public int Incomplete
        {
            get { return this.inComplete; }
            protected internal set { this.inComplete = value; }
        }


        /// <summary>
        /// The private key used in tracker communcations. Must be sent in every tracker request
        /// </summary>
        protected internal string Key
        {
            get { return key; }
        }


        /// <summary>
        /// The DateTime that the last tracker update was fired at
        /// </summary>
        public DateTime LastUpdated
        {
            get { return lastUpdated; }
        }


        /// <summary>
        /// The minimum update interval for the tracker
        /// </summary>
        public int MinUpdateInterval
        {
            get { return this.minUpdateInterval; }
            protected internal set { this.minUpdateInterval = value; }
        }


        /// <summary>
        /// The current state of the tracker
        /// </summary>
        public TrackerState State
        {
            get { return this.state; }
        }

        internal TrackerTier Tier
        {
            get { return tier; }
            set { tier = value; }
        }

        /// <summary>
        /// The ID for the current tracker
        /// </summary>
        public string TrackerId
        {
            get { return this.trackerId; }
            protected internal set { this.trackerId = value; }
        }


        /// <summary>
        /// The recommended update interval for the tracker
        /// </summary>
        public int UpdateInterval
        {
            get { return updateInterval; }
            protected internal set { updateInterval = value; }
        }


        /// <summary>
        /// True if the last tracker update succeeded
        /// </summary>
        public bool UpdateSucceeded
        {
            get { return this.updateSucceeded; }
            protected internal set { this.updateSucceeded = value; }
        }


        /// <summary>
        /// The warning message returned by the tracker
        /// </summary>
        public string WarningMessage
        {
            get { return this.warningMessage; }
            protected internal set { this.warningMessage = value; }
        }

        #endregion Properties


        #region Constructors

        protected Tracker()
        {
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

        public abstract WaitHandle Scrape(byte[] infohash, TrackerConnectionID id);



        /// <summary>
        /// Wrapper method to call the OnStateChanged event correctly
        /// </summary>
        /// 
        /// <param name="newState"></param>
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
            ThreadPool.QueueUserWorkItem(delegate
            {
                EventHandler<AnnounceResponseEventArgs> h = AnnounceComplete;
                if (h != null)
                    h(null, e);
            });
        }

        protected virtual void RaiseScrapeComplete(ScrapeResponseEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                EventHandler<ScrapeResponseEventArgs> h = ScrapeComplete;
                if (h != null)
                    h(null, e);
            });
        }

        private void RaiseStateChanged(TrackerStateChangedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                EventHandler<TrackerStateChangedEventArgs> h = StateChanged;
                if (h != null)
                    h(null, e);
            });
        }

        #endregion
    }
}
