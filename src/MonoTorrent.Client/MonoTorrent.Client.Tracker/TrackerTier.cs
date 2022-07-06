//
// TrackerTier.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
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
using System.Threading;

using MonoTorrent.Logging;

using ReusableTasks;

namespace MonoTorrent.Trackers
{
    public class TrackerTier
    {
        static readonly Logger logger = Logger.Create (nameof (TrackerTier));

        internal event EventHandler<AnnounceResponseEventArgs>? AnnounceComplete;
        internal event EventHandler<ScrapeResponseEventArgs>? ScrapeComplete;

        /// <summary>
        /// The <see cref="ITracker"/> which Announce and Scrape requests will be sent
        /// to by default.
        /// </summary>
        public ITracker ActiveTracker => Trackers[ActiveTrackerIndex];

        /// <summary>
        /// A readonly list of all trackers contained within this Tier.
        /// </summary>
        public IList<ITracker> Trackers { get; private set; }

        /// <summary>
        /// The <see cref="ScrapeInfo"/> dictionary containing information about each infohash associated with this torrent.
        /// </summary>
        public Dictionary<InfoHash, ScrapeInfo> ScrapeInfo => LastScrapeResponse.ScrapeInfo ?? new Dictionary<InfoHash, ScrapeInfo> ();

        /// <summary>
        /// Returns true if the the most recent Announce was successful and <see cref="ITracker.UpdateInterval"/> seconds
        /// have passed, or if <see cref="ITracker.MinUpdateInterval"/> seconds have passed and the Announce was unsuccessful.
        /// Otherwise returns false.
        /// </summary>
        bool CanSendAnnounce {
            get {
                // NOTE: All trackers in a tier are supposed to be identical load balancers. As such we can
                // assume all trackers have the same update intervals.
                if (LastAnnounceSucceeded && TimeSinceLastAnnounce < ActiveTracker.UpdateInterval)
                    return false;
                if (!LastAnnounceSucceeded && TimeSinceLastAnnounce < ActiveTracker.MinUpdateInterval)
                    return false;
                return true;
            }
        }

        /// <summary>
        /// Returns true if <see cref="ITracker.UpdateInterval"/> seconds have passed since the most recent
        /// scrape was sent.
        /// </summary>
        bool CanSendScrape {
            get {
                return TimeSinceLastScrape > ActiveTracker.UpdateInterval;
            }
        }

        int ActiveTrackerIndex { get; set; }
        bool SentStartedEvent { get; set; }

        ValueStopwatch LastAnnounce { get; set; }
        ValueStopwatch LastScrape { get; set; }

        public bool LastAnnounceSucceeded { get; private set; }
        public bool LastScrapeSucceeded { get; internal set; }

        public TimeSpan TimeSinceLastAnnounce {
            get => LastAnnounce.IsRunning ? LastAnnounce.Elapsed : TimeSpan.MaxValue;
            internal set => LastAnnounce = ValueStopwatch.WithTime (value);
        }

        public TimeSpan TimeSinceLastScrape {
            get => LastScrape.IsRunning ? LastScrape.Elapsed : TimeSpan.MaxValue;
            internal set => LastScrape = ValueStopwatch.WithTime (value);
        }

        ScrapeResponse LastScrapeResponse { get; set; } = new ScrapeResponse (TrackerState.Unknown);

        internal TrackerTier (Factories factories, IEnumerable<string> trackerUrls)
        {
            var trackerList = new List<ITracker> ();
            foreach (string trackerUrl in trackerUrls) {
                if (!Uri.TryCreate (trackerUrl, UriKind.Absolute, out Uri? result)) {
                    logger.InfoFormatted ("Invalid tracker Url specified: {0}", trackerUrl);
                    continue;
                }

                ITracker? tracker = factories.CreateTracker (result);
                if (tracker is null) {
                    logger.InfoFormatted ("Unsupported protocol {0}", result);
                } else {
                    trackerList.Add (tracker);
                }
            }

            Toolbox.Randomize (trackerList);
            Trackers = trackerList.AsReadOnly ();
        }

        internal TrackerTier (ITracker tracker)
        {
            Trackers = Array.AsReadOnly (new[] { tracker });
        }

