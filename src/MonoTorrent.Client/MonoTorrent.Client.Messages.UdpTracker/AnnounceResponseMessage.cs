//
// AnnouceResponseMessage.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class AnnounceResponseMessage : UdpTrackerMessage
    {
        public override int ByteLength => (4 * 5 + Peers.Count * 6);
        public TimeSpan Interval { get; private set; }
        public int Leechers { get; private set; }
        public List<Peer> Peers { get; private set; }
        public int Seeders { get; private set; }

        public AnnounceResponseMessage ()
            : this (0, TimeSpan.Zero, 0, 0, new List<Peer> ())
        {

        }

        public AnnounceResponseMessage (int transactionId, TimeSpan interval, int leechers, int seeders, List<Peer> peers)
            : base (1, transactionId)
        {
            Interval = interval;
            Leechers = leechers;
            Peers = peers;
            Seeders = seeders;
        }

        public override void Decode (byte[] buffer, int offset, int length)
        {
            if (Action != ReadInt (buffer, offset))
                ThrowInvalidActionException ();
            TransactionId = ReadInt (buffer, offset + 4);
            Interval = TimeSpan.FromSeconds (ReadInt (buffer, offset + 8));
            Leechers = ReadInt (buffer, offset + 12);
            Seeders = ReadInt (buffer, offset + 16);

            IList<Peer> peers = Peer.FromCompact (buffer, 20);
            Peers.AddRange (peers);
        }

        public override int Encode (byte[] buffer, int offset)
        {
            int written = offset;

            written += Write (buffer, written, Action);
            written += Write (buffer, written, TransactionId);
            written += Write (buffer, written, (int) Interval.TotalSeconds);
            written += Write (buffer, written, Leechers);
            written += Write (buffer, written, Seeders);

            for (int i = 0; i < Peers.Count; i++)
                Peers[i].CompactPeer (buffer, written + (i * 6));

            return written - offset;
        }
    }
}
