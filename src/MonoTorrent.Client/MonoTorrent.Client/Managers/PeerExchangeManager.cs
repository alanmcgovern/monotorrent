//
// PeerExchangeManager.cs
//
// Authors:
//   Olivier Dufour olivier.duff@gmail.com
//
// Copyright (C) 2006 Olivier Dufour
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

using MonoTorrent.Connections;
using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.Libtorrent;

namespace MonoTorrent.Client
{
    /// <summary>
    /// This class is used to send each minute a peer excahnge message to peer who have enable this protocol
    /// </summary>
    class PeerExchangeManager
    {
        #region Member Variables

        readonly PeerId PeerId;
        readonly Queue<PeerId> addedPeers;
        readonly Queue<PeerId> droppedPeers;

        readonly Queue<PeerId> added6Peers;
        readonly Queue<PeerId> dropped6Peers;

        // Peers are about 7 bytes each (if you include the 'dotf' data)
        // Calculate the max peers we can fit in the buffer.
        static readonly int BufferSize = ByteBufferPool.SmallMessageBufferSize;
        static readonly int MAX_PEERS = BufferSize / (4 + 2 + 1); // ipv4 bytes, port bytes, 'dotf' byte 
        static readonly int MAX_PEERS6 = BufferSize / (16 + 2 + 1); // ipv6 bytes, port bytes, 'dotf' byte

        IPeerExchangeSource Manager { get; }

        #endregion Member Variables

        #region Constructors

        internal PeerExchangeManager (IPeerExchangeSource manager, PeerId id)
        {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            PeerId = id ?? throw new ArgumentNullException(nameof(id));

            addedPeers = new Queue<PeerId> ();
            droppedPeers = new Queue<PeerId> ();

            added6Peers = new Queue<PeerId> ();
            dropped6Peers = new Queue<PeerId> ();
        }

        internal void OnAdd (PeerId peer)
        {
            ClientEngine.MainLoop.CheckThread ();
            // IPv4 peers will share with IPv4 peers, IPv6 share with 
            if (peer.Peer.Info.ConnectionUri.Scheme == "ipv4")
                addedPeers.Enqueue (peer);
            else if (peer.Peer.Info.ConnectionUri.Scheme == "ipv6")
                added6Peers.Enqueue (peer);
        }

        internal void OnDrop (PeerId peer)
        {
            ClientEngine.MainLoop.CheckThread ();
            if (peer.Peer.Info.ConnectionUri.Scheme == "ipv4")
                droppedPeers.Enqueue (peer);
            else if (peer.Peer.Info.ConnectionUri.Scheme == "ipv6")
                dropped6Peers.Enqueue (peer);
        }
        #endregion

        #region Methods

        internal void OnTick ()
        {
            // Do nothing if PEX is disabled.
            if (!Manager.Settings.AllowPeerExchange)
                return;

            // Do nothing if the four lists are empty.
            if (addedPeers.Count == 0 && droppedPeers.Count == 0 && added6Peers.Count == 0 && dropped6Peers.Count == 0)
                return;

            // Prepare the message and it's content.
            Memory<byte> added = default, addedDotF = default, dropped = default, added6 = default, added6DotF = default, dropped6 = default;
            ByteBufferPool.Releaser memoryReleaser = default;
            // Preferentially send ipv4 peers first until those lists are empty. Then send ipv6 peers.
            // Fix this by using a larger buffer, or randomise the order in which this happens.
            (var message, var releaser) = PeerMessage.Rent<PeerExchangeMessage> ();
            if (addedPeers.Count > 0 || droppedPeers.Count > 0) {
                (added, addedDotF, dropped, memoryReleaser) = Populate (6, MAX_PEERS, addedPeers, droppedPeers);
            } else if (added6Peers.Count > 0 || dropped6Peers.Count > 0) {
                (added, addedDotF, dropped, memoryReleaser) = Populate (18, MAX_PEERS, addedPeers, droppedPeers);
            }

            // Populate it with what we have!
            message.Initialize (new ExtensionSupports (new[] { PeerExchangeMessage.Support }), added, addedDotF, dropped, added6, added6DotF, dropped6, memoryReleaser);
            PeerId.MessageQueue.Enqueue (message, releaser);
        }

        static (Memory<byte> added, Memory<byte> addedDotF, Memory<byte> dropped, ByteBufferPool.Releaser memoryReleaser) Populate (int stride, int maxPeers, Queue<PeerId> addedPeers, Queue<PeerId> droppedPeers)
        {
            int len = (addedPeers.Count <= maxPeers) ? addedPeers.Count : maxPeers;
            var memoryReleaser = MemoryPool.Default.Rent (BufferSize, out Memory<byte> memory);
            var added = memory.Slice (0, len * stride);
            var addedDotF = memory.Slice (added.Length, len);

            for (int i = 0; i < len; i++) {
                var peer = addedPeers.Dequeue ();
                if (!peer.Peer.TryWriteCompactPeer (added.Span.Slice (i * stride, stride), out int written) || written != stride)
                    throw new NotSupportedException ();

                // FIXME: Decide whether to tell *other* peers if we believe *this* peer prefers encryption or not. I'm not sure
                // how this particular decision can be made. We can't ask peers whether or not they prefer encryption, or just happened
                // to use it because it's what the local monotorrent was configured to prefer/require.
                if (peer.EncryptionType == EncryptionType.RC4Full || peer.EncryptionType == EncryptionType.RC4Header) {
                    addedDotF.Span[i] = 0x01;
                } else {
                    addedDotF.Span[i] = 0x00;
                }

                addedDotF.Span[i] |= (byte) (peer.IsSeeder ? 0x02 : 0x00);
            }

            // The remainder of our buffer can be filled with dropped peers.
            // We do some math to slice the remainder of the original memory
            // buffer to an even multiple of 'stride'. Then we calculate how many
            // peers we actually want to put in it, and then we slice it one
            // more time if we don't have enough dropped peers.
            var dropped = memory.Slice (added.Length + addedDotF.Length);
            dropped = dropped.Slice (0, (dropped.Length / stride) * stride);
            len = Math.Min (dropped.Length / stride, droppedPeers.Count);
            dropped = dropped.Slice (0, len * stride);

            for (int i = 0; i < len; i++)
                if (!droppedPeers.Dequeue ().Peer.TryWriteCompactPeer (dropped.Span.Slice (i * stride, stride), out int written) || written != stride)
                    throw new NotSupportedException ();

            return (added, addedDotF, dropped, memoryReleaser);
        }

        #endregion
    }
}
