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
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client;

using ReusableTasks;

namespace MonoTorrent.Trackers
{
    /// <summary>
    /// Represents the connection to a tracker that an TorrentManager has
    /// </summary>
    class TrackerManager : ITrackerManager
    {
        static readonly int MaxConcurrentAnnounces = 15;

        public event EventHandler<AnnounceResponseEventArgs>? AnnounceComplete;
        public event EventHandler<ScrapeResponseEventArgs>? ScrapeComplete;

        ReusableSemaphore AnnounceLimiter { get; }

        Factories Factories { get; }

        /// <summary>
        /// If this is set to 'true' then <see cref="AddTrackerAsync(ITracker)"/>,
        /// <see cref="AddTrackerAsync(Uri)"/> and <see cref="RemoveTrackerAsync(ITracker)"/> will throw an
        /// <see cref="InvalidOperationException"/> when they are invoked.
        /// </summary>
        public bool Private { get; }

        /// <summary>
        /// Returns an immutable copy of the current list of trackers.
        /// </summary>
        public IList<TrackerTier> Tiers { get; private set; }

        /// <summary>
        /// The TorrentManager associated with this tracker
        /// </summary>
        ITrackerRequestFactory RequestFactory { get; }

        /// <summary>
        /// Creates a new TrackerConnection for the supplied torrent file
        /// </summary>
        /// <param name="factories"></param>
        /// <param name="requestFactory">The factory used to create tracker requests. Typically a <see cref="TorrentManager"/> instance.</param>
        /// <param name="announces">The list of tracker tiers</param>
        /// <param name="isPrivate">True if adding/removing tracker should be disallowed.</param>
        internal TrackerManager (Factories factories, ITrackerRequestFactory requestFactory, IEnumerable<IEnumerable<string>> announces, bool isPrivate)
        {
            AnnounceLimiter = new ReusableSemaphore (1);
            Factories = factories;
            RequestFactory = requestFactory;
            Private = isPrivate;

            var trackerTiers = new List<TrackerTier> ();
            foreach (var announceTier in announces) {
                var tier = new TrackerTier (factories, announceTier);
                if (tier.Trackers.Count > 0) {
                    tier.AnnounceComplete += RaiseAnnounceComplete;
                    tier.ScrapeComplete += RaiseScrapeComplete;
                    trackerTiers.Add (tier);
                }
            }
            Toolbox.Randomize (trackerTiers);
            Tiers = trackerTiers.AsReadOnly ();
        }

        public async ReusableTask AddTrackerAsync (ITracker tracker)
        {
            if (tracker == null)
                throw new ArgumentNullException (nameof (tracker));
            if (Private)
                throw new InvalidOperationException ("Cannot add trackers to a private torrent.");

            await ClientEngine.MainLoop;

            var tier = new TrackerTier (tracker);
            tier.AnnounceComplete += RaiseAnnounceComplete;
            tier.ScrapeComplete += RaiseScrapeComplete;

            var newTrackers = new List<TrackerTier> (Tiers.Count + 1);
            newTrackers.AddRange (Tiers);
            newTrackers.Add (tier);
            Tiers = newTrackers.AsReadOnly ();
        }

        public ReusableTask AddTrackerAsync (Uri trackerUri)
        {
            if (Private)
                throw new InvalidOperationException ("Cannot add trackers to a private Torrent");

            var tracker = Factories.CreateTracker (trackerUri);
            if (tracker != null)
                return AddTrackerAsync (tracker);
            else
                throw new NotSupportedException ($"TrackerFactory.Create could not create an ITracker for this {trackerUri}.");
        }

        public async ReusableTask<bool> RemoveTrackerAsync (ITracker tracker)
        {
            if (tracker == null)
                throw new ArgumentNullException (nameof (tracker));
            if (Private)
                throw new InvalidOperationException ("Cannot remove trackers from a private torrent.");

            await ClientEngine.MainLoop;

            var clone = new List<TrackerTier> (Tiers);
            var tier = Tiers.FirstOrDefault (t => t.Trackers.Contains (tracker));
            if (tier == null)
                return false;

            tier.AnnounceComplete -= AnnounceComplete;
            tier.ScrapeComplete -= ScrapeComplete;
            var index = clone.IndexOf (tier);
            if (tier.Trackers.Count == 1)
                clone.RemoveAt (index);
            else
                clone[index] = tier.Without (tracker);
            Tiers = clone.AsReadOnly ();
            return true;
        }

