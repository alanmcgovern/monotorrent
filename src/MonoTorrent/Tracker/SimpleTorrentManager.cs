using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MonoTorrent.BEncoding;
using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{
    /// <summary>
    ///     This class is a TorrentManager which uses .Net Generics datastructures, such
    ///     as Dictionary and List to manage Peers from a Torrent.
    /// </summary>
    public class SimpleTorrentManager
    {
        #region Constructors

        public SimpleTorrentManager(ITrackable trackable, IPeerComparer comparer, Tracker tracker)
        {
            this.comparer = comparer;
            Trackable = trackable;
            this.tracker = tracker;
            complete = new BEncodedNumber(0);
            downloaded = new BEncodedNumber(0);
            incomplete = new BEncodedNumber(0);
            peers = new Dictionary<object, Peer>();
            random = new Random();
        }

        #endregion Constructors

        #region Member Variables

        private readonly IPeerComparer comparer;
        private List<Peer> buffer = new List<Peer>();
        private readonly BEncodedNumber complete;
        private readonly BEncodedNumber incomplete;
        private readonly BEncodedNumber downloaded;
        private readonly Dictionary<object, Peer> peers;
        private readonly Random random;
        private readonly Tracker tracker;

        #endregion Member Variables

        #region Properties

        /// <summary>
        ///     The number of active seeds
        /// </summary>
        public long Complete
        {
            get { return complete.Number; }
        }

        public long Incomplete
        {
            get { return incomplete.Number; }
        }

        /// <summary>
        ///     The total number of peers being tracked
        /// </summary>
        public int Count
        {
            get { return peers.Count; }
        }


        /// <summary>
        ///     The total number of times the torrent has been fully downloaded
        /// </summary>
        public long Downloaded
        {
            get { return downloaded.Number; }
        }

        /// <summary>
        ///     The torrent being tracked
        /// </summary>
        public ITrackable Trackable { get; }

        #endregion Properties

        #region Methods

        /// <summary>
        ///     Adds the peer to the tracker
        /// </summary>
        /// <param name="peer"></param>
        internal void Add(Peer peer)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");

            Debug.WriteLine("Adding: {0}", peer.ClientAddress);
            peers.Add(peer.DictionaryKey, peer);
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
        ///     Retrieves a semi-random list of peers which can be used to fulfill an Announce request
        /// </summary>
        /// <param name="response">The bencoded dictionary to add the peers to</param>
        /// <param name="count">The number of peers to add</param>
        /// <param name="compact">True if the peers should be in compact form</param>
        /// <param name="exlude">The peer to exclude from the list</param>
        internal void GetPeers(BEncodedDictionary response, int count, bool compact)
        {
            byte[] compactResponse = null;
            BEncodedList nonCompactResponse = null;

            var total = Math.Min(peers.Count, count);
            // If we have a compact response, we need to create a single BencodedString
            // Otherwise we need to create a bencoded list of dictionaries
            if (compact)
                compactResponse = new byte[total*6];
            else
                nonCompactResponse = new BEncodedList(total);

            var start = random.Next(0, peers.Count);

            lock (buffer)
            {
                if (buffer.Count != peers.Values.Count)
                    buffer = new List<Peer>(peers.Values);
            }
            var p = buffer;

            while (total > 0)
            {
                var current = p[start++%p.Count];
                if (compact)
                {
                    Buffer.BlockCopy(current.CompactEntry, 0, compactResponse, (total - 1)*6, 6);
                }
                else
                {
                    nonCompactResponse.Add(current.NonCompactEntry);
                }
                total--;
            }

            if (compact)
                response.Add(Tracker.PeersKey, (BEncodedString) compactResponse);
            else
                response.Add(Tracker.PeersKey, nonCompactResponse);
        }

        internal void ClearZombiePeers(DateTime cutoff)
        {
            var removed = false;
            lock (buffer)
            {
                foreach (var p in buffer)
                {
                    if (p.LastAnnounceTime > cutoff)
                        continue;

                    tracker.RaisePeerTimedOut(new TimedOutEventArgs(p, this));
                    peers.Remove(p.DictionaryKey);
                    removed = true;
                }

                if (removed)
                    buffer.Clear();
            }
        }


        /// <summary>
        ///     Removes the peer from the tracker
        /// </summary>
        /// <param name="peer">The peer to remove</param>
        internal void Remove(Peer peer)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");

            Debug.WriteLine("Removing: {0}", peer.ClientAddress);
            peers.Remove(peer.DictionaryKey);
            lock (buffer)
                buffer.Clear();
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            var complete = 0;
            var incomplete = 0;

            foreach (var p in peers.Values)
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
        ///     Updates the peer in the tracker database based on the announce parameters
        /// </summary>
        /// <param name="par"></param>
        internal void Update(AnnounceParameters par)
        {
            Peer peer;
            var peerKey = comparer.GetKey(par);
            if (!peers.TryGetValue(peerKey, out peer))
            {
                peer = new Peer(par, peerKey);
                Add(peer);
            }
            else
            {
                Debug.WriteLine("Updating: {0} with key {1}", peer.ClientAddress, peerKey);
                peer.Update(par);
            }
            if (par.Event == TorrentEvent.Completed)
                Interlocked.Increment(ref downloaded.number);

            else if (par.Event == TorrentEvent.Stopped)
                Remove(peer);

            tracker.RaisePeerAnnounced(new AnnounceEventArgs(peer, this));
            UpdateCounts();
        }

        #endregion Methods
    }
}