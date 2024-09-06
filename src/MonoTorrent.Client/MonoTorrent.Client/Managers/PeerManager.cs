//
// PeerManager.cs
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


using System.Collections.Generic;

namespace MonoTorrent.Client
{
    public class PeerManager
    {
        internal List<PeerId> ConnectedPeers;
        internal List<Peer> ConnectingToPeers;

        internal List<Peer> ActivePeers;
        internal List<Peer> AvailablePeers;

        /// <summary>
        /// The number of peers which are available to be connected to.
        /// </summary>
        public int Available { get; private set; }

        /// <summary>
        /// Returns the number of Leechs we are currently connected to.
        /// </summary>
        /// <returns></returns>
        public int Leechs { get; private set; }

        /// <summary>
        /// Returns the number of Seeds we are currently connected to.
        /// </summary>
        /// <returns></returns>
        public int Seeds { get; private set; }

        /// <summary>
        /// This is the total number of known peers. It is the sum of <see cref="Seeds"/>, <see cref="Leechs"/> and <see cref="Available"/>.
        /// </summary>
        internal int TotalPeers => ActivePeers.Count + AvailablePeers.Count;

        internal PeerManager ()
        {
            ConnectedPeers = new List<PeerId> ();
            ConnectingToPeers = new List<Peer> ();

            ActivePeers = new List<Peer> ();
            AvailablePeers = new List<Peer> ();
        }

        internal void ClearAll ()
        {
            ConnectedPeers.Clear ();
            ConnectingToPeers.Clear ();

            ActivePeers.Clear ();
            AvailablePeers.Clear ();
        }

        internal bool Contains (Peer peer)
        {
            return ActivePeers.Contains (peer)
                || AvailablePeers.Contains (peer);
        }

        internal void UpdatePeerCounts ()
        {
            int seeds = 0;
            int leeches = 0;
            for (int i = 0; i < ActivePeers.Count; i++) {
                if (ActivePeers[i].IsSeeder)
                    seeds++;
                else
                    leeches++;
            }

            Available = AvailablePeers.Count;
            Seeds = seeds;
            Leechs = leeches;
        }
    }
}