        public ReusableTask AnnounceAsync (CancellationToken token)
            => AnnounceAsync (TorrentEvent.None, token);

        public ReusableTask AnnounceAsync (TorrentEvent clientEvent, CancellationToken token)
            => AnnounceAsync (clientEvent, null, token);

        public ReusableTask AnnounceAsync (ITracker tracker, CancellationToken token)
            => AnnounceAsync (TorrentEvent.None, tracker ?? throw new ArgumentNullException (nameof (tracker)), token);

        async ReusableTask AnnounceAsync (TorrentEvent clientEvent, ITracker? tracker, CancellationToken token)
        {
            // If the user initiates an Announce we need to go to the correct thread to process it.
            await ClientEngine.MainLoop;

            // Check if there are any in-progress announce requests being handled.
            if (!AnnounceLimiter.TryEnter (out ReusableSemaphore.Releaser releaser)) {
                // There is an in-progress announce, and this is a regular recurring announce attempt, so just bail out.
                if (tracker is null && clientEvent == TorrentEvent.None)
                    return;

                // If we get here it means there's an in-progress announce *and* this is a special event. Either the user
                // has announced to a specific tracker, or we have a special 'TorrentEvent' to announce to the tracker.
                // Wait for the in-progress announce to complete, then run this one!
                releaser = await AnnounceLimiter.EnterAsync ();
            }

            using var autorelease = releaser;
            token.ThrowIfCancellationRequested ();

            // This is either a regularly scheduled announce, or a specific TorrentEvent is being sent to the tracker.
            if (tracker is null) {
                // Create and re-use the announce args for all trackers and tiers
                var args = RequestFactory.CreateAnnounce (clientEvent);

                // Capture a list of Tiers in case the user adds/removes any mid-announce.
                var pendingTiers = new Queue<TrackerTier> (Tiers);
                var activeAnnounces = new List<Task> ();

                while (pendingTiers.Count > 0) {
                    if (activeAnnounces.Count == MaxConcurrentAnnounces) {
                        var completed = await Task.WhenAny (activeAnnounces);
                        activeAnnounces.Remove (completed);
                        await completed;
                    }

                    // The announce *might* fast-path and exit immediately if that
                    // tracker has been announced to recently.
                    var task = pendingTiers.Dequeue ().AnnounceAsync (args, token);
                    if (task.IsCompleted)
                        await task;
                    else
                        activeAnnounces.Add (task.AsTask ());
                }
                await Task.WhenAll (activeAnnounces);
            } else {
                // This only occurs when the user has manually announced to a specific tracker.
                var trackerTier = Tiers.First (t => t.Trackers.Contains (tracker));
                AnnounceRequest args = RequestFactory.CreateAnnounce (TorrentEvent.None);
                await trackerTier.AnnounceAsync (args, tracker, token);
            }
        }

        public async ReusableTask ScrapeAsync (CancellationToken token)
        {
            // If the user initiates a Scrape we need to go to the correct thread to process it.
            await ClientEngine.MainLoop;

            var args = RequestFactory.CreateScrape ();
            var scrapes = new List<Task> ();
            for (int i = 0; i < Tiers.Count; i++) {
                var scrape = Tiers[i].ScrapeAsync (args, token);
                if (!scrape.IsCompleted)
                    scrapes.Add (scrape.AsTask ());
            }

            if (scrapes.Count > 0)
                await Task.WhenAll (scrapes);
        }

        public async ReusableTask ScrapeAsync (ITracker tracker, CancellationToken token)
        {
            if (!tracker.CanScrape)
                throw new TorrentException ("This tracker does not support scraping");

            // If the user initiates a Scrape we need to go to the correct thread to process it.
            await ClientEngine.MainLoop;

            ScrapeRequest args = RequestFactory.CreateScrape ();
            var trackerTier = Tiers.Single (t => t.Trackers.Contains (tracker));
            await trackerTier.ScrapeAsync (args, tracker, token);
        }

        void RaiseAnnounceComplete (object? sender, AnnounceResponseEventArgs args)
            => AnnounceComplete?.InvokeAsync (this, args);

        void RaiseScrapeComplete (object? sender, ScrapeResponseEventArgs args)
            => ScrapeComplete?.InvokeAsync (this, args);
    }
}
