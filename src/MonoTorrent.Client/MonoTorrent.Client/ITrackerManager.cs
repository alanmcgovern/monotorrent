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
using System.Threading;

using ReusableTasks;

namespace MonoTorrent.Trackers
{
    public interface ITrackerManager
    {
        event EventHandler<AnnounceResponseEventArgs> AnnounceComplete;
        event EventHandler<ScrapeResponseEventArgs> ScrapeComplete;

        /// <summary>
        /// If this is set to 'true' then <see cref="AddTrackerAsync(ITracker)"/>,
        /// <see cref="AddTrackerAsync(Uri)"/> and <see cref="RemoveTrackerAsync(ITracker)"/> will throw an
        /// <see cref="InvalidOperationException"/> when they are invoked.
        /// </summary>
        bool Private { get; }

        /// <summary>
        /// The list of TrackerTiers
        /// </summary>
        IList<TrackerTier> Tiers { get; }

        /// <summary>
        /// Adds the tracker to a new TrackerTier.
        /// </summary>
        /// <param name="tracker">The tracker to add</param>
        ReusableTask AddTrackerAsync (ITracker tracker);

        /// <summary>
        /// Creates an ITracker instance for the given url and adds it to a
        /// new TrackerTier.
        /// </summary>
        /// <param name="trackerUri"></param>
        ReusableTask AddTrackerAsync (Uri trackerUri);

        /// <summary>
        /// Removes the <see cref="ITracker"/> from the manager. If the <see cref="TrackerTier"/> it was part of is now empty
        /// it will also be removed.
        /// </summary>
        /// <param name="tracker"></param>
        /// <returns></returns>
        ReusableTask<bool> RemoveTrackerAsync (ITracker tracker);

        /// <summary>
        /// Sends an Announce to each tier in <see cref="Tiers"/> to fetch additional peers.
        /// This will respect the Tracker's <see cref="ITracker.MinUpdateInterval"/> and
        /// <see cref="ITracker.UpdateInterval"/> to avoid announcing too frequently.
        /// </summary>
        /// <param name="token">The token used to cancel the request</param>
        /// <returns></returns>
        ReusableTask AnnounceAsync (CancellationToken token);

        /// <summary>
        /// Sends an Announce to the specified tracker using <see cref="TorrentEvent.None"/>
        /// in order to fetch more peers. This will respect the Tracker's
        /// <see cref="ITracker.MinUpdateInterval"/> and <see cref="ITracker.UpdateInterval"/>
        /// to avoid announcing to frequently.
        /// </summary>
        /// <param name="tracker">The tracker to send the Announce to.</param>
        /// <param name="token">The token used to cancel the request</param>
        /// <returns></returns>
        ReusableTask AnnounceAsync (ITracker tracker, CancellationToken token);

        /// <summary>
        /// Sends an announce with the specified event to each tier in <see cref="Tiers"/>.
        /// If <see cref="TorrentEvent.None"/> is specified then the Tracker's
        /// <see cref="ITracker.MinUpdateInterval"/> and <see cref="ITracker.UpdateInterval"/>
        /// will be respected to avoid announcing too frequently. Otherwise this
        /// is a special announce which will be sent regardless of the
        /// usual update interval.
        /// </summary>
        /// <param name="clientEvent">The event to send with the announce.</param>
        /// <param name="token">The token used to cancel the request.</param>
        /// <returns></returns>
        ReusableTask AnnounceAsync (TorrentEvent clientEvent, CancellationToken token);

        /// <summary>
        /// Sends a Scrape to each TrackerTier. This will respect the <see cref="ITracker.MinUpdateInterval"/>
        /// for the Tracker to avoid scraping too frequently
        /// </summary>
        /// <param name="token">The token used to cancel the request</param>
        /// <returns></returns>
        ReusableTask ScrapeAsync (CancellationToken token);

        /// <summary>
        /// Sends a Scrape to each TrackerTier. This will respect the <see cref="ITracker.MinUpdateInterval"/>
        /// for the Tracker to avoid scraping too frequently.
        /// </summary>
        /// <param name="tracker">Tje tracker to send the Scrape to.</param>
        /// <param name="token">The token used to cancel the request</param>
        /// <returns></returns>
        ReusableTask ScrapeAsync (ITracker tracker, CancellationToken token);
    }
}
