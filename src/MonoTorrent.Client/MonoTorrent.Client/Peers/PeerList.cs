//
// PeerConnectionId.cs
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

namespace MonoTorrent.Client
{
    class PeerList
    {
        #region Private Fields

        readonly List<PeerId> peers; //Peers held
        readonly PeerListType listType; //The type of list this represents
        int scanIndex; //Position in the list when scanning peers

        #endregion Private Fields

        #region Constructors

        public PeerList (PeerListType ListType)
        {
            peers = new List<PeerId> ();
            listType = ListType;
        }

        #endregion

        #region Public Properties

        public int Count
            => peers.Count;

        public bool MorePeers
            => scanIndex < peers.Count;

        #endregion

        #region Public Methods

        public void Add (PeerId peer)
        {
            peers.Add (peer);
        }

        public void Clear ()
        {
            peers.Clear ();
            scanIndex = 0;
        }

        public PeerId? GetNextPeer ()
        {
            if (scanIndex < peers.Count) {
                scanIndex++;
                return peers[scanIndex - 1];
            } else
                return null;
        }

        public PeerId? GetFirstInterestedChokedPeer ()
        {
            //Look for a choked peer
            foreach (PeerId peer in peers)
                if (peer.IsInterested && peer.AmChoking)
                    return peer;
            //None found, return null
            return null;
        }

        public PeerId? GetOUPeer ()
        {
            //Look for an untried peer that we haven't unchoked, or else return the choked peer with the longest unchoke interval
            PeerId? longestIntervalPeer = null;
            double longestIntervalPeerTime = 0;
            foreach (PeerId peer in peers)
                if (peer.IsInterested && peer.AmChoking) {
                    if (!peer.LastUnchoked.IsRunning)
                        //This is an untried peer that we haven't unchoked, return it
                        return peer;
                    else {
                        //This is an unchoked peer that we have unchoked in the past
                        //If this is the first one we've found, remember it
                        if (longestIntervalPeer == null)
                            longestIntervalPeer = peer;
                        else {
                            // Compare dates to determine whether the new one has a longer interval (but halve the interval
                            // if the peer has never sent us any data)
                            double newInterval = peer.LastUnchoked.Elapsed.TotalSeconds;
                            if (peer.Monitor.DataBytesReceived == 0)
                                newInterval /= 2;
                            if (newInterval > longestIntervalPeerTime) {
                                //The new peer has a longer interval than the current one, replace it
                                longestIntervalPeer = peer;
                                longestIntervalPeerTime = newInterval;
                            }
                        }
                    }
                }
            //Return the peer with the longest interval since it was unchoked, or null if none found
            return longestIntervalPeer;
        }

        public bool Contains (PeerId peer)
            => peers.Contains (peer);

        public void Sort (bool IsSeeding)
        {
            switch (listType) {
                case (PeerListType.NascentPeers):
                    peers.Sort (CompareNascentPeers);
                    break;

                case (PeerListType.CandidatePeers):
                    if (IsSeeding)
                        peers.Sort (CompareCandidatePeersWhileSeeding);
                    else
                        peers.Sort (CompareCandidatePeersWhileDownloading);
                    break;

                case (PeerListType.OptimisticUnchokeCandidatePeers):
                    if (IsSeeding)
                        peers.Sort (CompareOptimisticUnchokeCandidatesWhileSeeding);
                    else
                        peers.Sort (CompareOptimisticUnchokeCandidatesWhileDownloading);
                    break;
            }
        }

        #endregion

        #region Private Methods

        static int CompareCandidatePeersWhileDownloading (PeerId P1, PeerId P2)
        {
            //Comparer for candidate peers for use when the torrent is downloading
            //First sort Am interested before !AmInterested
            if (P1.AmInterested && !P2.AmInterested)
                return -1;
            else if (!P1.AmInterested && P2.AmInterested)
                return 1;

            //Both have the same AmInterested status, sort by download rate highest first
            return P2.LastReviewDownloadRate.CompareTo (P1.LastReviewDownloadRate);
        }

        static int CompareCandidatePeersWhileSeeding (PeerId P1, PeerId P2)
        {
            //Comparer for candidate peers for use when the torrent is seeding
            //Sort by upload rate, largest first
            return P2.LastReviewUploadRate.CompareTo (P1.LastReviewUploadRate);
        }

        static int CompareNascentPeers (PeerId P1, PeerId P2)
        {
            //Comparer for nascent peers
            //Sort most recent first
            if (P1.LastUnchoked.Elapsed > P2.LastUnchoked.Elapsed)
                return -1;
            else if (P1.LastUnchoked.Elapsed < P2.LastUnchoked.Elapsed)
                return 1;
            else
                return 0;
        }

        static int CompareOptimisticUnchokeCandidatesWhileDownloading (PeerId P1, PeerId P2)
        {
            //Comparer for optimistic unchoke candidates

            //Start by sorting peers that have given us most data before to the top
            if (P1.Monitor.DataBytesReceived > P2.Monitor.DataBytesReceived)
                return -1;
            else if (P1.Monitor.DataBytesReceived < P2.Monitor.DataBytesReceived)
                return 1;

            //Amount of data sent is equal (and probably 0), sort untried before tried
            if (!P1.LastUnchoked.IsRunning && P2.LastUnchoked.IsRunning)
                return -1;
            else if (P1.LastUnchoked.IsRunning && !P2.LastUnchoked.IsRunning)
                return 1;
            else if (!P1.LastUnchoked.IsRunning && !P2.LastUnchoked.IsRunning)
                //Both untried, nothing to choose between them
                return 0;

            //Both peers have been unchoked
            //Sort into descending order (most recent first)
            if (P1.LastUnchoked.Elapsed > P2.LastUnchoked.Elapsed)
                return -1;
            else if (P1.LastUnchoked.Elapsed < P2.LastUnchoked.Elapsed)
                return 1;
            else
                return 0;
        }

        static int CompareOptimisticUnchokeCandidatesWhileSeeding (PeerId P1, PeerId P2)
        {
            //Comparer for optimistic unchoke candidates

            //Start by sorting peers that we have sent most data to before to the top
            if (P1.Monitor.DataBytesSent > P2.Monitor.DataBytesSent)
                return -1;
            else if (P1.Monitor.DataBytesSent < P2.Monitor.DataBytesSent)
                return 1;

            //Amount of data sent is equal (and probably 0), sort untried before tried
            if (!P1.LastUnchoked.IsRunning && P2.LastUnchoked.IsRunning)
                return -1;
            else if (P1.LastUnchoked.IsRunning && !P2.LastUnchoked.IsRunning)
                return 1;
            else if (!P1.LastUnchoked.IsRunning && P2.LastUnchoked.IsRunning)
                //Both untried, nothing to choose between them
                return 0;

            //Both peers have been unchoked
            //Sort into descending order (most recent first)
            if (P1.LastUnchoked.Elapsed > P2.LastUnchoked.Elapsed)
                return -1;
            else if (P1.LastUnchoked.Elapsed < P2.LastUnchoked.Elapsed)
                return 1;
            else
                return 0;
        }

        internal void RemoveDisconnected ()
        {
            for (int i = 0; i < peers.Count; i ++) {
                if (peers[i].Disposed) {
                    peers.RemoveAt (i);
                    if (scanIndex >= i && scanIndex > 0)
                        scanIndex--;
                    i--;
                }
            }
        }

        #endregion
    }
}
