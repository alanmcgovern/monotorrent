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
using System.Net.Sockets;

namespace MonoTorrent.Messages.UdpTracker
{
    public class AnnounceResponseMessage : UdpTrackerMessage
    {
        public AddressFamily AddressFamily { get; private set; }
        public override int ByteLength => (4 * 5 + Peers.Count * Stride);
        public TimeSpan Interval { get; private set; }
        public int Leechers { get; private set; }
        public List<PeerInfo> Peers { get; private set; }
        public int Seeders { get; private set; }
        int Stride => AddressFamily switch {
            AddressFamily.InterNetwork => 6,
            AddressFamily.InterNetworkV6 => 18,
            _ => throw new NotSupportedException ()
        };

        public AnnounceResponseMessage (AddressFamily addressFamily)
            : this (addressFamily, 0, TimeSpan.Zero, 0, 0, new List<PeerInfo> ())
        {

        }

        public AnnounceResponseMessage (AddressFamily addressFamily, int transactionId, TimeSpan interval, int leechers, int seeders, List<PeerInfo> peers)
            : base (1, transactionId)
        {
            AddressFamily = addressFamily;
            Interval = interval;
            Leechers = leechers;
            Peers = peers;
            Seeders = seeders;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            if (Action != ReadInt (ref buffer))
                ThrowInvalidActionException ();
            TransactionId = ReadInt (ref buffer);
            Interval = TimeSpan.FromSeconds (ReadInt (ref buffer));
            Leechers = ReadInt (ref buffer);
            Seeders = ReadInt (ref buffer);

            IList<PeerInfo> peers = PeerInfo.FromCompact (buffer, AddressFamily);
            Peers.AddRange (peers);
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, Action);
            Write (ref buffer, TransactionId);
            Write (ref buffer, (int) Interval.TotalSeconds);
            Write (ref buffer, Leechers);
            Write (ref buffer, Seeders);

            for (int i = 0; i < Peers.Count; i++)
                if (!Peers[i].TryWriteCompactPeer (buffer.Slice (i * Stride, Stride), out int dataWritten) || dataWritten != Stride)
                    throw new InvalidOperationException ();

            return written - buffer.Length;
        }
    }
}
