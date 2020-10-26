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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MonoTorrent.Client.Tracker
{
    /// <summary>
    /// Represents the connection to a tracker that an TorrentManager has
    /// </summary>
    public class TrackerManager : ITrackerManager
    {
        public event EventHandler<AnnounceResponseEventArgs> AnnounceComplete;
        public event EventHandler<ScrapeResponseEventArgs> ScrapeComplete;

        #region Member Variables

        /// <summary>
        /// Returns the tracker which will be used, by default, for Announce or Scrape requests.
        /// </summary>
        [Obsolete("This is now a per-Tier value and should be accessed using TrackerTier.ActiveTracker.")]
        public ITracker CurrentTracker => Tiers.SelectMany (t => t.Trackers).OrderBy (t => t.TimeSinceLastAnnounce).FirstOrDefault ();

#pragma warning disable CS0618 // Type or member is obsolete
        TrackerTier CurrentTier => Tiers.FirstOrDefault (t => t.Trackers.Contains (CurrentTracker));
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>
        /// True if the most recent Announce request was successful.
        /// </summary>
        [Obsolete ("This is now a per-Tier value and should be accessed using TrackerTier.LastAnnounceSucceeded.")]
        public bool LastAnnounceSucceeded => CurrentTier?.LastAnnounceSucceeded ?? false;

        /// <summary>
        /// The time, in UTC, when the most recent Announce request was sent
        /// </summary>
        public DateTime LastUpdated => CurrentTier?.LastUpdated ?? DateTime.MaxValue;

        /// <summary>
        /// The TorrentManager associated with this tracker
        /// </summary>
        ITrackerRequestFactory RequestFactory { get; }

        /// <summary>
        /// The available trackers.
        /// </summary>
        public IList<TrackerTier> Tiers { get; }

        /// <summary>
        /// The amount of time since the most recent Announce request was issued.
        /// </summary>
        public TimeSpan TimeSinceLastAnnounce => CurrentTier?.TimeSinceLastAnnounce ?? TimeSpan.Zero;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new TrackerConnection for the supplied torrent file
        /// </summary>
        /// <param name="requestFactory">The factory used to create tracker requests. Typically a <see cref="TorrentManager"/> instance.</param>
        /// <param name="announces">The list of tracker tiers</param>
        internal TrackerManager (ITrackerRequestFactory requestFactory, IEnumerable<RawTrackerTier> announces)
        {
            RequestFactory = requestFactory;

            // Check if this tracker supports scraping
            var trackerTiers = new List<TrackerTier> ();
            foreach (RawTrackerTier tier in announces)
                trackerTiers.Add (new TrackerTier (tier));
            trackerTiers.RemoveAll (tier => tier.Trackers.Count == 0);
            Tiers = trackerTiers.AsReadOnly ();
        }

        #endregion


        #region Methods

        public async Task Announce ()
            => await Announce (TorrentEvent.None);

        public async Task Announce (TorrentEvent clientEvent)
        {
            // If the user initiates an Announce we need to go to the correct thread to process it.
            await ClientEngine.MainLoop;

            var announces = new List<Task> ();
            for (int i = 0; i < Tiers.Count; i++) {
                var tier = Tiers[i];
                var tracker = tier.ActiveTracker;
                var interval = tier.LastAnnounceSucceeded ? tracker.UpdateInterval : tracker.MinUpdateInterval;
                if (tier.TimeSinceLastAnnounce > interval && (clientEvent != TorrentEvent.Stopped || tier.LastAnnounceSucceeded))
                    announces.Add (AnnounceToTier (clientEvent, tier));
            }
            await Task.WhenAll (announces);
        }

        
        async Task AnnounceToTier (TorrentEvent clientEvent, TrackerTier tier)
        {
            for (int i = 0; i < tier.Trackers.Count; i++) {
                int trackerIndex = (i + tier.ActiveTrackerIndex) % tier.Trackers.Count;
                var tracker = tier.Trackers[trackerIndex];

                // We should really wait til after the announce to reset the timer. However
                // there is no way to prevent us from announcing multiple times concurrently
                // to the same tracker without resetting this timer. Our logic is completely
                // dependent on 'time since last announce'
                tier.LastAnnounce = ValueStopwatch.StartNew ();
                var result = await Announce (clientEvent, tracker);
                if (result) {
                    tier.ActiveTrackerIndex = trackerIndex;
                    tier.LastAnnounceSucceeded = true;
                    return;
                }
            }

            // All trackers failed to respond.
            tier.LastAnnounceSucceeded = false;
        }

        public async Task Announce (ITracker tracker)
        {
            Check.Tracker (tracker);

            // If the user initiates an Announce we need to go to the correct thread to process it.
            await ClientEngine.MainLoop;
            await Announce (TorrentEvent.None, tracker);
        }

        async Task<bool> Announce (TorrentEvent clientEvent, ITracker tracker)
        {
            var trackerTier = Tiers.First (t => t.Trackers.Contains (tracker));
            try {
                // If we have not announced to this Tracker tier yet then we should replace the ClientEvent.
                // But if we end up announcing to a different Tracker tier we may want to send the
                // original/unmodified args.
                AnnounceParameters actualArgs = RequestFactory.CreateAnnounce (clientEvent);
                if (!trackerTier.SentStartedEvent)
                    actualArgs = actualArgs.WithClientEvent (TorrentEvent.Started);

                List<Peer> peers = await tracker.AnnounceAsync (actualArgs);
                trackerTier.LastAnnounceSucceeded = true;

                trackerTier.ActiveTrackerIndex = trackerTier.Trackers.IndexOf (tracker);
                trackerTier.SentStartedEvent |= actualArgs.ClientEvent == TorrentEvent.Started;
                trackerTier.LastAnnounce = ValueStopwatch.StartNew ();
                AnnounceComplete?.InvokeAsync (this, new AnnounceResponseEventArgs (tracker, true, peers.AsReadOnly ()));
                return true;
            } catch {
            }

            trackerTier.LastAnnounceSucceeded = false;
            AnnounceComplete?.InvokeAsync (this, new AnnounceResponseEventArgs (tracker, false));
            return false;
        }

        public async Task Scrape ()
        {
            // If the user initiates a Scrape we need to go to the correct thread to process it.
            await ClientEngine.MainLoop;

            var scrapes = new List<Task> ();
            for (int i = 0; i < Tiers.Count; i++) {
                var tier = Tiers[i];
                var tracker = tier.ActiveTracker;
                if (tier.TimeSinceLastScrape > tracker.UpdateInterval)
                    scrapes.Add (ScrapeTier (tier));
            }
            await Task.WhenAll (scrapes);

        }

        async Task ScrapeTier (TrackerTier tier)
        {
            for (int i = 0; i < tier.Trackers.Count; i++) {
                int trackerIndex = (i + tier.ActiveTrackerIndex) % tier.Trackers.Count;
                var tracker = tier.Trackers[trackerIndex];

                tier.LastScrape = ValueStopwatch.StartNew ();
                if (tracker.CanScrape)
                    await Scrape (tracker);

                if (tier.LastScrapSucceeded)
                    break;
            }
            // All trackers failed to respond.
        }

        public async Task Scrape (ITracker tracker)
        {
            if (!tracker.CanScrape)
                throw new TorrentException ("This tracker does not support scraping");

            // If the user initiates a Scrape we need to go to the correct thread to process it.
            await ClientEngine.MainLoop;

            var trackerTier = Tiers.First (t => t.Trackers.Contains (tracker));
            trackerTier.LastScrape = ValueStopwatch.StartNew ();

            try {
                ScrapeParameters parameters = RequestFactory.CreateScrape ();
                await tracker.ScrapeAsync (parameters);
                trackerTier.LastScrapSucceeded = true;
                ScrapeComplete?.InvokeAsync (this, new ScrapeResponseEventArgs (tracker, true));
            } catch {
                trackerTier.LastScrapSucceeded = false;
                ScrapeComplete?.InvokeAsync (this, new ScrapeResponseEventArgs (tracker, false));
            }
        }

        #endregion
    }
}
