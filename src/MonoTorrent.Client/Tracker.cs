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
using System.Net;
using System.Threading;
using System;
using MonoTorrent.Common;
using System.Text;
using System.Web;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Class representing an instance of a Tracker
    /// </summary>
    public class Tracker
    {
        #region Member Variables
        private AsyncCallback announceCallback;
        private AsyncCallback scrapeCallback;


        /// <summary>
        /// The announce URL for this tracker
        /// </summary>
        private string announceUrl;


        /// <summary>
        /// True if the tracker supports scrape requests
        /// </summary>
        public bool CanScrape
        {
            get { return this.canScrape; }
        }
        private bool canScrape;


        /// <summary>
        /// The number of seeders downloading the torrent
        /// </summary>
        public int Complete
        {
            get { return this.complete; }
            internal set { this.complete = value; }
        }
        private int complete;


        /// <summary>
        /// The number of times the torrent was downloaded
        /// </summary>
        public int Downloaded
        {
            get { return this.downloaded; }
            internal set { this.downloaded = value; }
        }
        private int downloaded;


        /// <summary>
        /// The error message returned by the tracker
        /// </summary>
        public string FailureMessage
        {
            get { return this.failureMessage; }
            internal set { this.failureMessage = value; }
        }
        private string failureMessage;


        /// <summary>
        /// The number of peers downloading the torrent who are not seeders.
        /// </summary>
        public int Incomplete
        {
            get { return this.inComplete; }
            internal set { this.inComplete = value; }
        }
        private int inComplete;


        /// <summary>
        /// The DateTime that the last tracker update was fired at
        /// </summary>
        public DateTime LastUpdated
        {
            get { return lastUpdated; }
        }
        private DateTime lastUpdated;


        /// <summary>
        /// The minimum update interval for the tracker
        /// </summary>
        public int MinUpdateInterval
        {
            get { return this.minUpdateInterval; }
            internal set { this.minUpdateInterval = value; }
        }
        private int minUpdateInterval;


        /// <summary>
        /// The Scrape URL for this tracker
        /// </summary>
        //public string ScrapeUrl
        //{
        //    get { return this.scrapeUrl; }
        //}
        private string scrapeUrl;


        internal bool SendingStatedEvent
        {
            get { return this.sendingStartedEvent; }
            set { this.sendingStartedEvent = value; }
        }
        private bool sendingStartedEvent;


        internal bool StartedEventSentSuccessfully
        {
            get { return this.startedEventSentSuccessfully; }
            set { this.startedEventSentSuccessfully = value; }
        }
        private bool startedEventSentSuccessfully;


        /// <summary>
        /// The current state of the tracker
        /// </summary>
        public TrackerState State
        {
            get { return this.state; }
            internal set { this.state = value; }
        }
        private TrackerState state;


        /// <summary>
        /// The ID for the current tracker
        /// </summary>
        public string TrackerId
        {
            get { return this.trackerId; }
            internal set { this.trackerId = value; }
        }
        private string trackerId;


        /// <summary>
        /// The recommended update interval for the tracker
        /// </summary>
        public int UpdateInterval
        {
            get { return updateInterval; }
            internal set { updateInterval = value; }
        }
        private int updateInterval;


        /// <summary>
        /// True if the last tracker update succeeded
        /// </summary>
        public bool UpdateSucceeded
        {
            get { return this.updateSucceeded; }
            internal set { this.updateSucceeded = value; }
        }
        private bool updateSucceeded;


        /// <summary>
        /// The warning message returned by the tracker
        /// </summary>
        public string WarningMessage
        {
            get { return this.warningMessage; }
            internal set { this.warningMessage = value; }
        }
        private string warningMessage;
        #endregion


        #region Constructors
        public Tracker(string announceUrl, AsyncCallback announceCallback, AsyncCallback scrapeCallback)
        {
            this.state = TrackerState.Unknown;
            this.lastUpdated = DateTime.Now.AddDays(-1);    // Forces an update on the first timertick.
            this.updateInterval = 300;                      // Update every 300 seconds.
            this.minUpdateInterval = 180;                   // Don't update more frequently than this.

            this.announceUrl = announceUrl;
            this.announceCallback = announceCallback;
            this.scrapeCallback = scrapeCallback;
            int indexOfAnnounce = announceUrl.LastIndexOf('/') + 1;
            if (announceUrl.Substring(indexOfAnnounce, 8) == "announce")
            {
                this.canScrape = true;
                Regex r = new Regex("announce");
                this.scrapeUrl = r.Replace(announceUrl, "scrape", 1, indexOfAnnounce);
            }
        }
        #endregion


        #region Methods
        internal WaitHandle Scrape(bool requestSingle, string infohash)
        {
            HttpWebRequest request;

            if (requestSingle)
                request = (HttpWebRequest)HttpWebRequest.Create(this.scrapeUrl + "?infohash=" + infohash);
            else
                request = (HttpWebRequest)HttpWebRequest.Create(this.scrapeUrl);

            TrackerConnectionID id = new TrackerConnectionID(request, this);
            return request.BeginGetResponse(announceCallback, id).AsyncWaitHandle;
        }


        internal WaitHandle Announce(long bytesDownloaded, long bytesUploaded, long bytesLeft, TorrentEvent clientEvent, string infohash)
        {
            IPAddress ipAddress;
            TrackerConnectionID id;
            HttpWebRequest request;
            StringBuilder sb = new StringBuilder(256);

            this.updateSucceeded = true;        // If the update ends up failing, reset this to false.
            this.lastUpdated = DateTime.Now;

            ipAddress = ConnectionListener.ListenEndPoint.Address;
            if (ipAddress != null && (ipAddress == IPAddress.Any || ipAddress == IPAddress.Loopback))
                ipAddress = null;

            sb.Append(this.announceUrl);
            sb.Append("?info_hash=");
            sb.Append(infohash);
            sb.Append("&peer_id=");
            sb.Append(ClientEngine.PeerId);
            sb.Append("&port=");
            sb.Append(ConnectionListener.ListenEndPoint.Port);
            sb.Append("&uploaded=");
            sb.Append(bytesUploaded);
            sb.Append("&downloaded=");
            sb.Append(bytesDownloaded);
            sb.Append("&left=");
            sb.Append(bytesLeft);
            sb.Append("&compact=1");    // Always use compact response
            sb.Append("&numwant=");
            sb.Append(100);
            if (ipAddress != null)
            {
                sb.Append("&ip=");
                sb.Append(ipAddress.ToString());
            }

            // If we have successfully sent the started event, we just continue as normal
            if (this.startedEventSentSuccessfully)
            {
                if (clientEvent != TorrentEvent.None)
                {
                    sb.Append("&event=");
                    sb.Append(clientEvent.ToString().ToLower());
                }
            }
            else // Otherwise we must override the supplied event and send the started event
            {
                sb.Append("&event=started");
                this.sendingStartedEvent = true;
            }

            if ((trackerId != null) && (trackerId.Length > 0))
            {
                sb.Append("&trackerid=");
                sb.Append(trackerId);
            }
            request = (HttpWebRequest)HttpWebRequest.Create(sb.ToString());
            request.Proxy = new WebProxy();   // If i don't do this, i can't run the webrequest. It's wierd.

            id = new TrackerConnectionID(request, this);
            IAsyncResult res = request.BeginGetResponse(this.announceCallback, id);

            return res.AsyncWaitHandle;
        }
        #endregion


        #region Overidden Methods
        public override bool Equals(object obj)
        {
            Tracker tracker = obj as Tracker;
            if (tracker == null)
                return false;

            // If the announce URL matches, then CanScrape and the scrape URL must match too
            return (this.announceUrl == tracker.announceUrl);
        }


        public override int GetHashCode()
        {
            return this.announceUrl.GetHashCode();
        }

      
        public override string ToString()
        {
            return this.announceUrl;
        }
        #endregion
    }
}