        internal async ReusableTask AnnounceAsync (AnnounceRequest args, CancellationToken token)
        {
            // Bail out if we're announcing too frequently for this tracker tier.
            if (args.ClientEvent == TorrentEvent.None && !CanSendAnnounce)
                return;

            if (!SentStartedEvent)
                args = args.WithClientEvent (TorrentEvent.Started);

            // Update before sending an announce so 'CanSendAnnounce' starts to return 'false'.
            LastAnnounce = ValueStopwatch.StartNew ();

            // If a specific tracker is passed to this method then only announce to that tracker. Otherwise
            // we should try all trackers in a round-robin fashion.
            for (int i = 0; i < Trackers.Count; i++) {
                var tracker = Trackers[(ActiveTrackerIndex + i) % Trackers.Count];
                try {
                    var response = await DoAnnounceAsync (args, tracker, token);
                    var dict = response.Peers;
                    AnnounceComplete?.Invoke (this, new AnnounceResponseEventArgs (tracker, true, dict));
                    LastAnnounce = ValueStopwatch.StartNew ();
                    LastAnnounceSucceeded = true;
                    logger.InfoFormatted ("Announced to {0}", tracker.Uri);
                    return;
                } catch {
                    logger.ErrorFormatted ("Could not announce to {0}", tracker.Uri);
                    AnnounceComplete?.Invoke (this, new AnnounceResponseEventArgs (tracker, false));
                    token.ThrowIfCancellationRequested ();
                }
            }

            LastAnnounce = ValueStopwatch.StartNew ();
            LastAnnounceSucceeded = false;
            logger.Error ("Could not announce to any tracker");
        }

        internal async ReusableTask<AnnounceResponse> AnnounceAsync (AnnounceRequest args, ITracker tracker, CancellationToken token)
        {
            if (!SentStartedEvent)
                args = args.WithClientEvent (TorrentEvent.Started);

            // Update before sending an announce so 'CanSendAnnounce' starts to return 'false'.
            LastAnnounce = ValueStopwatch.StartNew ();

            try {
                var result = await DoAnnounceAsync (args, tracker, token);
                AnnounceComplete?.Invoke (this, new AnnounceResponseEventArgs (tracker, true));
                LastAnnounceSucceeded = true;
                return result;
            } catch {
                AnnounceComplete?.Invoke (this, new AnnounceResponseEventArgs (tracker, false));
                LastAnnounceSucceeded = false;
                token.ThrowIfCancellationRequested ();
                throw;
            } finally {
                LastAnnounce = ValueStopwatch.StartNew ();
            }
        }

        async ReusableTask<AnnounceResponse> DoAnnounceAsync (AnnounceRequest args, ITracker tracker, CancellationToken token)
        {
            var response = await tracker.AnnounceAsync (args, token);
            ActiveTrackerIndex = Trackers.IndexOf (tracker);
            SentStartedEvent |= args.ClientEvent == TorrentEvent.Started;
            return response;
        }

        internal async ReusableTask ScrapeAsync (ScrapeRequest args, CancellationToken token)
        {
            if (!CanSendScrape)
                return;

            for (int i = 0; i < Trackers.Count; i++) {
                var tracker = Trackers[(ActiveTrackerIndex + i) % Trackers.Count];
                if (!tracker.CanScrape)
                    continue;
                try {
                    LastScrapeResponse = await tracker.ScrapeAsync (args, token);
                    ScrapeComplete?.Invoke (this, new ScrapeResponseEventArgs (tracker, true));
                } catch {
                    ScrapeComplete?.Invoke (this, new ScrapeResponseEventArgs (tracker, false));
                    token.ThrowIfCancellationRequested ();
                }
            }
        }

        internal async ReusableTask ScrapeAsync (ScrapeRequest args, ITracker tracker, CancellationToken token)
        {
            try {
                LastScrapeResponse = await tracker.ScrapeAsync (args, token);
                ScrapeComplete?.Invoke (this, new ScrapeResponseEventArgs (tracker, true));
            } catch {
                ScrapeComplete?.Invoke (this, new ScrapeResponseEventArgs (tracker, false));
                token.ThrowIfCancellationRequested ();
            }
        }

        internal TrackerTier With (ITracker tracker)
        {
            if (Trackers.Contains (tracker))
                return this;

            var clonedTrackers = new List<ITracker> (Trackers);
            clonedTrackers.Add (tracker);

            var clonedTier = (TrackerTier) MemberwiseClone ();
            clonedTier.Trackers = clonedTrackers;
            return clonedTier;
        }

        internal TrackerTier Without (ITracker tracker)
        {
            if (!Trackers.Contains (tracker))
                return this;

            var clonedTrackers = new List<ITracker> (Trackers);
            clonedTrackers.Remove (tracker);

            var clonedTier = (TrackerTier) MemberwiseClone ();
            clonedTier.Trackers = clonedTrackers;
            clonedTier.ActiveTrackerIndex %= Trackers.Count;
            return clonedTier;
        }
    }
}
