//
// SimpleTorrentManager.cs
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
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;

using MonoTorrent.BEncoding;

namespace MonoTorrent.TrackerServer
{
    ///<summary>
    ///This class is a TorrentManager which uses .Net Generics datastructures, such 
    ///as Dictionary and List to manage Peers from a Torrent.
    ///</summary>
    class SimpleTorrentManager : ITrackerItem
    {

        /// <summary>
        /// Used to check whether two <see cref="Peer"/> objects are the same.
        /// </summary>
        IPeerComparer Comparer { get; }

        /// <summary>
        /// The number of active seeds (peers which have fully downloaded the torrent).
        /// </summary>
        public int Complete { get; private set; }

        /// <summary>
        /// The number of active peers.
        /// </summary>
        public int Count => PeersIPv4.Count + PeersIPv6.Count;

        /// <summary>
        /// The number of times the torrent has been fully downloaded.
        /// </summary>
        public int Downloaded { get; private set; }

        /// <summary>
        /// The number of active leeches (peers which have not fully downloaded the torrent).
        /// </summary>
        public int Incomplete { get; private set; }

        /// <summary>
        /// Used to choose the start point in the peer list when choosing the peers to return.
        /// </summary>
        Random Random { get; }

        /// <summary>
        /// A dictionary containing all the peers
        /// </summary>
        Dictionary<object, Peer> PeersIPv4 { get; }

        /// <summary>
        /// A dictionary containing all the peers
        /// </summary>
        Dictionary<object, Peer> PeersIPv6 { get; }

        /// <summary>
        /// A list which used used to reduce allocations when generating responses to announce requests.
        /// </summary>
        List<Peer> PeersList { get; set; }

        /// <summary>
        /// The torrent being tracked
        /// </summary>
        public ITrackable Trackable { get; }

        /// <summary>
        /// A reference to the TrackerServer associated with this torrent.
        /// </summary>
        TrackerServer Tracker { get; }

        public SimpleTorrentManager (ITrackable trackable, IPeerComparer comparer, TrackerServer tracker)
        {
            Comparer = comparer;
            Trackable = trackable;
            Tracker = tracker;

            PeersIPv4 = new Dictionary<object, Peer> ();
            PeersIPv6 = new Dictionary<object, Peer> ();
            PeersList = new List<Peer> ();
            Random = new Random ();
        }

        #region Methods

        /// <summary>
        /// Adds the peer to the tracker
        /// </summary>
        /// <param name="peer"></param>
        internal void Add (Peer peer)
        {
            if (peer == null)
                throw new ArgumentNullException (nameof (peer));

            var peers = peer.ClientAddress.AddressFamily switch {
                AddressFamily.InterNetwork => PeersIPv4,
                AddressFamily.InterNetworkV6 => PeersIPv6,
                _ => throw new NotSupportedException ($"AddressFamily.{peer.ClientAddress.AddressFamily} is unsupported")
            };
            Debug.WriteLine ($"Adding: {peer.ClientAddress}");
            peers.Add (peer.DictionaryKey, peer);
            lock (PeersList)
                PeersList.Clear ();
            UpdateCounts ();
        }

        public List<Peer> GetPeers (AddressFamily addressFamily)
        {
            lock (PeersList)
                return new List<Peer> (PeersList.Where (t => t.ClientAddress.AddressFamily == addressFamily));
        }

