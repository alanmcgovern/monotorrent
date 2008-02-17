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

namespace MonoTorrent.Tracker
{
    public class Tracker : IEnumerable<SimpleTorrentManager>
    {
        #region Static BEncodedStrings

        internal static readonly BEncodedString peers = "peers";
        internal static readonly BEncodedString interval = "interval";
        internal static readonly BEncodedString min_interval = "min interval";
        internal static readonly BEncodedString tracker_id = "tracker id";
        internal static readonly BEncodedString complete = "complete";
        internal static readonly BEncodedString incomplete = "incomplete";
        internal static readonly BEncodedString tracker_id_value = "monotorrent-tracker";
        internal static readonly BEncodedString peer_id = "peer id";
        internal static readonly BEncodedString port = "port";
        internal static readonly BEncodedString ip = "ip";
        internal static readonly BEncodedNumber interval_value = new BEncodedNumber(0);
        internal static readonly BEncodedNumber min_interval_value = new BEncodedNumber(0);

        #endregion Static BEncodedStrings
        #region Fields

        private bool allowScrape;
        private bool allowNonCompact;
        private Dictionary<byte[], SimpleTorrentManager> torrents;
        private StaticIntervalAlgorithm intervalAlgorithm;

        #endregion Fields


        #region Properties

        ///<summary>
        /// True if non-compact responses are enabled
        ///</summary>
        public bool AllowNonCompact
        {
            get { return allowNonCompact; }
            set { allowNonCompact = value; }
        }


        /// <summary>
        /// True if scrape requests should be handled
        /// </summary>
        public bool AllowScrape
        {
            get { return allowScrape; }
            set { allowScrape = value; }
        }


        ///<summary>
        /// Get and set the IntervalAlgorithm used by this Tracker
        ///</summary>
        public StaticIntervalAlgorithm Intervals
        {
            get { return intervalAlgorithm; }
            set { intervalAlgorithm = value; }
        }

        #endregion Properties


        #region Constructors

        /// <summary>
        /// Creates a new tracker
        /// </summary>
        public Tracker()
        {
            allowNonCompact = true;
            allowScrape = true;
            intervalAlgorithm = new StaticIntervalAlgorithm();
            torrents = new Dictionary<byte[], SimpleTorrentManager>(new ByteComparer());
        }

        #endregion Constructors


        #region Methods

        /// <summary>
        /// Adds the trackable to the tracker so that the tracker will monitor peers for that torrent
        /// </summary>
        /// <param name="torrent">The trackable to add to the tracker</param>
        /// <returns>True if the trackable was added, false if it was already being tracked</returns>
        public bool Add(ITrackable trackable)
        {
            if (trackable == null)
                throw new ArgumentNullException("trackable");

            if (torrents.ContainsKey(trackable.InfoHash))
                return false;

            Debug.WriteLine(string.Format("Tracking Torrent: {0}", trackable.Name));
            torrents.Add(trackable.InfoHash, new SimpleTorrentManager(trackable));
            return true;
        }


        /// <summary>
        /// Gets an enumerator which iterates through all torrents which are being tracked
        /// </summary>
        /// <returns></returns>
        public IEnumerator<SimpleTorrentManager> GetEnumerator()
        {
            return this.torrents.Values.GetEnumerator();
        }


        /// <summary>
        /// This method has been hooked up to the AnnounceReceived event on registered listeners
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAnnounceReceived(object sender, AnnounceParameters e)
        {
            SimpleTorrentManager manager;

            // Check to see if we're monitoring the requested torrent
            if (!torrents.TryGetValue(e.InfoHash, out manager))
            {
                e.Response.Add(RequestParameters.FailureKey, (BEncodedString)"The requested torrent is not registered with this tracker");
                return;
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
                manager.ClearZombiePeers(DateTime.Now.AddSeconds(-this.Intervals.PeerTimeout));

                // Fulfill the announce request
                manager.GetPeers(e.Response, e.NumberWanted, e.HasRequestedCompact, e.ClientAddress);
            }

            // Make sure the values are updated
            Tracker.interval_value.Number = Intervals.Interval;
            Tracker.min_interval_value.Number = Intervals.MinInterval;

            e.Response.Add(Tracker.interval, Tracker.interval_value);
            e.Response.Add(Tracker.min_interval, Tracker.min_interval_value);
            e.Response.Add(Tracker.tracker_id, Tracker.tracker_id_value); // FIXME: Is this right?
            e.Response.Add(Tracker.complete, new BEncodedNumber(manager.Complete));
            e.Response.Add(Tracker.incomplete, new BEncodedNumber(manager.Incomplete));

            //FIXME is this the right behaivour 
            //if (par.TrackerId == null)
            //    par.TrackerId = "monotorrent-tracker";
        }


        /// <summary>
        /// This method has been hooked up to the ScrapeReceived event on registered listeners
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnScrapeReceived(object sender, ScrapeParameters e)
        {
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
            BEncodedDictionary files = new BEncodedDictionary();
            for (int i = 0; i < e.InfoHashes.Count; i++)
            {
                // FIXME: Converting infohash
                SimpleTorrentManager manager;
                string key = Toolbox.ToHex(e.InfoHashes[i]);
                if (!torrents.TryGetValue(e.InfoHashes[i], out manager))
                    continue;

                BEncodedDictionary dict = new BEncodedDictionary();
                dict.Add("complete",new BEncodedNumber( manager.Complete));
                dict.Add("downloaded", new BEncodedNumber(manager.Downloaded));
                dict.Add("incomplete", new BEncodedNumber(manager.Incomplete));
                dict.Add("name", new BEncodedString(manager.Trackable.Name));
                files.Add(key, dict);
            }

            e.Response.Add("files", files);
        }


        /// <summary>
        /// Registers a listener with the tracker so any incoming connections can be processed
        /// by the tracker. A listener can be registered with multiple trackers.
        /// </summary>
        /// <param name="listener">The listener to register with the tracker</param>
        public void RegisterListener(ListenerBase listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");

            listener.AnnounceReceived += new EventHandler<AnnounceParameters>(OnAnnounceReceived);
            listener.ScrapeReceived += new EventHandler<ScrapeParameters>(OnScrapeReceived);
        }


        /// <summary>
        /// Removes the specified torrent from the tracker and drops all peer information
        /// related to that torrent
        /// </summary>
        /// <param name="torrent">The torrent to remove</param>
        public void Remove(ITrackable trackable)
        {
            if (trackable == null)
                throw new ArgumentNullException("trackable");

            torrents.Remove(trackable.InfoHash);
        }


        /// <summary>
        /// Drops all peer information from memory and unloads all loaded torrents
        /// </summary>
        public void Reset()
        {
            Debug.WriteLine("Resetting tracker... ");
            Debug.WriteLine("Flushing data from memory... COMPLETE");
            torrents.Clear();
        }


        /// <summary>
        /// Unregisters a listener with the tracker so the tracker does not try to process incoming
        /// requests from that listener
        /// </summary>
        /// <param name="listener">The listener to unregister</param>
        public void UnregisterListener(ListenerBase listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");

            listener.AnnounceReceived -= new EventHandler<AnnounceParameters>(OnAnnounceReceived);
            listener.ScrapeReceived -= new EventHandler<ScrapeParameters>(OnScrapeReceived);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion Methods
    }
}
