//
// TrackerConnection.cs
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
using System.Text;
using System.Net;
using System.IO;
using MonoTorrent.Common;
using System.Collections.ObjectModel;
using System.Threading;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Represents the connection to a tracker that an ITorrentManager has
    /// </summary>
    public class TrackerManager
    {
        #region Events
        /// <summary>
        /// Event that's fired every time the state changes during a TrackerUpdate
        /// </summary>
        public event EventHandler<TrackerUpdateEventArgs> UpdateRecieved;
        #endregion


        #region Member Variables
        /// <summary>
        /// The current state of the tracker
        /// </summary>
        public TrackerState State
        {
            get { return this.state; }
        }
        private TrackerState state;


        /// <summary>
        /// True if the last update succeeded
        /// </summary>
        public bool UpdateSucceeded
        {
            get { return this.updateSucceeded; }
        }
        private bool updateSucceeded;


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
        /// The infohash for the torrent
        /// </summary>
        private byte[] infoHash;


        /// <summary>
        /// 
        /// </summary>
        public string Compact
        {
            get { return this.compact; }
        }
        private string compact;


        /// <summary>
        /// The announceURLs available for this torrent
        /// </summary>
        public string[] AnnounceUrls
        {
            get { return this.announceUrls; }
        }
        private string[] announceUrls;
        private int lastUsedAnnounceUrl;

        
        private TorrentManager manager;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new TrackerConnection for the supplied torrent file
        /// </summary>
        /// <param name="manager">The TorrentManager to create the tracker connection for</param>
        public TrackerManager(TorrentManager manager)
        {
            this.lastUsedAnnounceUrl = 0;
            this.state = TrackerState.Inactive;
            this.infoHash = manager.Torrent.InfoHash;
            this.announceUrls = manager.Torrent.AnnounceUrls;
            this.manager = manager;
            this.compact = "1";                             // Always use compact if possible.
            this.lastUpdated = DateTime.Now.AddDays(-1);    // Forces an update on the first timertick.
            this.updateInterval = 300;                      // Update every 300 seconds.
            this.minUpdateInterval = 180;                   // Don't update more frequently than this.
        }
        #endregion


        #region Methods
        /// <summary>
        /// Sends a status update to the tracker
        /// </summary>
        /// <param name="bytesDownloaded">The number of bytes downloaded since the last update</param>
        /// <param name="bytesUploaded">The number of bytes uploaded since the last update</param>
        /// <param name="bytesLeft">The number of bytes left to download</param>
        /// <param name="clientEvent">The Event (if any) that represents this update</param>
        public WaitHandle SendUpdate(long bytesDownloaded, long bytesUploaded, long bytesLeft, TorrentEvent clientEvent)
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

            sb.Append(ChooseTracker());        // FIXME: Should cycle through trackers if a problem is found
            sb.Append("?info_hash=");
            sb.Append(System.Web.HttpUtility.UrlEncode(this.infoHash));
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
            //sb.Append("&numwant=");
            //sb.Append(150);             //FIXME: 50 is enough?
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

            id = new TrackerConnectionID(request, manager);
            IAsyncResult res = request.BeginGetResponse(new AsyncCallback(ResponseRecieved), id);

            if (this.UpdateRecieved != null)
                this.UpdateRecieved(this.manager, new TrackerUpdateEventArgs(TrackerState.Updating, null));

            return res.AsyncWaitHandle;
        }


        /// <summary>
        /// Called as part of the Async SendUpdate reponse
        /// </summary>
        /// <param name="result"></param>
        private void ResponseRecieved(IAsyncResult result)
        {
            int bytesRead = 0;
            int totalRead = 0;
            byte[] buffer = new byte[2048];
            HttpWebResponse response;
            TrackerConnectionID id = (TrackerConnectionID)result.AsyncState;

            try
            {
                response = (HttpWebResponse)id.Request.EndGetResponse(result);
            }
            catch (WebException ex)
            {
                this.state = TrackerState.Inactive;
                if (this.UpdateRecieved != null)
                    UpdateRecieved(this.manager, new TrackerUpdateEventArgs(TrackerState.UpdateFailed, null));
                return;
            }

            this.state = TrackerState.Active;
            MemoryStream dataStream = new MemoryStream(response.ContentLength > 0 ? (int)response.ContentLength : 0);

            using (BinaryReader reader = new BinaryReader(response.GetResponseStream()))
            {
                // If there is a ContentLength, use that to decide how much we read.
                if (response.ContentLength > 0)
                {
                    while (totalRead < response.ContentLength)
                    {
                        bytesRead = reader.Read(buffer, 0, buffer.Length);
                        dataStream.Write(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                    }
                }



                else    // A compact response doesn't always have a content length, so we
                {       // just have to keep reading until we think we have everything.
                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                        dataStream.Write(buffer, 0, bytesRead);
                }
            }
            response.Close();
            if (this.UpdateRecieved != null)
                UpdateRecieved(this.manager, new TrackerUpdateEventArgs(this.state, dataStream.ToArray()));
        }


        /// <summary>
        /// If a tracker is unreachable, the next tracker is chosen from the list
        /// </summary>
        /// <returns></returns>
        private string ChooseTracker()
        {
            if (this.state == TrackerState.UpdateFailed)
            {
                this.lastUsedAnnounceUrl++;
                if (this.lastUsedAnnounceUrl == this.announceUrls.Length)
                    this.lastUsedAnnounceUrl = 0;
            }

            return this.announceUrls[this.lastUsedAnnounceUrl];
        }
        #endregion
    }
}