        /// <summary>
        /// Retrieves a semi-random list of peers which can be used to fulfill an Announce request
        /// </summary>
        /// <param name="response">The bencoded dictionary to add the peers to</param>
        /// <param name="count">The number of peers to add</param>
        /// <param name="compact">True if the peers should be in compact form</param>
        /// <param name="addressFamily"></param>
        internal void GetPeers (BEncodedDictionary response, int count, bool compact, AddressFamily addressFamily)
        {
            byte[]? compactResponse = null;
            BEncodedList? nonCompactResponse = null;

            (int stride, Dictionary<object, Peer> peers, BEncodedString peersKey) = addressFamily switch {
                AddressFamily.InterNetwork => (4 + 2, PeersIPv4, TrackerServer.PeersKey),
                AddressFamily.InterNetworkV6 => (16 + 2, PeersIPv6, compact ? TrackerServer.Peers6Key : TrackerServer.PeersKey),
                _ => throw new NotSupportedException ($"AddressFamily.{addressFamily} is unsupported")
            };

            int total = Math.Min (peers.Count, count);
            // If we have a compact response, we need to create a single BencodedString
            // Otherwise we need to create a bencoded list of dictionaries
            if (compact)
                compactResponse = new byte[total * stride];
            else
                nonCompactResponse = new BEncodedList (total);

            int start = Random.Next (0, peers.Count);

            lock (PeersList) {
                if (PeersList.Count != peers.Values.Count)
                    PeersList = new List<Peer> (peers.Values);
            }

            while (total > 0) {
                Peer current = PeersList[(start++) % PeersList.Count];
                if (compact) {
                    Buffer.BlockCopy (current.CompactEntry, 0, compactResponse!, (total - 1) * current.CompactEntry.Length, current.CompactEntry.Length);
                } else {
                    nonCompactResponse!.Add (current.NonCompactEntry);
                }
                total--;
            }

            if (compact)
                response.Add (peersKey, (BEncodedString) compactResponse!);
            else
                response.Add (peersKey, nonCompactResponse!);
        }

        internal void ClearZombiePeers (DateTime cutoff)
        {
            bool removed = false;
            lock (PeersList) {
                foreach (Peer p in PeersList) {
                    if (p.LastAnnounceTime > cutoff)
                        continue;

                    Tracker.RaisePeerTimedOut (new TimedOutEventArgs (p, this));
                    PeersIPv4.Remove (p.DictionaryKey);
                    PeersIPv6.Remove (p.DictionaryKey);
                    removed = true;
                }

                if (removed)
                    PeersList.Clear ();
            }
        }


        /// <summary>
        /// Removes the peer from the tracker
        /// </summary>
        /// <param name="peer">The peer to remove</param>
        internal void Remove (Peer peer)
        {
            if (peer == null)
                throw new ArgumentNullException (nameof (peer));

            Debug.WriteLine ($"Removing: {peer.ClientAddress}");
            PeersIPv4.Remove (peer.DictionaryKey);
            PeersIPv6.Remove (peer.DictionaryKey);
            lock (PeersList)
                PeersList.Clear ();
            UpdateCounts ();
        }

        void UpdateCounts ()
        {
            int tempComplete = 0;
            int tempIncomplete = 0;
            foreach (var dict in new[] { PeersIPv4, PeersIPv6 }) {
                foreach (Peer p in dict.Values) {
                    if (p.HasCompleted)
                        tempComplete++;
                    else
                        tempIncomplete++;
                }
            }

            Complete = tempComplete;
            Incomplete = tempIncomplete;
        }

        /// <summary>
        /// Updates the peer in the tracker database based on the announce parameters
        /// </summary>
        /// <param name="par"></param>
        internal void Update (AnnounceRequest par)
        {
            var peers = par.ClientAddress.AddressFamily switch {
                AddressFamily.InterNetwork => PeersIPv4,
                AddressFamily.InterNetworkV6 => PeersIPv6,
                _ => throw new NotSupportedException ($"AddressFamily.{par.ClientAddress.AddressFamily} is unsupported")
            };

            object peerKey = Comparer.GetKey (par);
            if (!peers.TryGetValue (peerKey, out Peer? peer)) {
                peer = new Peer (par, peerKey);
                Add (peer);
            } else {
                Debug.WriteLine ($"Updating: {peer.ClientAddress} with key {peerKey}");
                peer.Update (par);
            }
            if (par.Event == TorrentEvent.Completed)
                Downloaded++;

            else if (par.Event == TorrentEvent.Stopped)
                Remove (peer);

            Tracker.RaisePeerAnnounced (new AnnounceEventArgs (peer, this));
            UpdateCounts ();
        }

        #endregion Methods
    }
}
