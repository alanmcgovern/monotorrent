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
        public ITracker CurrentTracker => Tiers.SelectMany (t => t.Trackers).OrderBy (t => t.TimeSinceLastAnnounce).FirstOrDefault ();

        /// <summary>
        /// True if the most recent Announce request was successful.
        /// </summary>
        public bool LastAnnounceSucceeded { get; private set; }

        /// <summary>
        /// The timer tracking the time since the most recent Announce request was sent.
        /// </summary>
        ValueStopwatch LastAnnounce;

        /// <summary>
        /// The time, in UTC, when the most recent Announce request was sent
        /// </summary>
        public DateTime LastUpdated { get; private set; }

        /// <summary>
        /// The TorrentManager associated with this tracker
        /// </summary>
        ITrackerRequestFactory RequestFactory { get; set; }

        /// <summary>
        /// The available trackers.
        /// </summary>
        public IList<TrackerTier> Tiers { get; }

        /// <summary>
        /// The amount of time since the most recent Announce request was issued.
        /// </summary>
        public TimeSpan TimeSinceLastAnnounce => LastAnnounce.IsRunning ? LastAnnounce.Elapsed : TimeSpan.MaxValue;

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
            LastAnnounce = new ValueStopwatch ();

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
        {
            await Announce (TorrentEvent.None);
        }

        public async Task Announce (TorrentEvent clientEvent)
        {
            await Announce (clientEvent, null);
        }

        public async Task Announce (ITracker tracker)
        {
            Check.Tracker (tracker);
            await Announce (TorrentEvent.None, tracker);
        }

        async Task Announce (TorrentEvent clientEvent, ITracker referenceTracker)
        {
            // If the user initiates an Announce we need to go to the correct thread to process it.
            await ClientEngine.MainLoop;

            LastAnnounce.Restart ();
            LastUpdated = DateTime.UtcNow;

            AnnounceParameters p = RequestFactory.CreateAnnounce (clientEvent);

            foreach ((TrackerTier trackerTier, ITracker tracker) in GetNextTracker (referenceTracker)) {
                try {
                    // If we have not announced to this Tracker tier yet then we should replace the ClientEvent.
                    // But if we end up announcing to a different Tracker tier we may want to send the
                    // original/unmodified args.
                    AnnounceParameters actualArgs = p;
                    if (!trackerTier.SentStartedEvent)
                        actualArgs = actualArgs.WithClientEvent (TorrentEvent.Started);

                    List<Peer> peers = await tracker.AnnounceAsync (actualArgs);
                    LastAnnounceSucceeded = true;
                    AnnounceComplete?.InvokeAsync (this, new AnnounceResponseEventArgs (tracker, true, peers.AsReadOnly ()));
                    return;
                } catch {
                }
            }

            LastAnnounceSucceeded = false;
            AnnounceComplete?.InvokeAsync (this, new AnnounceResponseEventArgs (null, false));
        }

        public async Task Scrape ()
        {
            await Scrape (null);
        }

        public async Task Scrape (ITracker tracker)
        {
            // If the user initiates a Scrape we need to go to the correct thread to process it.
            await ClientEngine.MainLoop;

            Tuple<TrackerTier, ITracker> tuple = GetNextTracker (tracker).FirstOrDefault ();
            if (tuple != null && !tuple.Item2.CanScrape)
                throw new TorrentException ("This tracker does not support scraping");
            try {
                ScrapeParameters parameters = RequestFactory.CreateScrape ();
                await tuple.Item2.ScrapeAsync (parameters);
                ScrapeComplete?.InvokeAsync (this, new ScrapeResponseEventArgs (tuple.Item2, true));
            } catch {
                ScrapeComplete?.InvokeAsync (this, new ScrapeResponseEventArgs (tuple.Item2, false));
            }
        }

        IEnumerable<Tuple<TrackerTier, ITracker>> GetNextTracker (ITracker referenceTracker)
        {
            foreach (TrackerTier tier in Tiers)
                foreach (ITracker tracker in tier.Trackers.OrderBy (t => t.TimeSinceLastAnnounce))
                    if (referenceTracker == null || referenceTracker == tracker)
                        yield return Tuple.Create (tier, tracker);
        }

        #endregion
    }
}
