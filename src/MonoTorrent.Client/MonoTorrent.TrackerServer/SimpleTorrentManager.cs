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
        public int Count => Peers.Count;

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
        Dictionary<object, Peer> Peers { get; }

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

            Peers = new Dictionary<object, Peer> ();
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

            Debug.WriteLine ($"Adding: {peer.ClientAddress}");
            Peers.Add (peer.DictionaryKey, peer);
            lock (PeersList)
                PeersList.Clear ();
            UpdateCounts ();
        }

        public List<Peer> GetPeers ()
        {
            lock (PeersList)
                return new List<Peer> (PeersList);
        }

        /// <summary>
        /// Retrieves a semi-random list of peers which can be used to fulfill an Announce request
        /// </summary>
        /// <param name="response">The bencoded dictionary to add the peers to</param>
        /// <param name="count">The number of peers to add</param>
        /// <param name="compact">True if the peers should be in compact form</param>
        internal void GetPeers (BEncodedDictionary response, int count, bool compact)
        {
            byte[]? compactResponse = null;
            BEncodedList? nonCompactResponse = null;

            int total = Math.Min (Peers.Count, count);
            // If we have a compact response, we need to create a single BencodedString
            // Otherwise we need to create a bencoded list of dictionaries
            if (compact)
                compactResponse = new byte[total * 6];
            else
                nonCompactResponse = new BEncodedList (total);

            int start = Random.Next (0, Peers.Count);

            lock (PeersList) {
                if (PeersList.Count != Peers.Values.Count)
                    PeersList = new List<Peer> (Peers.Values);
            }

            while (total > 0) {
                Peer current = PeersList[(start++) % PeersList.Count];
                if (compact) {
                    Buffer.BlockCopy (current.CompactEntry, 0, compactResponse!, (total - 1) * 6, 6);
                } else {
                    nonCompactResponse!.Add (current.NonCompactEntry);
                }
                total--;
            }

            if (compact)
                response.Add (TrackerServer.PeersKey, (BEncodedString) compactResponse!);
            else
                response.Add (TrackerServer.PeersKey, nonCompactResponse!);
        }

        internal void ClearZombiePeers (DateTime cutoff)
        {
            bool removed = false;
            lock (PeersList) {
                foreach (Peer p in PeersList) {
                    if (p.LastAnnounceTime > cutoff)
                        continue;

                    Tracker.RaisePeerTimedOut (new TimedOutEventArgs (p, this));
                    Peers.Remove (p.DictionaryKey);
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
            Peers.Remove (peer.DictionaryKey);
            lock (PeersList)
                PeersList.Clear ();
            UpdateCounts ();
        }

        void UpdateCounts ()
        {
            int tempComplete = 0;
            int tempIncomplete = 0;
            foreach (Peer p in Peers.Values) {
                if (p.HasCompleted)
                    tempComplete++;
                else
                    tempIncomplete++;
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
            object peerKey = Comparer.GetKey (par);
            if (!Peers.TryGetValue (peerKey, out Peer? peer)) {
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
