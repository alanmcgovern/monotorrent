//
// TrackerServer.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//
// Copyright (C) 2006 Gregor Burger
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

using MonoTorrent.BEncoding;
using MonoTorrent.Connections.TrackerServer;

namespace MonoTorrent.TrackerServer
{
    public class TrackerServer : IDisposable
    {
        static readonly Random Random = new Random ();

        /// <summary>
        /// Used for non-compact responses and for compact ipv4 responses
        /// </summary>
        internal static readonly BEncodedString PeersKey = "peers";
        /// <summary>
        /// Used for compact ipv6 responses only.
        /// </summary>
        internal static readonly BEncodedString Peers6Key = "peers6";
        internal static readonly BEncodedString IntervalKey = "interval";
        internal static readonly BEncodedString MinIntervalKey = "min interval";
        internal static readonly BEncodedString TrackerIdKey = "tracker id";
        internal static readonly BEncodedString CompleteKey = "complete";
        internal static readonly BEncodedString DownloadedKey = "downloaded";
        internal static readonly BEncodedString IncompleteKey = "incomplete";
        internal static readonly BEncodedString PeerIdKey = "peer id";
        internal static readonly BEncodedString Port = "port";
        internal static readonly BEncodedString Ip = "ip";

        public event EventHandler<AnnounceEventArgs>? PeerAnnounced;
        public event EventHandler<ScrapeEventArgs>? PeerScraped;
        public event EventHandler<TimedOutEventArgs>? PeerTimedOut;

        /// <summary>
        /// If this false then all Announce requests which require non-compact peer encoding will
        /// be fulfilled by returning an error response. Defaults to <see langword="true"/>.
        /// </summary>
        public bool AllowNonCompact { get; set; } = true;

        /// <summary>
        /// If this is false then any Scrape requests will be fulfilled by returning an error response.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool AllowScrape { get; set; } = true;

        /// <summary>
        /// If this is true then the tracker will add any infohash to it's table as soon as the first
        /// Announce request is received. If it is false, an error response will be sent for any Announce
        /// or Scrape request which queries an infohash which has not been pre-registered with the tracker.
        /// Defaults to <see langword="false"/>.
        /// </summary>
        public bool AllowUnregisteredTorrents { get; set; } = false;

        /// <summary>
        /// This is the regular interval in which peers should re-announce. It should be less than 1/2 the Timeout interval so
        /// peers must miss two announce before timing out. Defaults to 45 minutes.
        /// </summary>
        public TimeSpan AnnounceInterval { get; set; } = TimeSpan.FromMinutes (45);

        /// <summary>
        /// The number of torrents being tracked
        /// </summary>
        public int Count => Torrents.Count;

        /// <summary>
        /// True if the tracker has been disposed.
        /// </summary>
        public bool Disposed { get; private set; }

        /// <summary>
        /// This is the minimum time between Announce. No peer should announce more frequently than this.
        /// Defaults to 10 minutes.
        /// </summary>
        public TimeSpan MinAnnounceInterval { get; set; } = TimeSpan.FromMinutes (10);

        /// <summary>
        /// Tracks the number of Announce and Scrape requests, and the requests per second.
        /// </summary>
        public RequestMonitor Requests { get; }

        /// <summary>
        /// This is the amount of time that has to elapse since an Announce or Scrape request until a peer is
        /// considered offline and is removed from the list. Defaults to 50 minutes.
        /// </summary>
        public TimeSpan TimeoutInterval { get; set; } = TimeSpan.FromMinutes (50);

        /// <summary>
        /// The unique identifier for this tracker. It should be considered an arbitrary string with no
        /// specific meaning, but by default it is of the form "MO1234-{random positive integer}" to allow the
        /// tracker to be identified.
        /// </summary>
        public BEncodedString TrackerId { get; }

        /// <summary>
        /// The listeners which have been registered with the tracker.
        /// </summary>
        List<ITrackerListener> Listeners { get; }

        /// <summary>
        /// The infohashes which have been registered with the tracker, along with the metadata associated with them.
        /// </summary>
        Dictionary<InfoHash, SimpleTorrentManager> Torrents { get; }

        /// <summary>
        /// Creates a new tracker using an autogenerated unique identifier as the <see cref="TrackerId"/>.
        /// </summary>
        public TrackerServer ()
            : this (BEncodedString.Empty)
        {

        }

