//
// ITrackerManager.cs
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

namespace MonoTorrent.Client.Tracker
{
    public interface ITrackerManager
    {
        event EventHandler<AnnounceResponseEventArgs> AnnounceComplete;
        event EventHandler<ScrapeResponseEventArgs> ScrapeComplete;

        /// <summary>
        /// Returns the ITracker which mostly recently responded to an Announce or Scrape request.
        /// </summary>
        [Obsolete("This is now a per-Tier value and should be accessed using TrackerTier.ActiveTier.")]
        ITracker CurrentTracker { get; }

        /// <summary>
        /// True if the most recent Announce request was successful.
        /// </summary>
        [Obsolete("This is now a per-Tier value and should be accessed using TrackerTier.LastAnnounceSucceeded.")]
        bool LastAnnounceSucceeded { get; }

        /// <summary>
        /// The time, in UTC, when the most recent Announce request was sent
        /// </summary>
        [Obsolete("This is now a per-Tier value and should be accessed using TrackerTier.LastUpdated.")]
        DateTime LastUpdated { get; }

        /// <summary>
        /// The available trackers.
        /// </summary>
        IList<TrackerTier> Tiers { get; }

        /// <summary>
        /// The amount of time which has passed since the most recent Announce request was sent.
        /// </summary>
        TimeSpan TimeSinceLastAnnounce { get; }

        /// <summary>
        /// Sends an announce request to each tierSend an Announce request to the <see cref="CurrentTracker"/>.
        /// </summary>
        /// <returns></returns>
        Task Announce ();

        /// <summary>
        /// Send an Announce request to the <see cref="CurrentTracker"/>, with the
        /// specified <see cref="TorrentEvent"/>.
        /// </summary>
        /// <param name="clientEvent">The event, if any, associated with this Announce request. If the torrent has just started
        /// then <see cref="TorrentEvent.Started"/>) is sent. If it was just stopped then (<see cref="TorrentEvent.Stopped"/> is sent.
        /// If it just reached 100% completion then <see cref="TorrentEvent.Completed"/> is sent, otherwise
        /// <see cref="TorrentEvent.None"/> should be sent.</param>
        /// <returns></returns>
        Task Announce (TorrentEvent clientEvent);

        /// <summary>
        /// Send an Announce request to the specified tracker.
        /// </summary>
        /// <param name="tracker">The tracker to query</param>
        /// <returns></returns>
        Task Announce (ITracker tracker);

        /// <summary>
        /// Send a Scrape request to the <see cref="CurrentTracker"/>
        /// </summary>
        /// <returns></returns>
        Task Scrape ();

        /// <summary>
        /// Send a Scrape request to the specified tracker.
        /// </summary>
        /// <param name="tracker">The tracker to query</param>
        /// <returns></returns>
        Task Scrape (ITracker tracker);
    }
}
