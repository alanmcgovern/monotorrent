//
// Tracker.cs
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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Connections.Tracker;

using ReusableTasks;

namespace MonoTorrent.Trackers
{
    public class Tracker : ITracker
    {
        static Random Random { get; } = new Random ();

        IList<ITrackerConnection> Connections { get; }

        ValueStopwatch LastAnnounced;
        AnnounceResponse LastAnnounceResponse { get; set; }
        TrackerResponse LastResponse { get; set; }

        public int AnnounceKey { get; }

        public bool CanAnnounce => true;
        public bool CanScrape => Connections[0].CanScrape;

        public Dictionary<InfoHash, ScrapeInfo> ScrapeInfo => LastResponse.ScrapeInfo ?? new Dictionary<InfoHash, ScrapeInfo> ();

        public string FailureMessage => LastResponse.FailureMessage;
        public string WarningMessage => LastResponse.WarningMessage;


        public TimeSpan MinUpdateInterval => LastAnnounceResponse.MinUpdateInterval;
        public TimeSpan UpdateInterval => LastAnnounceResponse.UpdateInterval;


        public TrackerState Status => StatusOverride.HasValue ? StatusOverride.Value : LastResponse.State;
        public TimeSpan TimeSinceLastAnnounce => LastAnnounced.IsRunning ? LastAnnounced.Elapsed : TimeSpan.MaxValue;
        public Uri Uri => Connections[0].Uri;

        TrackerState? StatusOverride { get; set; }

        public Tracker (ITrackerConnection connection)
           : this (new[] { connection ?? throw new ArgumentNullException (nameof (connection)) })
        {
        }

        public Tracker (params ITrackerConnection[] connections)
        {
            Connections = new List<ITrackerConnection> (connections ?? throw new ArgumentNullException (nameof (connections))).AsReadOnly ();

            // Use a random integer prefixed by our identifier.
            lock (Random)
                AnnounceKey = Random.Next (1, int.MaxValue);

            LastAnnounced = new ValueStopwatch ();
            LastResponse = LastAnnounceResponse = new AnnounceResponse (TrackerState.Unknown);
        }

        public async ReusableTask<AnnounceResponse> AnnounceAsync (AnnounceRequest parameters, CancellationToken token)
        {
            try {
                StatusOverride = TrackerState.Connecting;
                parameters = parameters.WithKey (AnnounceKey);
                var responses = await Task.WhenAll (Connections.Select (c => c.AnnounceAsync (parameters, token).AsTask ()));
                var response = responses.FirstOrDefault (t => t.State == TrackerState.Ok) ?? responses.First ();

                // Combine peers from each connection (usually ipv4 and ipv6)
                if (response.State == TrackerState.Ok) {
                    foreach (var infohash in response.Peers.Keys.ToArray ()) {
                        foreach (var resp in responses.Where (t => t.State == TrackerState.Ok && t != response))
                            response.Peers[infohash] = response.Peers[infohash].Concat (resp.Peers[infohash]).ToList ().AsReadOnly ();
                    }
                }

                LastResponse = response;
                return response;
            } finally {
                StatusOverride = null;
                LastAnnounced.Restart ();
            }
        }

        public async ReusableTask<ScrapeResponse> ScrapeAsync (ScrapeRequest parameters, CancellationToken token)
        {
            try {
                StatusOverride = TrackerState.Connecting;
                var responses = await Task.WhenAll (Connections.Select (c => c.ScrapeAsync (parameters, token).AsTask ()));
                var response = responses.FirstOrDefault (t => t.State == TrackerState.Ok) ?? responses.First ();
                LastResponse = response;
                return response;
            } finally {
                StatusOverride = null;
                LastAnnounced.Restart ();
            }
        }
    }
}