        /// <summary>
        /// Creates a new tracker
        /// </summary>
        /// <param name="trackerId">The unique identifier to use as the <see cref="TrackerId"/></param>
        public TrackerServer (BEncodedString trackerId)
        {
            Requests = new RequestMonitor ();
            Torrents = new Dictionary<InfoHash, SimpleTorrentManager> ();

            // Generate an ID which shows that this is monotorrent, and the version, and then a unique(ish) integer.
            if (BEncodedString.IsNullOrEmpty (trackerId)) {
                lock (Random)
                    trackerId = $"{GitInfoHelper.ClientVersion}-{Random.Next (1, int.MaxValue)}";
            }
            TrackerId = trackerId;

            Listeners = new List<ITrackerListener> ();
            Client.ClientEngine.MainLoop.QueueTimeout (TimeSpan.FromSeconds (1), delegate {
                Requests.Tick ();
                return !Disposed;
            });
        }

        /// <summary>
        /// Adds the trackable to the tracker. Peers will be compared for equality based on their PeerId.
        /// </summary>
        /// <param name="trackable">The trackable to add</param>
        /// <returns></returns>
        public bool Add (ITrackable trackable)
        {
            return Add (trackable, new ClientAddressComparer ());
        }

        /// <summary>
        /// Adds the trackable to the server
        /// </summary>
        /// <param name="trackable">The trackable to add</param>
        /// <param name="comparer">The comparer used to decide whether two peers are the same.</param>
        /// <returns></returns>
        public bool Add (ITrackable trackable, IPeerComparer comparer)
        {
            CheckDisposed ();
            if (trackable == null)
                throw new ArgumentNullException (nameof (trackable));

           return Add (new SimpleTorrentManager (trackable, comparer, this));
        }

        /// <summary>
        /// Adds the trackable to the server
        /// </summary>
        /// <param name="manager">.</param>
        /// <returns></returns>
        internal bool Add (SimpleTorrentManager manager)
        {
            lock (Torrents) {
                if (Torrents.ContainsKey (manager.Trackable.InfoHash))
                    return false;

                Torrents.Add (manager.Trackable.InfoHash, manager);
            }

            return true;
        }

        void CheckDisposed ()
        {
            if (Disposed)
                throw new ObjectDisposedException (GetType ().Name);
        }

        /// <summary>
        /// Checks if the InfoHash associated with the given trackable has been registered with the tracker.
        /// </summary>
        /// <param name="trackable"></param>
        /// <returns></returns>
        public bool Contains (ITrackable trackable)
        {
            CheckDisposed ();
            if (trackable == null)
                throw new ArgumentNullException (nameof (trackable));

            lock (Torrents)
                return Torrents.ContainsKey (trackable.InfoHash);
        }

        /// <summary>
        /// Returns the manager associated with the given trackable. If the trackable has not been registered
        /// with this tracker then null will be returned.
        /// </summary>
        /// <param name="trackable"></param>
        /// <returns></returns>
        public ITrackerItem? GetTrackerItem (ITrackable trackable)
        {
            CheckDisposed ();
            if (trackable == null)
                throw new ArgumentNullException (nameof (trackable));

            lock (Torrents)
                if (Torrents.TryGetValue (trackable.InfoHash, out SimpleTorrentManager? value))
                    return value;

            return null;
        }

        /// <summary>
        /// Returns a duplicate of the list of active torrents
        /// </summary>
        public List<ITrackerItem> GetTrackerItems ()
        {
            lock (Torrents)
                return new List<ITrackerItem> (Torrents.Values);
        }

        public bool IsRegistered (ITrackerListener listener)
        {
            CheckDisposed ();
            if (listener == null)
                throw new ArgumentNullException (nameof (listener));

            return Listeners.Contains (listener);
        }

