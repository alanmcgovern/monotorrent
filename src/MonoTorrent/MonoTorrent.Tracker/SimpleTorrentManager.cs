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
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using System.Net;


namespace MonoTorrent.Tracker
{
    public class IPAddressComparer : IEqualityComparer<Peer>
    {
        public bool Equals(Peer left, Peer right)
        {
            return left.ClientAddress.Equals(right.ClientAddress);
        }

        public int GetHashCode(Peer peer)
        {
            return peer.ClientAddress.GetHashCode();
        }
    }

    ///<summary>
    ///This class is a TorrentManager which uses .Net Generics datastructures, such 
    ///as Dictionary and List to manage Peers from a Torrent.
    ///</summary>
    public class SimpleTorrentManager
    {
        #region Member Variables

        private List<Peer> buffer = new List<Peer>();
        private BEncodedNumber complete;
        private BEncodedNumber incomplete;
        private BEncodedNumber downloaded;
        private Dictionary<IPAddress, Peer> peers;
        private Random random;
        private ITrackable trackable;

        #endregion Member Variables


        #region Properties

        /// <summary>
        /// The number of active seeds
        /// </summary>
        public long Complete
        {
            get { return complete.Number; }
        }

        public long Incomplete
        {
            get
            {
                return incomplete.Number;
            }
        }

        /// <summary>
        /// The total number of peers being tracked
        /// </summary>
        public int Count
        {
            get { return peers.Count; }
        }


        /// <summary>
        /// The total number of times the torrent has been fully downloaded
        /// </summary>
        public long Downloaded
        {
            get { return downloaded.Number; }
        }

        /// <summary>
        /// The torrent being tracked
        /// </summary>
        public ITrackable Trackable
        {
            get { return trackable; }
        }

        #endregion Properties


        #region Constructors

        //public SimpleTorrentManager(ITrackable trackable)
        //    : this(trackable, new IPAddressComparer())
        //{

        //}

        // FIXME: Use the comparer
        public SimpleTorrentManager(ITrackable trackable)//, IEqualityComparer<Peer> comparer)
        {
            this.trackable = trackable;
            complete = new BEncodedNumber(0);
            downloaded = new BEncodedNumber(0);
            incomplete = new BEncodedNumber(0);
            peers = new Dictionary<IPAddress, Peer>();
            random = new Random();
        }

        #endregion Constructors


        #region Methods

        /// <summary>
        /// Adds the peer to the tracker
        /// </summary>
        /// <param name="peer"></param>
        internal void Add(Peer peer)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");

            Debug.WriteLine(string.Format("Adding: {0}", peer.ClientAddress));
            peers.Add(peer.ClientAddress.Address, peer);
            lock (buffer)
                buffer.Clear();
            UpdateCounts();
        }

        public List<Peer> GetPeers()
        {
            lock (buffer)
                return new List<Peer>(buffer);
        }

        /// <summary>
        /// Retrieves a semi-random list of peers which can be used to fulfill an Announce request
        /// </summary>
        /// <param name="response">The bencoded dictionary to add the peers to</param>
        /// <param name="count">The number of peers to add</param>
        /// <param name="compact">True if the peers should be in compact form</param>
        /// <param name="exlude">The peer to exclude from the list</param>
        internal void GetPeers(BEncodedDictionary response, int count, bool compact, IPEndPoint exlude)
        {
            byte[] compactResponse = null;
            BEncodedList nonCompactResponse = null;

            int total = Math.Min(peers.Count, count);
            // If we have a compact response, we need to create a single BencodedString
            // Otherwise we need to create a bencoded list of dictionaries
            if (compact)
                compactResponse = new byte[total * 6];
            else
                nonCompactResponse = new BEncodedList(total);

            int start = random.Next(0, peers.Count);

            lock (buffer)
            {
                if (buffer.Count != peers.Values.Count)
                    buffer = new List<Peer>(peers.Values);
            }
            List<Peer> p = buffer;

            while (total > 0)
            {
                Peer current = p[(start++) % p.Count];
                if (compact)
                {
                    Buffer.BlockCopy(current.CompactEntry, 0, compactResponse, (total - 1) * 6, 6);
                }
                else
                {
                    nonCompactResponse.Add(current.NonCompactEntry);
                }
                total--;
            }

            if (compact)
                response.Add(Tracker.peers, (BEncodedString)compactResponse);
            else
                response.Add(Tracker.peers, nonCompactResponse);
        }

        internal void ClearZombiePeers(DateTime cutoff)
        {
            lock (buffer)
            {
                buffer.ForEach(delegate(Peer p) { if (p.LastAnnounceTime < cutoff) peers.Remove(p.ClientAddress.Address); });
                buffer.Clear();
            }
        }


        /// <summary>
        /// Removes the peer from the tracker
        /// </summary>
        /// <param name="peer">The peer to remove</param>
        internal void Remove(Peer peer)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");

            Debug.WriteLine(string.Format("Removing: {0}", peer.ClientAddress));
            peers.Remove(peer.ClientAddress.Address);
            lock (buffer)
                buffer.Clear();
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            int complete = 0;
            int incomplete = 0;

            foreach (Peer p in this.peers.Values)
            {
                if (p.HasCompleted)
                    complete++;
                else
                    incomplete++;
            }

            this.complete.number = complete;
            this.incomplete.number = incomplete;
        }

        /// <summary>
        /// Updates the peer in the tracker database based on the announce parameters
        /// </summary>
        /// <param name="par"></param>
        internal void Update(AnnounceParameters par)
        {
            Peer peer;
            if (!peers.TryGetValue(par.ClientAddress.Address, out peer))
            {
                peer = new Peer(par);
                Add(peer);
            }

            Debug.WriteLine(string.Format("Updating: {0}", peer.ClientAddress));
            peer.Update(par);

            if (par.Event == TorrentEvent.Completed)
                System.Threading.Interlocked.Increment(ref downloaded.number);

            else if (par.Event == TorrentEvent.Stopped)
                Remove(peer);

            UpdateCounts();
        }

        #endregion Methods
    }
}
