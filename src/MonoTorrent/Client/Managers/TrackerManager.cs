using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    /// <summary>
    ///     Represents the connection to a tracker that an TorrentManager has
    /// </summary>
    public class TrackerManager : IEnumerable<TrackerTier>
    {
        #region Constructors

        /// <summary>
        ///     Creates a new TrackerConnection for the supplied torrent file
        /// </summary>
        /// <param name="manager">The TorrentManager to create the tracker connection for</param>
        public TrackerManager(TorrentManager manager, InfoHash infoHash, IList<RawTrackerTier> announces)
        {
            this.manager = manager;
            this.infoHash = infoHash;

            // Check if this tracker supports scraping
            trackerTiers = new List<TrackerTier>();
            for (var i = 0; i < announces.Count; i++)
                trackerTiers.Add(new TrackerTier(announces[i]));

            trackerTiers.RemoveAll(delegate(TrackerTier t) { return t.Trackers.Count == 0; });
            foreach (var tier in trackerTiers)
            {
                foreach (var tracker in tier)
                {
                    tracker.AnnounceComplete +=
                        delegate(object o, AnnounceResponseEventArgs e)
                        {
                            ClientEngine.MainLoop.Queue(delegate { OnAnnounceComplete(o, e); });
                        };

                    tracker.ScrapeComplete +=
                        delegate(object o, ScrapeResponseEventArgs e)
                        {
                            ClientEngine.MainLoop.Queue(delegate { OnScrapeComplete(o, e); });
                        };
                }
            }

            TrackerTiers = new ReadOnlyCollection<TrackerTier>(trackerTiers);
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

        #region Member Variables

        private readonly TorrentManager manager;


        /// <summary>
        ///     Returns the tracker that is current in use by the engine
        /// </summary>
        public Tracker CurrentTracker
        {
            get
            {
                if (trackerTiers.Count == 0 || trackerTiers[0].Trackers.Count == 0)
                    return null;

                return trackerTiers[0].Trackers[0];
            }
        }


        /// <summary>
        ///     The infohash for the torrent
        /// </summary>
        private readonly InfoHash infoHash;


        /// <summary>
        ///     True if the last update succeeded
        /// </summary>
        public bool UpdateSucceeded { get; private set; }


        /// <summary>
        ///     The time the last tracker update was sent to any tracker
        /// </summary>
        public DateTime LastUpdated { get; private set; }


        /// <summary>
        ///     The trackers available
        /// </summary>
        public IList<TrackerTier> TrackerTiers { get; }

        private readonly List<TrackerTier> trackerTiers;

        #endregion

        #region Methods

        public WaitHandle Announce()
        {
            if (CurrentTracker == null)
                return new ManualResetEvent(true);

            return Announce(trackerTiers[0].SentStartedEvent ? TorrentEvent.None : TorrentEvent.Started);
        }

        public WaitHandle Announce(Tracker tracker)
        {
            Check.Tracker(tracker);
            var tier = trackerTiers.Find(delegate(TrackerTier t) { return t.Trackers.Contains(tracker); });
            if (tier == null)
                throw new ArgumentException("Tracker has not been registered with the manager", "tracker");

            var tevent = tier.SentStartedEvent ? TorrentEvent.None : TorrentEvent.Started;
            return Announce(tracker, tevent, false, new ManualResetEvent(false));
        }

        internal WaitHandle Announce(TorrentEvent clientEvent)
        {
            if (CurrentTracker == null)
                return new ManualResetEvent(true);
            return Announce(CurrentTracker, clientEvent, true, new ManualResetEvent(false));
        }

        private WaitHandle Announce(Tracker tracker, TorrentEvent clientEvent, bool trySubsequent,
            ManualResetEvent waitHandle)
        {
            var engine = manager.Engine;

            // If the engine is null, we have been unregistered
            if (engine == null)
            {
                waitHandle.Set();
                return waitHandle;
            }

            UpdateSucceeded = true;
            LastUpdated = DateTime.Now;

            var e = engine.Settings.AllowedEncryption;
            var requireEncryption = !Toolbox.HasEncryption(e, EncryptionTypes.PlainText);
            var supportsEncryption = Toolbox.HasEncryption(e, EncryptionTypes.RC4Full) ||
                                     Toolbox.HasEncryption(e, EncryptionTypes.RC4Header);

            requireEncryption = requireEncryption && ClientEngine.SupportsEncryption;
            supportsEncryption = supportsEncryption && ClientEngine.SupportsEncryption;

            var reportedAddress = engine.Settings.ReportedAddress;
            var ip = reportedAddress == null ? null : reportedAddress.Address.ToString();
            var port = reportedAddress == null ? engine.Listener.Endpoint.Port : reportedAddress.Port;

            // FIXME: In metadata mode we need to pretend we need to download data otherwise
            // tracker optimisations might result in no peers being sent back.
            long bytesLeft = 1000;
            if (manager.HasMetadata)
                bytesLeft = (long) ((1 - manager.Bitfield.PercentComplete/100.0)*manager.Torrent.Size);
            var p = new AnnounceParameters(manager.Monitor.DataBytesDownloaded,
                manager.Monitor.DataBytesUploaded,
                bytesLeft,
                clientEvent, infoHash, requireEncryption, manager.Engine.PeerId,
                ip, port);
            p.SupportsEncryption = supportsEncryption;
            var id = new TrackerConnectionID(tracker, trySubsequent, clientEvent, waitHandle);
            tracker.Announce(p, id);
            return waitHandle;
        }

        private bool GetNextTracker(Tracker tracker, out TrackerTier trackerTier, out Tracker trackerReturn)
        {
            for (var i = 0; i < trackerTiers.Count; i++)
            {
                for (var j = 0; j < trackerTiers[i].Trackers.Count; j++)
                {
                    if (trackerTiers[i].Trackers[j] != tracker)
                        continue;

                    // If we are on the last tracker of this tier, check to see if there are more tiers
                    if (j == trackerTiers[i].Trackers.Count - 1)
                    {
                        if (i == trackerTiers.Count - 1)
                        {
                            trackerTier = null;
                            trackerReturn = null;
                            return false;
                        }

                        trackerTier = trackerTiers[i + 1];
                        trackerReturn = trackerTier.Trackers[0];
                        return true;
                    }

                    trackerTier = trackerTiers[i];
                    trackerReturn = trackerTier.Trackers[j + 1];
                    return true;
                }
            }

            trackerTier = null;
            trackerReturn = null;
            return false;
        }

        private void OnScrapeComplete(object sender, ScrapeResponseEventArgs e)
        {
            e.Id.WaitHandle.Set();
        }

        private void OnAnnounceComplete(object sender, AnnounceResponseEventArgs e)
        {
            UpdateSucceeded = e.Successful;
            if (manager.Engine == null)
            {
                e.Id.WaitHandle.Set();
                return;
            }

            if (e.Successful)
            {
                manager.Peers.BusyPeers.Clear();
                var count = manager.AddPeersCore(e.Peers);
                manager.RaisePeersFound(new TrackerPeersAdded(manager, count, e.Peers.Count, e.Tracker));

                var tier = trackerTiers.Find(delegate(TrackerTier t) { return t.Trackers.Contains(e.Tracker); });
                if (tier != null)
                {
                    Toolbox.Switch(tier.Trackers, 0, tier.IndexOf(e.Tracker));
                    Toolbox.Switch(trackerTiers, 0, trackerTiers.IndexOf(tier));
                }
                e.Id.WaitHandle.Set();
            }
            else
            {
                TrackerTier tier;
                Tracker tracker;

                if (!e.Id.TrySubsequent || !GetNextTracker(e.Tracker, out tier, out tracker))
                    e.Id.WaitHandle.Set();
                else
                    Announce(tracker, e.Id.TorrentEvent, true, e.Id.WaitHandle);
            }
        }

        public WaitHandle Scrape()
        {
            if (CurrentTracker == null)
                return new ManualResetEvent(true);
            return Scrape(CurrentTracker, false);
        }

        public WaitHandle Scrape(Tracker tracker)
        {
            var tier = trackerTiers.Find(delegate(TrackerTier t) { return t.Trackers.Contains(tracker); });
            if (tier == null)
                return new ManualResetEvent(true);

            return Scrape(tracker, false);
        }

        private WaitHandle Scrape(Tracker tracker, bool trySubsequent)
        {
            if (tracker == null)
                throw new ArgumentNullException("tracker");

            if (!tracker.CanScrape)
                throw new TorrentException("This tracker does not support scraping");

            var id = new TrackerConnectionID(tracker, trySubsequent, TorrentEvent.None,
                new ManualResetEvent(false));
            tracker.Scrape(new ScrapeParameters(infoHash), id);
            return id.WaitHandle;
        }

        #endregion
    }
}