        void ListenerReceivedAnnounce (object? sender, AnnounceRequest e)
        {
            if (Disposed) {
                e.Response.Add (TrackerRequest.FailureKey, (BEncodedString) "The tracker has been shut down");
                return;
            }

            Requests.AnnounceReceived ();
            SimpleTorrentManager? manager;

            // Check to see if we're monitoring the requested torrent
            lock (Torrents) {
                if (!Torrents.TryGetValue (e.InfoHash!, out manager)) {
                    if (AllowUnregisteredTorrents) {
                        Add (new InfoHashTrackable (e.InfoHash!.ToHex (), e.InfoHash));
                        manager = Torrents[e.InfoHash];
                    } else {
                        e.Response.Add (TrackerRequest.FailureKey, (BEncodedString) "The requested torrent is not registered with this tracker");
                        return;
                    }
                }
            }

            // If a non-compact response is expected and we do not allow non-compact responses
            // bail out
            if (!AllowNonCompact && !e.HasRequestedCompact) {
                e.Response.Add (TrackerRequest.FailureKey, (BEncodedString) "This tracker does not support non-compact responses");
                return;
            }

            lock (manager) {
                // Update the tracker with the peers information. This adds the peer to the tracker,
                // updates it's information or removes it depending on the context
                manager.Update (e);

                // Clear any peers who haven't announced within the allowed timespan and may be inactive
                manager.ClearZombiePeers (DateTime.Now.Add (-TimeoutInterval));

                // Fulfill the announce request
                manager.GetPeers (e.Response, e.NumberWanted, e.HasRequestedCompact, e.ClientAddress.AddressFamily);
            }

            e.Response.Add (IntervalKey, new BEncodedNumber ((int) AnnounceInterval.TotalSeconds));
            e.Response.Add (MinIntervalKey, new BEncodedNumber ((int) MinAnnounceInterval.TotalSeconds));
            e.Response.Add (TrackerIdKey, TrackerId); // FIXME: Is this right?
            e.Response.Add (CompleteKey, new BEncodedNumber (manager.Complete));
            e.Response.Add (IncompleteKey, new BEncodedNumber (manager.Incomplete));
            e.Response.Add (DownloadedKey, new BEncodedNumber (manager.Downloaded));

            //FIXME is this the right behaivour 
            //if (par.TrackerId == null)
            //    par.TrackerId = "monotorrent-tracker";
        }

        void ListenerReceivedScrape (object? sender, ScrapeRequest e)
        {
            if (Disposed) {
                e.Response.Add (TrackerRequest.FailureKey, (BEncodedString) "The tracker has been shut down");
                return;
            }

            Requests.ScrapeReceived ();
            if (!AllowScrape) {
                e.Response.Add (TrackerRequest.FailureKey, (BEncodedString) "This tracker does not allow scraping");
                return;
            }

            if (e.InfoHashes.Count == 0) {
                e.Response.Add (TrackerRequest.FailureKey, (BEncodedString) "You must specify at least one infohash when scraping this tracker");
                return;
            }

            var managers = new List<ITrackerItem> ();
            var files = new BEncodedDictionary ();
            for (int i = 0; i < e.InfoHashes.Count; i++) {
                if (!Torrents.TryGetValue (e.InfoHashes[i], out SimpleTorrentManager? manager))
                    continue;

                managers.Add (manager);

                var dict = new BEncodedDictionary {
                    { "complete", new BEncodedNumber (manager.Complete) },
                    { "downloaded", new BEncodedNumber (manager.Downloaded) },
                    { "incomplete", new BEncodedNumber (manager.Incomplete) },
                    { "name", new BEncodedString (manager.Trackable.Name) }
                };
                files.Add (new BEncodedString (e.InfoHashes[i].Span.ToArray ()), dict);
            }
            RaisePeerScraped (new ScrapeEventArgs (managers));
            if (files.Count > 0)
                e.Response.Add ("files", files);
        }

        internal void RaisePeerAnnounced (AnnounceEventArgs e)
        {
            PeerAnnounced?.Invoke (this, e);
        }

        internal void RaisePeerScraped (ScrapeEventArgs e)
        {
            PeerScraped?.Invoke (this, e);
        }

        internal void RaisePeerTimedOut (TimedOutEventArgs e)
        {
            PeerTimedOut?.Invoke (this, e);
        }

        public void RegisterListener (ITrackerListener listener)
        {
            CheckDisposed ();
            if (listener == null)
                throw new ArgumentNullException (nameof (listener));

            listener.AnnounceReceived += ListenerReceivedAnnounce;
            listener.ScrapeReceived += ListenerReceivedScrape;
            Listeners.Add (listener);
        }

        /// <summary>
        /// Removes the trackable from the tracker
        /// </summary>
        /// <param name="trackable"></param>
        public void Remove (ITrackable trackable)
        {
            CheckDisposed ();
            if (trackable == null)
                throw new ArgumentNullException (nameof (trackable));

            lock (Torrents)
                Torrents.Remove (trackable.InfoHash);
        }

        public void UnregisterListener (ITrackerListener listener)
        {
            CheckDisposed ();
            if (listener == null)
                throw new ArgumentNullException (nameof (listener));

            listener.AnnounceReceived -= ListenerReceivedAnnounce;
            listener.ScrapeReceived -= ListenerReceivedScrape;
            Listeners.Remove (listener);
        }

        /// <summary>
        /// This unregisters all listeners so no further requests will be processed by this tracker. The listeners
        /// themselves are not disposed.
        /// </summary>
        public void Dispose ()
        {
            if (Disposed)
                return;

            while (Listeners.Count > 0)
                UnregisterListener (Listeners[Listeners.Count - 1]);
            Disposed = true;
        }
    }
}
