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
using System.Diagnostics;
using System.Collections.Generic;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.Tracker
{
    /// <summary>
    /// Represents the connection to a tracker that an TorrentManager has
    /// </summary>
    public class TrackerManager
    {
        #region Member Variables
        private TorrentManager manager;


        /// <summary>
        /// Returns the tracker that is current in use by the engine
        /// </summary>
        public Tracker CurrentTracker
        {
            get
            {
                if (this.trackerTiers.Length == 0 || this.trackerTiers[0].Trackers.Length == 0)
                    return null;

                return this.trackerTiers[0].Trackers[0];
            }
        }


        /// <summary>
        /// The infohash for the torrent
        /// </summary>
        private byte[] infoHash;


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
        /// The trackers available
        /// </summary>
        public TrackerTier[] TrackerTiers
        {
            get { return this.trackerTiers; }
        }
        private TrackerTier[] trackerTiers;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new TrackerConnection for the supplied torrent file
        /// </summary>
        /// <param name="manager">The TorrentManager to create the tracker connection for</param>
        public TrackerManager(TorrentManager manager)
        {
            this.manager = manager;
            this.infoHash = new byte[20];
            Buffer.BlockCopy(manager.Torrent.infoHash, 0, infoHash, 0, 20);

            // Check if this tracker supports scraping
            this.trackerTiers = new TrackerTier[manager.Torrent.AnnounceUrls.Count];
            for (int i = 0; i < manager.Torrent.AnnounceUrls.Count; i++)
                this.trackerTiers[i] = new TrackerTier(manager.Torrent.AnnounceUrls[i]);

            foreach (TrackerTier tier in trackerTiers)
            {
                foreach (Tracker tracker in tier)
                {
                    tracker.AnnounceComplete += new EventHandler<AnnounceResponseEventArgs>(OnAnnounceComplete);
                    tracker.ScrapeComplete += new EventHandler<ScrapeResponseEventArgs>(OnScrapeComplete);
                }
            }
        }

        #endregion


        #region Methods

        public WaitHandle Announce()
        {
            return Announce(TorrentEvent.None);
        }

        /// <summary>
        /// Sends a status update to the tracker
        /// </summary>
        /// <param name="bytesDownloaded">The number of bytes downloaded since the last update</param>
        /// <param name="bytesUploaded">The number of bytes uploaded since the last update</param>
        /// <param name="bytesLeft">The number of bytes left to download</param>
        /// <param name="clientEvent">The Event (if any) that represents this update</param>
        internal WaitHandle Announce(TorrentEvent clientEvent)
        {
            return Announce(CurrentTracker, clientEvent, true);
        }

        public WaitHandle Announce(TrackerTier tier, Tracker tracker)
        {
            return Announce(tracker, TorrentEvent.None, false);
        }

        private WaitHandle Announce(Tracker tracker, TorrentEvent clientEvent, bool trySubsequent)
        {
            return Announce(tracker, clientEvent, trySubsequent, new ManualResetEvent(false));
        }

        private WaitHandle Announce(Tracker tracker, TorrentEvent clientEvent, bool trySubsequent, ManualResetEvent waitHandle)
        {
            ClientEngine engine = manager.Engine;
            
            // If the engine is null, we have been unregistered
            if (engine == null)
            {
                waitHandle.Set();
                return waitHandle;
            }

            TrackerConnectionID id = new TrackerConnectionID(tracker, trySubsequent, clientEvent, null, waitHandle);
            this.updateSucceeded = true;
            this.lastUpdated = DateTime.Now;
            
            bool supportsEncryption = ClientEngine.SupportsEncryption && engine.Settings.MinEncryptionLevel != EncryptionType.None;

            AnnounceParameters p = new AnnounceParameters(this.manager.Monitor.DataBytesDownloaded,
                                                this.manager.Monitor.DataBytesUploaded,
                                                (long)((1 - this.manager.Bitfield.PercentComplete / 100.0) * this.manager.Torrent.Size),
                                                clientEvent, this.infoHash, id, supportsEncryption, manager.Engine.PeerId,
                                                null, manager.Engine.Settings.ListenPort);
            tracker.LastUpdated = DateTime.Now;
            tracker.Announce(p);
            return id.WaitHandle;
        }

        private void GetNextTracker(Tracker tracker, out TrackerTier trackerTier, out Tracker trackerReturn)
        {
            for (int i = 0; i < this.trackerTiers.Length; i++)
            {
                for (int j = 0; j < this.trackerTiers[i].Trackers.Length; j++)
                {
                    if (this.trackerTiers[i].Trackers[j] != tracker)
                        continue;

                    // If we are on the last tracker of this tier, check to see if there are more tiers
                    if (j == (this.trackerTiers[i].Trackers.Length - 1))
                    {
                        if (i == (this.trackerTiers.Length - 1))
                        {
                            trackerTier = null;
                            trackerReturn = null;
                            return;
                        }

                        trackerTier = this.trackerTiers[i + 1];
                        trackerReturn = trackerTier.Trackers[0];
                        return;
                    }

                    trackerTier = this.trackerTiers[i];
                    trackerReturn = trackerTier.Trackers[j + 1];
                    return;
                }
            }

            trackerTier= null;
            trackerReturn = null;
        }

        private void OnScrapeComplete(object sender, ScrapeResponseEventArgs e)
        {
            // No need to do anything here.
        }

        private void OnAnnounceComplete(object sender, AnnounceResponseEventArgs e)
        {
            this.updateSucceeded = e.Successful;

            if (e.Successful)
            {
                e.TrackerId.WaitHandle.Set();
                // FIXME: Figure out why manually firing the event throws an exception here
                Toolbox.Switch<Tracker>(e.TrackerId.Tracker.Tier.Trackers, 0, e.TrackerId.Tracker.Tier.IndexOf(e.Tracker));
                manager.AddPeers(e.Peers);
            }
            else
            {
                TrackerTier tier;
                Tracker tracker;
                GetNextTracker(e.TrackerId.Tracker, out tier, out tracker);

                if (!e.TrackerId.TrySubsequent || tier == null || tracker == null)
                {
                    e.TrackerId.WaitHandle.Set();
                    return;
                }
                Announce(tracker, e.TrackerId.TorrentEvent, true, e.TrackerId.WaitHandle);
            }
        }

        /// <summary>
        /// Scrapes the first tracker for peer information.
        /// </summary>
        /// <param name="requestSingle">True if you want scrape information for just the torrent in the TorrentManager. False if you want everything on the tracker</param>
        /// <returns></returns>
        public WaitHandle Scrape()
        {
            return Scrape(this.trackerTiers[0].Trackers[0], true);
        }

        /// <summary>
        /// Scrapes the specified tracker for peer information.
        /// </summary>
        /// <param name="requestSingle">True if you want scrape information for just the torrent in the TorrentManager. False if you want everything on the tracker</param>
        /// <returns></returns>
        public WaitHandle Scrape(Tracker tracker)
        {
            return Scrape(tracker, false);
        }

        private WaitHandle Scrape(Tracker tracker, bool trySubsequent)
        {
            if (!tracker.CanScrape)
                throw new TorrentException("This tracker does not support scraping");

            TrackerConnectionID id = new TrackerConnectionID(tracker, trySubsequent, TorrentEvent.None, null);
            WaitHandle handle = tracker.Scrape(this.infoHash, id);
            
            return handle;
        }

        #endregion
    }
}
