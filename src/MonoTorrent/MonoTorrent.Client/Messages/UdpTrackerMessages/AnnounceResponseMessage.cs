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
using System.Text;
using MonoTorrent.Client.Messages;
using System.Net;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class AnnounceResponseMessage : UdpTrackerMessage
    {
        int interval;
        int leechers;
        int seeders;

        List<Peer> peers = new List<Peer>();

        public override int ByteLength
        {
            get { return (4 * 5 + peers.Count * 6); }
        }

        public List<Peer> Peers
        {
            get { return peers; }
        }

        public AnnounceResponseMessage()
            : this(0, 0, 0, 0, new List<Peer>())
        {
            
        }

        public AnnounceResponseMessage(int transactionId, int interval, int leechers, int seeders, List<Peer> peers)
            :base(1, transactionId)
        {
            this.interval = interval;
            this.leechers = leechers;
            this.seeders = seeders;
            this.peers = peers;
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            if (Action != ReadInt(buffer, offset))
                ThrowInvalidActionException();
            TransactionId = ReadInt(buffer, offset + 4);
            interval = ReadInt(buffer, offset + 8);
            leechers = ReadInt(buffer, offset + 12);
            seeders = ReadInt(buffer, offset + 16);

            LoadPeerDetails(buffer, 20);
        }

        private void LoadPeerDetails(byte[] buffer, int offset)
        {
            while(offset <= (buffer.Length - 6))
            {
                int ip = IPAddress.NetworkToHostOrder(ReadInt(buffer, ref offset));
                ushort port = (ushort)ReadShort(buffer, ref offset);
                peers.Add(new Peer("", new Uri("tcp://" + new IPEndPoint(new IPAddress(ip), port).ToString())));
            }
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;

            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);
            written += Write(buffer, written, interval);
            written += Write(buffer, written, leechers);
            written += Write(buffer, written, seeders);

            for (int i=0; i < peers.Count; i++)
                Peers[i].CompactPeer(buffer, written + (i * 6));

            return written - offset;
        }
    }
}
