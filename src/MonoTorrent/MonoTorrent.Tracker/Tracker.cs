//
// Tracker.cs
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
using System.IO;
using System.Net;
using System.Web;
using System.Text;
using System.Diagnostics;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;

using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using MonoTorrent.Tracker.Listeners;

namespace MonoTorrent.Tracker
{
    public class Tracker : IEnumerable<SimpleTorrentManager>
    {
        #region Static BEncodedStrings

        internal static readonly BEncodedString PeersKey = "peers";
        internal static readonly BEncodedString IntervalKey = "interval";
        internal static readonly BEncodedString MinIntervalKey = "min interval";
        internal static readonly BEncodedString TrackerIdKey = "tracker id";
        internal static readonly BEncodedString CompleteKey = "complete";
        internal static readonly BEncodedString Incomplete = "incomplete";
        internal static readonly BEncodedString PeerIdKey = "peer id";
        internal static readonly BEncodedString Port = "port";
        internal static readonly BEncodedString Ip = "ip";

        #endregion Static BEncodedStrings


        #region Events

        public event EventHandler<AnnounceEventArgs> PeerAnnounced;
        public event EventHandler<ScrapeEventArgs> PeerScraped;
        public event EventHandler<TimedOutEventArgs> PeerTimedOut;

        #endregion Events


        #region Fields

        private bool allowScrape;
        private bool allowNonCompact;
        private bool allowUnregisteredTorrents;
        private TimeSpan announceInterval;
        private TimeSpan minAnnounceInterval;
        private RequestMonitor monitor;
        private TimeSpan timeoutInterval;
        private Dictionary<byte[], SimpleTorrentManager> torrents;
        private BEncodedString trackerId;


        #endregion Fields


        #region Properties

        public bool AllowNonCompact
        {
            get { return allowNonCompact; }
            set { allowNonCompact = value; }
        }

        public bool AllowScrape
        {
            get { return allowScrape; }
            set { allowScrape = value; }
        }

        public bool AllowUnregisteredTorrents
        {
            get { return allowUnregisteredTorrents; }
            set { allowUnregisteredTorrents = value; }
        }

        public TimeSpan AnnounceInterval
        {
            get { return announceInterval; }
            set { announceInterval = value; }
        }

        public int Count
        {
            get { return torrents.Count; }
        }

        public TimeSpan MinAnnounceInterval
        {
            get { return minAnnounceInterval; }
            set { minAnnounceInterval = value; }
        }

        public RequestMonitor Requests
        {
            get { return monitor; }
        }

        public TimeSpan TimeoutInterval
        {
            get { return timeoutInterval; }
            set { timeoutInterval = value; }
        }

        public BEncodedString TrackerId
        {
            get { return trackerId; }
        }

        #endregion Properties


        #region Constructors

        /// <summary>
        /// Creates a new tracker
        /// </summary>
        public Tracker()
            : this(new BEncodedString("monotorrent-tracker"))
        {

        }

        public Tracker(BEncodedString trackerId)
        {
            allowNonCompact = true;
            allowScrape = true;
            monitor = new RequestMonitor();
            torrents = new Dictionary<byte[], SimpleTorrentManager>(new ByteComparer());
            this.trackerId = trackerId;

            announceInterval = TimeSpan.FromMinutes(45);
            minAnnounceInterval = TimeSpan.FromMinutes(10);
            timeoutInterval = TimeSpan.FromMinutes(50);
        }

        #endregion Constructors


        #region Methods

        public bool Add(ITrackable trackable)
        {
            return Add(trackable, new IPAddressComparer());
        }

        public bool Add(ITrackable trackable, IPeerComparer comparer)
        {
            if (trackable == null)
                throw new ArgumentNullException("trackable");

            lock (torrents)
            {
                if (torrents.ContainsKey(trackable.InfoHash))
                    return false;

                torrents.Add(trackable.InfoHash, new SimpleTorrentManager(trackable, comparer, this));
            }

            Debug.WriteLine(string.Format("Tracking Torrent: {0}", trackable.Name));
            return true;
        }

        public bool Contains(ITrackable trackable)
        {
            if (trackable == null)
                throw new ArgumentNullException("trackable");

            lock (torrents)
                return torrents.ContainsKey(trackable.InfoHash);
        }

        public SimpleTorrentManager GetManager(ITrackable trackable)
        {
            if(trackable == null)
                throw new ArgumentNullException("trackable");

            SimpleTorrentManager value;
            lock (torrents)
                if (torrents.TryGetValue(trackable.InfoHash, out value))
                    return value;

            return null;
        }

        public IEnumerator<SimpleTorrentManager> GetEnumerator()
        {
            lock (torrents)
                return new List<SimpleTorrentManager>(this.torrents.Values).GetEnumerator();
        }

        public bool IsRegistered(ListenerBase listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");
            
            return listener.Tracker == this;
        }

