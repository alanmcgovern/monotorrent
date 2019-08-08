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
using System.Diagnostics;

namespace MonoTorrent.Client.Tracker
{
    /// <summary>
    /// Represents the connection to a tracker that an TorrentManager has
    /// </summary>
    public class TrackerManager
    {
        public event EventHandler<AnnounceResponseEventArgs> AnnounceComplete;
        public event EventHandler<ScrapeResponseEventArgs> ScrapeComplete;

        #region Member Variables

        /// <summary>
        /// Returns the tracker which responded to the most recent Announce request.
        /// </summary>
        public ITracker CurrentTracker => Tiers.SelectMany (t => t.Trackers).OrderBy (t => t.TimeSinceLastAnnounce).FirstOrDefault ();

        /// <summary>
        /// True if the most recent Announce request was successful.
        /// </summary>
        public bool LastAnnounceSucceeded { get; private set; }

        /// <summary>
        /// The timer tracking the time since the most recent Announce request was sent.
        /// </summary>
        Stopwatch LastAnnounce { get; }

        /// <summary>
        /// The time, in UTC, when the most recent Announce request was sent
        /// </summary>
        public DateTime LastUpdated { get; private set; }

        /// <summary>
        /// The TorrentManager associated with this tracker
        /// </summary>
        TorrentManager Manager { get; set; }

        /// <summary>
        /// The available trackers.
        /// </summary>
        public IList<TrackerTier> Tiers { get; }

        /// <summary>
        /// The amount of time since the most recent Announce request was issued.
        /// </summary>
        internal TimeSpan TimeSinceLastAnnounce => LastAnnounce.IsRunning ? LastAnnounce.Elapsed : TimeSpan.MaxValue;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new TrackerConnection for the supplied torrent file
        /// </summary>
        /// <param name="manager">The TorrentManager to create the tracker connection for</param>
        public TrackerManager(TorrentManager manager, IList<RawTrackerTier> announces)
        {
            Manager = manager;
            LastAnnounce = new Stopwatch ();

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

        public async Task Announce(ITracker tracker)
        {
            Check.Tracker(tracker);
            await Announce(TorrentEvent.None, tracker);
        }

        async Task Announce(TorrentEvent clientEvent, ITracker referenceTracker)
        {
            // If the user initiates an Announce we need to go to the correct thread to process it.
            await ClientEngine.MainLoop;

            ClientEngine engine = Manager.Engine;
            
            // If the engine is null, we have been unregistered
            if (engine == null)
                return;

            LastAnnounce.Restart ();
            LastUpdated = DateTime.UtcNow;

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
            if (Manager.HasMetadata)
                bytesLeft = (long)((1 - Manager.Bitfield.PercentComplete / 100.0) * Manager.Torrent.Size);

            AnnounceParameters p = new AnnounceParameters(Manager.Monitor.DataBytesDownloaded,
                                                Manager.Monitor.DataBytesUploaded,
                                                bytesLeft,
                                                clientEvent, Manager.InfoHash, requireEncryption, Manager.Engine.PeerId,
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
                    Manager.Peers.BusyPeers.Clear ();
                    int count = await Manager.AddPeersAsync(peers);
                    Manager.RaisePeersFound(new TrackerPeersAdded(Manager, count, peers.Count, tuple.Item2));

                    LastAnnounceSucceeded = true;
                    Toolbox.RaiseAsyncEvent (AnnounceComplete, this, new AnnounceResponseEventArgs (tuple.Item2, true, peers));
                    return;
                } catch {
                }
            }

            LastAnnounceSucceeded = false;
            Toolbox.RaiseAsyncEvent (AnnounceComplete, this, new AnnounceResponseEventArgs (null, false));
        }

        public async Task Scrape()
        {
            await Scrape (null);
        }

        public async Task Scrape(ITracker tracker)
        {
            // If the user initiates a Scrape we need to go to the correct thread to process it.
            await ClientEngine.MainLoop;

            var tuple = GetNextTracker (tracker).FirstOrDefault ();
            if (tuple != null && !tuple.Item2.CanScrape)
                throw new TorrentException("This tracker does not support scraping");
            try {
                await tuple.Item2.ScrapeAsync(new ScrapeParameters(Manager.InfoHash));
                Toolbox.RaiseAsyncEvent (ScrapeComplete, this, new ScrapeResponseEventArgs (tuple.Item2, true));
            } catch {
                Toolbox.RaiseAsyncEvent (ScrapeComplete, this, new ScrapeResponseEventArgs (tuple.Item2, false));
            }
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
