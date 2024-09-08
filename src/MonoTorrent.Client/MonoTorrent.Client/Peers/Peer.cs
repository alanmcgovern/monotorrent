//
// Peer.cs
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

using MonoTorrent.BEncoding;
using MonoTorrent.Connections;

namespace MonoTorrent.Client
{
    class Peer : IEquatable<Peer>
    {
        /// <summary>
        /// The number of times this peer has had it's connection closed
        /// </summary>
        internal int CleanedUpCount { get; set; }

        /// <summary>
        /// The number of times we failed to establish an outgoing connection to this peer.
        /// </summary>
        internal int FailedConnectionAttempts { get; set; }

        /// <summary>
        /// A cache of the last known seeding state of this peer. This is used to avoid connecting to seeders when 100%
        /// of the torrent has been downloaded.
        /// </summary>
        internal bool IsSeeder { get; set; }

        /// <summary>
        /// The local port this peer is listening for connections on.
        /// </summary>
        internal int LocalPort { get; set; }

        /// <summary>
        /// A stale peer is one which came from an old announce/DHT/peer exchange request. These
        /// peers may still be contactable, but if a new peer is provided via one of the normal
        /// mechanisms then the new peer should replace any stale peers in the event the torrent
        /// is already holding the maximum number of peers.
        /// </summary>
        internal bool MaybeStale { get; set; }

        internal PeerInfo Info { get; private set; }

        /// <summary>
        /// The number of times, in a row, that this peer has sent us the blocks for a piece and that
        /// piece failed the hash check.
        /// </summary>
        internal int RepeatedHashFails { get; set; }

        /// <summary>
        /// This is the overall count for the number of pieces which failed the hash check after being
        /// received from this peer.
        /// </summary>
        internal int TotalHashFails { get; set; }

        /// <summary>
        /// The time since the last outgoing connection was attempted
        /// </summary>
        internal ValueStopwatch WaitUntilNextConnectionAttempt;

        public Peer (PeerInfo peerInfo)
        {
            Info = peerInfo ?? throw new ArgumentNullException (nameof (peerInfo));
        }

        public override bool Equals (object? obj)
            => Equals (obj as Peer);

        public bool Equals (Peer? other)
            => Info.Equals (other?.Info);

        public override int GetHashCode ()
            => Info.GetHashCode ();

        internal void HashedPiece (bool succeeded)
        {
            if (succeeded && RepeatedHashFails > 0)
                RepeatedHashFails--;

            if (!succeeded) {
                RepeatedHashFails++;
                TotalHashFails++;
            }
        }

        public override string ToString ()
            => Info.ConnectionUri.ToString ();

        internal byte[] CompactPeer ()
            => Info.CompactPeer ();

        internal bool TryWriteCompactPeer (Span<byte> buffer, out int written)
            => Info.TryWriteCompactPeer (buffer, out written);

        internal static BEncodedList Encode (IEnumerable<Peer> peers)
        {
            var list = new BEncodedList ();
            foreach (Peer p in peers)
                list.Add ((BEncodedString) p.Info.CompactPeer ());
            return list;
        }

        internal void UpdatePeerId (BEncodedString peerId)
            => Info = new PeerInfo (Info.ConnectionUri, peerId, Info.MaybeSeeder);
    }
}