        private void ListenerReceivedAnnounce(object sender, AnnounceParameters e)
        {
            monitor.AnnounceReceived();
            SimpleTorrentManager manager;

            // Check to see if we're monitoring the requested torrent
            lock (torrents)
            {
                if (!torrents.TryGetValue(e.InfoHash, out manager))
                {
                    if (AllowUnregisteredTorrents)
                    {
                        Add(new InfoHashTrackable(BitConverter.ToString(e.InfoHash), e.InfoHash));
                        manager = torrents[e.InfoHash];
                    }
                    else
                    {
                        e.Response.Add(RequestParameters.FailureKey, (BEncodedString)"The requested torrent is not registered with this tracker");
                        return;
                    }
                }
            }

            // If a non-compact response is expected and we do not allow non-compact responses
            // bail out
            if (!AllowNonCompact && !e.HasRequestedCompact)
            {
                e.Response.Add(RequestParameters.FailureKey, (BEncodedString)"This tracker does not support non-compact responses");
                return;
            }

            lock (manager)
            {
                // Update the tracker with the peers information. This adds the peer to the tracker,
                // updates it's information or removes it depending on the context
                manager.Update(e);

                // Clear any peers who haven't announced within the allowed timespan and may be inactive
                manager.ClearZombiePeers(DateTime.Now.Add(-TimeoutInterval));

                // Fulfill the announce request
                manager.GetPeers(e.Response, e.NumberWanted, e.HasRequestedCompact);
            }

            e.Response.Add(Tracker.IntervalKey, new BEncodedNumber((int)AnnounceInterval.TotalSeconds));
            e.Response.Add(Tracker.MinIntervalKey, new BEncodedNumber((int)MinAnnounceInterval.TotalSeconds));
            e.Response.Add(Tracker.TrackerIdKey, trackerId); // FIXME: Is this right?
            e.Response.Add(Tracker.CompleteKey, new BEncodedNumber(manager.Complete));
            e.Response.Add(Tracker.Incomplete, new BEncodedNumber(manager.Incomplete));

            //FIXME is this the right behaivour 
            //if (par.TrackerId == null)
            //    par.TrackerId = "monotorrent-tracker";
        }

        private void ListenerReceivedScrape(object sender, ScrapeParameters e)
        {
            monitor.ScrapeReceived();
            if (!AllowScrape)
            {
                e.Response.Add(RequestParameters.FailureKey, (BEncodedString)"This tracker does not allow scraping");
                return;
            }

            if (e.InfoHashes.Count == 0)
            {
                e.Response.Add(RequestParameters.FailureKey, (BEncodedString)"You must specify at least one infohash when scraping this tracker");
                return;
            }
            List<SimpleTorrentManager> managers = new List<SimpleTorrentManager>();
            BEncodedDictionary files = new BEncodedDictionary();
            for (int i = 0; i < e.InfoHashes.Count; i++)
            {
                // FIXME: Converting infohash
                SimpleTorrentManager manager;
                string key = Toolbox.ToHex(e.InfoHashes[i]);
                if (!torrents.TryGetValue(e.InfoHashes[i], out manager))
                    continue;

                managers.Add(manager);
                
                BEncodedDictionary dict = new BEncodedDictionary();
                dict.Add("complete",new BEncodedNumber( manager.Complete));
                dict.Add("downloaded", new BEncodedNumber(manager.Downloaded));
                dict.Add("incomplete", new BEncodedNumber(manager.Incomplete));
                dict.Add("name", new BEncodedString(manager.Trackable.Name));
                files.Add(key, dict);
            }
            RaisePeerScraped(new ScrapeEventArgs(managers));
            e.Response.Add("files", files);
        }

        internal void RaisePeerAnnounced(AnnounceEventArgs e)
        {
            EventHandler<AnnounceEventArgs> h = PeerAnnounced;
            if (h != null)
                h(this, e);
        }

        internal void RaisePeerScraped(ScrapeEventArgs e)
        {
            EventHandler<ScrapeEventArgs> h = PeerScraped;
            if (h != null)
                h(this, e);
        }

        internal void RaisePeerTimedOut(TimedOutEventArgs e)
        {
            EventHandler<TimedOutEventArgs> h = PeerTimedOut;
            if (h != null)
                h(this, e);
        }

        public void RegisterListener(ListenerBase listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");

            if (listener.Tracker != null)
                throw new TorrentException("The listener is registered to a different Tracker");

            listener.Tracker = this;
            listener.AnnounceReceived += new EventHandler<AnnounceParameters>(ListenerReceivedAnnounce);
            listener.ScrapeReceived += new EventHandler<ScrapeParameters>(ListenerReceivedScrape);
        }

        public void Remove(ITrackable trackable)
        {
            if (trackable == null)
                throw new ArgumentNullException("trackable");

            lock (torrents)
                torrents.Remove(trackable.InfoHash);
        }

        public void UnregisterListener(ListenerBase listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");

            if (listener.Tracker != this)
                throw new TorrentException("The listener is not registered with this tracker");

            listener.Tracker = null;
            listener.AnnounceReceived -= new EventHandler<AnnounceParameters>(ListenerReceivedAnnounce);
            listener.ScrapeReceived -= new EventHandler<ScrapeParameters>(ListenerReceivedScrape);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion Methods
    }
}
