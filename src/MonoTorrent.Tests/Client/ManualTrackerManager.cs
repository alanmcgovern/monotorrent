//
// ManualLocalPeerListener.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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
using System.Threading.Tasks;

using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Client
{
    public class ManualTrackerManager : ITrackerManager
    {
        public event EventHandler<AnnounceResponseEventArgs> AnnounceComplete;
        public event EventHandler<ScrapeResponseEventArgs> ScrapeComplete;

        public List<Tuple<ITracker, TorrentEvent>> Announces { get; }
        public List<ITracker> Scrapes { get; }

        public ITracker CurrentTracker { get; private set; }

        public bool LastAnnounceSucceeded { get; }

        public DateTime LastUpdated { get; }

        public TimeSpan ResponseDelay { get; set; }

        public IList<TrackerTier> Tiers { get; } = new List<TrackerTier> ();

        public TimeSpan TimeSinceLastAnnounce { get; private set; }

        public ManualTrackerManager ()
        {
            Announces = new List<Tuple<ITracker, TorrentEvent>> ();
            Scrapes = new List<ITracker> ();
        }

        public ManualTrackerManager (string tracker)
            : this ()
        {
            AddTracker (tracker);
        }

        public void AddTracker (string tracker)
        {
            var trackers = new[] { tracker };
            Tiers.Add (new TrackerTier (trackers));
            CurrentTracker = Tiers[0].Trackers[0];
        }

        public Task Announce ()
            => Announce (CurrentTracker, TorrentEvent.None);

        public Task Announce (TorrentEvent clientEvent)
            => Announce (CurrentTracker, clientEvent);

        public Task Announce (ITracker tracker)
            => Announce (tracker, TorrentEvent.None);

        async Task Announce (ITracker tracker, TorrentEvent clientEvent)
        {
            if (ResponseDelay != TimeSpan.Zero)
                await Task.Delay (ResponseDelay);

            Announces.Add (Tuple.Create (tracker, clientEvent));
        }

        public void RaiseAnnounceComplete (ITracker tracker, bool successful, IList<Peer> peers)
            => AnnounceComplete?.Invoke (this, new AnnounceResponseEventArgs (tracker, successful, peers));

        public void RaiseScrapeComplete (ITracker tracker, bool successful)
            => ScrapeComplete?.Invoke (this, new ScrapeResponseEventArgs (tracker, successful));

        public Task Scrape ()
            => Scrape (CurrentTracker);

        public Task Scrape (ITracker tracker)
        {
            Scrapes.Add (tracker);
            return Task.CompletedTask;
        }
    }
}
