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
using MonoTorrent.Client.Encryption;
using System.Collections;
using System.Threading.Tasks;

namespace MonoTorrent.Client.Tracker
{
    /// <summary>
    /// Represents the connection to a tracker that an TorrentManager has
    /// </summary>
    public class TrackerManager : IEnumerable<TrackerTier>
    {
        #region Member Variables
        private TorrentManager manager;
        IList<TrackerTier> tierList;


        /// <summary>
        /// Returns the tracker that is current in use by the engine
        /// </summary>
        public ITracker CurrentTracker
        {
            get
            {
                if (this.trackerTiers.Count == 0 || this.trackerTiers[0].Trackers.Count == 0)
                    return null;

                return this.trackerTiers[0].Trackers[0];
            }
        }


        /// <summary>
        /// The infohash for the torrent
        /// </summary>
        private InfoHash infoHash;


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
        public IList<TrackerTier> TrackerTiers
        {
            get { return tierList; }
        }
        List<TrackerTier> trackerTiers;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new TrackerConnection for the supplied torrent file
        /// </summary>
        /// <param name="manager">The TorrentManager to create the tracker connection for</param>
        public TrackerManager(TorrentManager manager, InfoHash infoHash, IList<RawTrackerTier> announces)
        {
            this.manager = manager;
            this.infoHash = infoHash;

            // Check if this tracker supports scraping
            trackerTiers = new List<TrackerTier>();
            for (int i = 0; i < announces.Count; i++)
                trackerTiers.Add(new TrackerTier(announces[i]));

            trackerTiers.RemoveAll(delegate(TrackerTier t) { return t.Trackers.Count == 0; });
            tierList = new ReadOnlyCollection<TrackerTier>(trackerTiers);
        }

        #endregion


        #region Methods

        public async Task Announce()
        {
            if (CurrentTracker != null)
                await Announce(trackerTiers[0].SentStartedEvent ? TorrentEvent.None : TorrentEvent.Started);
        }

        internal async Task Announce(TorrentEvent clientEvent)
        {
            if (CurrentTracker != null)
                await Announce(CurrentTracker, clientEvent, true);
        }

        public async Task Announce(Tracker tracker)
        {
            Check.Tracker(tracker);
            TrackerTier tier = trackerTiers.Find(delegate(TrackerTier t) { return t.Trackers.Contains(tracker); });
            if(tier == null)
                throw new ArgumentException("Tracker has not been registered with the manager", "tracker");

            TorrentEvent tevent = tier.SentStartedEvent ? TorrentEvent.None : TorrentEvent.Started;
            await Announce(tracker, tevent , false);
        }


        private async Task Announce(ITracker tracker, TorrentEvent clientEvent, bool trySubsequent)
        {
            ClientEngine engine = manager.Engine;
            
            // If the engine is null, we have been unregistered
            if (engine == null)
                return;

            this.updateSucceeded = true;
            this.lastUpdated = DateTime.Now;

            EncryptionTypes e = engine.Settings.AllowedEncryption;
            bool requireEncryption = !Toolbox.HasEncryption(e, EncryptionTypes.PlainText);
            bool supportsEncryption = Toolbox.HasEncryption(e, EncryptionTypes.RC4Full) || Toolbox.HasEncryption(e, EncryptionTypes.RC4Header);

            requireEncryption = requireEncryption && ClientEngine.SupportsEncryption;
            supportsEncryption = supportsEncryption && ClientEngine.SupportsEncryption;

            IPEndPoint reportedAddress = engine.Settings.ReportedAddress;
            string ip = reportedAddress == null ? null : reportedAddress.Address.ToString();
            int port = reportedAddress == null ? engine.Listener.Endpoint.Port : reportedAddress.Port;

            // FIXME: In metadata mode we need to pretend we need to download data otherwise
            // tracker optimisations might result in no peers being sent back.
            long bytesLeft = 1000;
            if (manager.HasMetadata)
                bytesLeft = (long)((1 - this.manager.Bitfield.PercentComplete / 100.0) * this.manager.Torrent.Size);
            AnnounceParameters p = new AnnounceParameters(this.manager.Monitor.DataBytesDownloaded,
                                                this.manager.Monitor.DataBytesUploaded,
                                                bytesLeft,
                                                clientEvent, this.infoHash, requireEncryption, manager.Engine.PeerId,
                                                ip, port);
            p.SupportsEncryption = supportsEncryption;
            try {
                var peers = await tracker.AnnounceAsync(p);
                await OnAnnounceComplete(tracker, peers, trySubsequent, clientEvent, true);
            } catch {
                 await OnAnnounceComplete (tracker, new List<Peer>(), trySubsequent, clientEvent, false);
            }
        }

        private bool GetNextTracker(ITracker tracker, out TrackerTier trackerTier, out ITracker trackerReturn)
        {
            for (int i = 0; i < this.trackerTiers.Count; i++)
            {
                for (int j = 0; j < this.trackerTiers[i].Trackers.Count; j++)
                {
                    if (this.trackerTiers[i].Trackers[j] != tracker)
                        continue;

                    // If we are on the last tracker of this tier, check to see if there are more tiers
                    if (j == (this.trackerTiers[i].Trackers.Count - 1))
                    {
                        if (i == (this.trackerTiers.Count - 1))
                        {
                            trackerTier = null;
                            trackerReturn = null;
                            return false;
                        }

                        trackerTier = this.trackerTiers[i + 1];
                        trackerReturn = trackerTier.Trackers[0];
                        return true;
                    }

                    trackerTier = this.trackerTiers[i];
                    trackerReturn = trackerTier.Trackers[j + 1];
                    return true;
                }
            }

            trackerTier = null;
            trackerReturn = null;
            return false;
        }

        private async Task OnAnnounceComplete(ITracker tracker, List<Peer> peers, bool trySubsequent, TorrentEvent clientEvent, bool successful)
        {
            this.updateSucceeded = successful;
            if (manager.Engine == null)
                return;

            if (successful)
            {
                manager.Peers.BusyPeers.Clear ();
                int count = await manager.AddPeersAsync(peers);
                manager.RaisePeersFound(new TrackerPeersAdded(manager, count, peers.Count, tracker));

                TrackerTier tier = trackerTiers.Find(delegate(TrackerTier t) { return t.Trackers.Contains(tracker); });
                if (tier != null)
                {
                    Toolbox.Switch(tier.Trackers, 0, tier.IndexOf(tracker));
                    Toolbox.Switch(trackerTiers, 0, trackerTiers.IndexOf(tier));
                }
            }
            else
            {
                TrackerTier tier;
                if (!trySubsequent || !GetNextTracker(tracker, out tier, out tracker))
                    return;
                else
                    await Announce(tracker, clientEvent, true);
            }
        }

        public async Task Scrape()
        {
            if (CurrentTracker != null)
                await Scrape(CurrentTracker);
        }

        public async Task Scrape(ITracker tracker)
        {
            TrackerTier tier = trackerTiers.Find(delegate(TrackerTier t) { return t.Trackers.Contains(tracker); });
            if (tier == null)
                return;

            if (tracker == null)
                throw new ArgumentNullException("tracker");

            if (!tracker.CanScrape)
                throw new TorrentException("This tracker does not support scraping");

            await tracker.ScrapeAsync(new ScrapeParameters(this.infoHash));
        }

        #endregion

        public IEnumerator<TrackerTier> GetEnumerator()
        {
            return trackerTiers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
