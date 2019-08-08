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
using System.Linq;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using MonoTorrent.Client.Encryption;
using MonoTorrent.Common;

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
        public ITracker CurrentTracker => Tiers.SelectMany (t => t.Trackers).OrderBy (t => t.TimeSinceLastAnnounce).FirstOrDefault ();

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
        public IList<TrackerTier> Tiers { get; }

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
            var trackerTiers = new List<TrackerTier>();
            for (int i = 0; i < announces.Count; i++)
                trackerTiers.Add(new TrackerTier(announces[i]));

            trackerTiers.RemoveAll(delegate(TrackerTier t) { return t.Trackers.Count == 0; });
            Tiers = trackerTiers.AsReadOnly ();
        }

        #endregion


        #region Methods

        public async Task Announce()
            => await Announce (TorrentEvent.None);

        internal async Task Announce(TorrentEvent clientEvent)
            => await Announce (clientEvent, null);

        public async Task Announce(Tracker tracker)
        {
            Check.Tracker(tracker);
            await Announce(TorrentEvent.None, tracker);
        }

        async Task Announce(TorrentEvent clientEvent, ITracker referenceTracker)
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
                                                ip, port, supportsEncryption);

            foreach (var tuple in GetNextTracker (referenceTracker)) {
                try {
                    // If we have not announced to this Tracker tier yet then we should replace the ClientEvent.
                    // But if we end up announcing to a different Tracker tier we may want to send the
                    // original/unmodified args.
                    var actualArgs = p;
                    if (!tuple.Item1.SentStartedEvent)
                        actualArgs = actualArgs.WithClientEvent (TorrentEvent.Started);

                    var peers = await tuple.Item2.AnnounceAsync(actualArgs);
                    manager.Peers.BusyPeers.Clear ();
                    int count = await manager.AddPeersAsync(peers);
                    manager.RaisePeersFound(new TrackerPeersAdded(manager, count, peers.Count, tuple.Item2));

                    return;
                } catch {

                }
            }

            updateSucceeded = false;
        }

        public async Task Scrape()
        {
            await Scrape (null);
        }

        public async Task Scrape(ITracker tracker)
        {
            var tuple = GetNextTracker (tracker).FirstOrDefault ();
            if (tuple != null && !tuple.Item2.CanScrape)
                throw new TorrentException("This tracker does not support scraping");
            await tuple.Item2.ScrapeAsync(new ScrapeParameters(manager.InfoHash));
        }

        IEnumerable<Tuple<TrackerTier, ITracker>> GetNextTracker (ITracker referenceTracker)
        {
            foreach (var tier in Tiers)
                foreach (var tracker in tier.Trackers.OrderBy (t => t.TimeSinceLastAnnounce))
                    if (referenceTracker == null || referenceTracker == tracker)
                        yield return Tuple.Create (tier, tracker);
        }

        #endregion
    }
}
