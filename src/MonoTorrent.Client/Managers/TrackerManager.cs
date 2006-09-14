//
// TrackerManager.cs
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
using System.Web;

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
        private AsyncCallback responseReceived;
        private TorrentManager manager;


        /// <summary>
        /// The infohash for the torrent
        /// </summary>
        private string infoHash;


        /// <summary>
        /// True if the last update succeeded
        /// </summary>
        public bool UpdateSucceeded
        {
            get { return this.updateSucceeded; }
        }
        private bool updateSucceeded;


        /// <summary>
        /// The time the last tracker update was sent to any tracker
        /// </summary>
        public DateTime LastUpdated
        {
            get { return this.lastUpdated; }
        }
        private DateTime lastUpdated;


        /// <summary>
        /// The announceURLs available for this torrent
        /// </summary>
        public Tracker[] Trackers
        {
            get { return this.trackers; }
        }
        private Tracker[] trackers;
        private int lastUsedTracker;


        /// <summary>
        /// Returns the tracker that is currently being actively used by the engine.
        /// </summary>
        public Tracker CurrentTracker
        {
            get { return this.trackers[this.lastUsedTracker]; } 
        }
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new TrackerConnection for the supplied torrent file
        /// </summary>
        /// <param name="manager">The TorrentManager to create the tracker connection for</param>
        public TrackerManager(TorrentManager manager)
        {
            this.manager = manager;
            this.lastUsedTracker = 0;
            this.infoHash = HttpUtility.UrlEncode(manager.Torrent.InfoHash);

            this.responseReceived = new AsyncCallback(this.ResponseRecieved);

            // Check if this tracker supports scraping
            this.trackers = new Tracker[manager.Torrent.AnnounceUrls.Length];
            for (int i = 0; i < manager.Torrent.AnnounceUrls.Length; i++)
                this.trackers[i] = new Tracker(manager.Torrent.AnnounceUrls[i], this.responseReceived);
        }
        #endregion


        #region Methods
        /// <summary>
        /// Scrapes the tracker for peer information.
        /// </summary>
        /// <param name="requestSingle">True if you want scrape information for just the torrent in the TorrentManager. False if you want everything on the tracker</param>
        /// <returns></returns>
        public WaitHandle Scrape(bool requestSingle)
        {
            Tracker tracker = this.ChooseTracker();
            WaitHandle handle = tracker.Scrape(requestSingle, this.infoHash);
            this.UpdateRecieved(this, new TrackerUpdateEventArgs(tracker, null));
            return handle;
        }

        /// <summary>
        /// Sends a status update to the tracker
        /// </summary>
        /// <param name="bytesDownloaded">The number of bytes downloaded since the last update</param>
        /// <param name="bytesUploaded">The number of bytes uploaded since the last update</param>
        /// <param name="bytesLeft">The number of bytes left to download</param>
        /// <param name="clientEvent">The Event (if any) that represents this update</param>
        public WaitHandle SendUpdate(long bytesDownloaded, long bytesUploaded, long bytesLeft, TorrentEvent clientEvent)
        {
            this.updateSucceeded = true;
            this.lastUpdated = DateTime.Now;
            Tracker tracker = this.ChooseTracker();
            WaitHandle handle = tracker.SendUpdate(bytesDownloaded, bytesUploaded, bytesLeft, clientEvent, this.infoHash);

            if (this.UpdateRecieved != null)
                this.UpdateRecieved(this.manager, new TrackerUpdateEventArgs(tracker, null));

            return handle;
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
                this.updateSucceeded = false;
                id.Tracker.UpdateSucceeded = false;

                if (id.Tracker.State == TrackerState.Announcing)
                    id.Tracker.State = TrackerState.AnnouncingFailed;
                else if (id.Tracker.State == TrackerState.Scraping)
                    id.Tracker.State = TrackerState.ScrapingFailed;

                if (this.UpdateRecieved != null)
                    UpdateRecieved(this.manager, new TrackerUpdateEventArgs(id.Tracker, null));
                return;
            }

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

            if (id.Tracker.State == TrackerState.Announcing)
                id.Tracker.State= TrackerState.AnnounceSuccessful;
            else if (id.Tracker.State == TrackerState.Scraping)
                id.Tracker.State = TrackerState.ScrapeSuccessful;

            if (this.UpdateRecieved != null)
                UpdateRecieved(this.manager, new TrackerUpdateEventArgs(id.Tracker, dataStream.ToArray()));
        }


        /// <summary>
        /// If a tracker is unreachable, the next tracker is chosen from the list
        /// </summary>
        /// <returns></returns>
        private Tracker ChooseTracker()
        {
            if (!this.updateSucceeded)
            {
                this.lastUsedTracker++;
                if (this.lastUsedTracker == this.trackers.Length)
                    this.lastUsedTracker = 0;
            }

            return this.trackers[this.lastUsedTracker];
        }

        internal void OnTrackerEvent(object sender, TrackerUpdateEventArgs e)
        {
            this.UpdateRecieved(sender, e);
        }
        #endregion
    }
}