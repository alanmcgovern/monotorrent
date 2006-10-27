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
        private AsyncCallback requestCallback;

        /// <summary>
        /// 
        /// </summary>
        public string Compact
        {
            get { return this.compact; }
        }
        private string compact;


        /// <summary>
        /// The announce URL for this tracker
        /// </summary>
        public string AnnounceUrl
        {
            get { return this.announceUrl; }
        }
        private string announceUrl;


        /// <summary>
        /// The Scrape URL for this tracker
        /// </summary>
        public string ScrapeUrl
        {
            get { return this.scrapeUrl; }
        }
        private string scrapeUrl;


        /// <summary>
        /// True if the tracker supports scrape requests
        /// </summary>
        public bool CanScrape
        {
            get { return this.canScrape; }
        }
        private bool canScrape;


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
        /// The minimum update interval for the tracker
        /// </summary>
        public int MinUpdateInterval
        {
            get { return this.minUpdateInterval; }
            internal set { this.minUpdateInterval = value; }
        }
        private int minUpdateInterval;


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
        /// The DateTime that the last tracker update was fired at
        /// </summary>
        public DateTime LastUpdated
        {
            get { return lastUpdated; }
        }
        private DateTime lastUpdated;


        /// <summary>
        /// True if the last tracker update succeeded
        /// </summary>
        public bool UpdateSucceeded
        {
            get { return this.updateSucceeded; }
            internal set { this.updateSucceeded = value; }
        }
        private bool updateSucceeded;
        #endregion


        #region Constructors
        public Tracker(string announceUrl, AsyncCallback requestCallback)
        {
            this.compact = "1";                             // Always use compact if possible.
            this.state = TrackerState.Unknown;
            this.lastUpdated = DateTime.Now.AddDays(-1);    // Forces an update on the first timertick.
            this.updateInterval = 300;                      // Update every 300 seconds.
            this.minUpdateInterval = 180;                   // Don't update more frequently than this.


            this.announceUrl = announceUrl;
            this.requestCallback = requestCallback;
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
        public WaitHandle Scrape(bool requestSingle, string infohash)
        {
            HttpWebRequest request;
            this.state = TrackerState.Scraping;

            if (requestSingle)
                request = (HttpWebRequest)HttpWebRequest.Create(this.scrapeUrl + "?infohash=" + infohash);
            else
                request = (HttpWebRequest)HttpWebRequest.Create(this.scrapeUrl);

            TrackerConnectionID id = new TrackerConnectionID(request, this);
            return request.BeginGetResponse(requestCallback, id).AsyncWaitHandle;
        }


        public WaitHandle SendUpdate(long bytesDownloaded, long bytesUploaded, long bytesLeft, TorrentEvent clientEvent, string infohash)
        {
            IPAddress ipAddress;
            TrackerConnectionID id;
            HttpWebRequest request;
            StringBuilder sb = new StringBuilder(256);

            this.updateSucceeded = true;        // If the update ends up failing, reset this to false.
            this.lastUpdated = DateTime.Now;
            this.state = TrackerState.Announcing;

            ipAddress = ConnectionListener.ListenEndPoint.Address;
            if (ipAddress != null && (ipAddress == IPAddress.Any || ipAddress == IPAddress.Loopback))
                ipAddress = null;

            sb.Append(this.announceUrl);        // FIXME: Should cycle through trackers if a problem is found
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
            sb.Append("&compact=");
            sb.Append(this.compact);
            sb.Append("&numwant=");
            sb.Append(50);             //FIXME: 50 is enough?
            if (ipAddress != null)
            {
                sb.Append("&ip=");
                sb.Append(ipAddress.ToString());
            }
            if (clientEvent != TorrentEvent.None)
            {
                sb.Append("&event=");
                sb.Append(clientEvent.ToString().ToLower());
            }

            if ((trackerId != null) && (trackerId.Length > 0))
            {
                sb.Append("&trackerid=");
                sb.Append(trackerId);
            }
            request = (HttpWebRequest)HttpWebRequest.Create(sb.ToString());
            request.Proxy = new WebProxy();   // If i don't do this, i can't run the webrequest. It's wierd.

            id = new TrackerConnectionID(request, this);
            IAsyncResult res = request.BeginGetResponse(this.requestCallback, id);

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