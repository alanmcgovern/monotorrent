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
using System.Threading;
using System.Threading.Tasks;

using ReusableTasks;

namespace MonoTorrent.Trackers
{
    public class ManualTrackerManager : ITrackerManager
    {
        public event EventHandler<AnnounceResponseEventArgs> AnnounceComplete;
        public event EventHandler<ScrapeResponseEventArgs> ScrapeComplete;

        public bool Private { get; set; }
        public IList<TrackerTier> Tiers { get; set; } = new List<TrackerTier> ();

        public TimeSpan ResponseDelay { get; set; }
        public List<Tuple<ITracker, TorrentEvent>> Announces { get; }
        public List<ITracker> Scrapes { get; }

        public ManualTrackerManager ()
        {
            Announces = new List<Tuple<ITracker, TorrentEvent>> ();
            Scrapes = new List<ITracker> ();
        }

        public ManualTrackerManager (Uri tracker)
            : this ()
        {
            Tiers.Add (new TrackerTier (Factories.Default.CreateTracker (tracker)));
        }

        public ReusableTask AddTrackerAsync (ITracker tracker)
        {
            Tiers.Add (new TrackerTier (tracker));
            return ReusableTask.CompletedTask;
        }

        public ReusableTask AddTrackerAsync (Uri trackerUri)
        {
            return AddTrackerAsync (Factories.Default.CreateTracker (trackerUri));
        }

        public ReusableTask<bool> RemoveTrackerAsync (ITracker tracker)
        {
            throw new NotSupportedException ();
        }

        public ReusableTask AnnounceAsync (CancellationToken token)
            => AnnounceAsync (null, TorrentEvent.None, token);

        public ReusableTask AnnounceAsync (ITracker tracker, CancellationToken token)
            => AnnounceAsync (tracker, TorrentEvent.None, token);

        public ReusableTask AnnounceAsync (TorrentEvent clientEvent, CancellationToken token)
            => AnnounceAsync (null, clientEvent, token);

        async ReusableTask AnnounceAsync (ITracker tracker, TorrentEvent clientEvent, CancellationToken token)
        {
            if (ResponseDelay != TimeSpan.Zero)
                await Task.Delay (ResponseDelay);

            Announces.Add (Tuple.Create (tracker, clientEvent));
        }

        public ReusableTask ScrapeAsync (CancellationToken token)
        {
            Scrapes.Add (null);
            return ReusableTask.CompletedTask;
        }

        public ReusableTask ScrapeAsync (ITracker tracker, CancellationToken token)
        {
            Scrapes.Add (tracker);
            return ReusableTask.CompletedTask;
        }

        public void RaiseAnnounceComplete (ITracker tracker, bool successful, Dictionary<InfoHash, IList<PeerInfo>> peers)
            => AnnounceComplete?.Invoke (this, new AnnounceResponseEventArgs (tracker, successful, peers));

        public void RaiseScrapeComplete (ITracker tracker, bool successful)
            => ScrapeComplete?.Invoke (this, new ScrapeResponseEventArgs (tracker, successful));
    }
}
