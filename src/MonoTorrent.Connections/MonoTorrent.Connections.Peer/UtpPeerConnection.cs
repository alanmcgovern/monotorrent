//
// UtpPeerConnection.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2026 Alan McGovern
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
using System.Net;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Connections.Peer.Utp;

using ReusableTasks;

namespace MonoTorrent.Connections.Peer
{
    class UtpPeerConnection : IPeerConnection
    {
        // Most likely 'safe' limit on public internet is 1452 bytes.
        // 1500 - (28 byte ip header overhead) - (20 byte utp header overhead)
        // This will be lower if data is encapsulated in another protocol.
        //
        // Start with 1400 as that should allow a full 16kB piece to be sent
        // in 12 packets incl and also provide some headroom for encapsulation.
        //
        // Can be probed at runtime to increase/decrease as necessary.
        static readonly int InitialMtuSize = 1400;

        public ReadOnlyMemory<byte> AddressBytes { get; }
        public bool CanReconnect { get; }
        public bool Disposed { get; }
        public IPEndPoint? EndPoint { get; }
        public bool IsIncoming { get; }
        public Uri Uri { get; }


        // Implement path MTU discovery to optimise this for the uncommon case.
        // e.g. small MTU or jumbo frames.
        int CurrentMtu { get; set; } = InitialMtuSize;

        Queue<UtpPacket> ReceiveQueue = new Queue<UtpPacket> ();
        Queue<UtpPacket> SendQueue = new Queue<UtpPacket> ();

        public UtpPeerConnection (Uri uri)
            => Uri = uri;

        public ReusableTask<bool> ConnectAsync ()
        {
            throw new NotImplementedException ();
        }

        public void Dispose ()
        {
            throw new NotImplementedException ();
        }

        public ReusableTask<int> ReceiveAsync (Memory<byte> buffer)
        {
            throw new NotImplementedException ();
        }

        public ReusableTask<int> SendAsync (ReadOnlyMemory<byte> buffer)
        {
            throw new NotImplementedException ();
        }
    }
}
