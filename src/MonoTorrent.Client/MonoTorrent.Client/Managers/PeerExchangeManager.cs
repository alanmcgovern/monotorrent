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
    class PeerExchangeManager : IDisposable
    {
        #region Member Variables

        readonly PeerId id;
        readonly Queue<PeerId> addedPeers;
        readonly Queue<PeerId> droppedPeers;
        bool disposed;

        // Peers are about 7 bytes each (if you include the 'dotf' data)
        // Calculate the max peers we can fit in the buffer.
        static readonly int BufferSize = ByteBufferPool.SmallMessageBufferSize;
        static readonly int MAX_PEERS = ByteBufferPool.SmallMessageBufferSize / 7;

        TorrentManager Manager { get; }

        #endregion Member Variables

        #region Constructors

        internal PeerExchangeManager (TorrentManager manager, PeerId id)
        {
            Manager = manager;
            this.id = id;

            addedPeers = new Queue<PeerId> ();
            droppedPeers = new Queue<PeerId> ();
            manager.PeerConnected += OnAdd;
        }

        internal void OnAdd (object? source, PeerConnectedEventArgs args)
        {
            addedPeers.Enqueue (args.Peer);
        }
        // TODO onDropped!
        #endregion


        #region Methods

        internal void OnTick ()
        {
            if (!Manager.Settings.AllowPeerExchange)
                return;

            int len = (addedPeers.Count <= MAX_PEERS) ? addedPeers.Count : MAX_PEERS;
            var memoryReleaser = MemoryPool.Default.Rent (BufferSize, out Memory<byte> memory);
            var added = memory.Slice (0, len * 6);
            var addedDotF = memory.Slice (added.Length, len);

            for (int i = 0; i < len; i++) {
                var peer = addedPeers.Dequeue ();
                peer.Peer.CompactPeer (added.Span.Slice (i * 6, 6));
                if (EncryptionTypes.SupportsRC4 (peer.Peer.AllowedEncryption)) {
                    addedDotF.Span[i] = 0x01;
                } else {
                    addedDotF.Span[i] = 0x00;
                }

                addedDotF.Span[i] |= (byte) (peer.IsSeeder ? 0x02 : 0x00);
            }

            // The remainder of our buffer can be filled with dropped peers.
            // We do some math to slice the remainder of the original memory
            // buffer to an even multiple of 6. Then we calculate how many
            // peers we actually want to put in it, and then we slice it one
            // more time if we don't have enough dropped peers.
            var dropped = memory.Slice (added.Length + addedDotF.Length);
            dropped = dropped.Slice (0, (dropped.Length / 6) * 6);
            len = Math.Min (dropped.Length / 6, droppedPeers.Count);
            dropped = dropped.Slice (0, len * 6);

            for (int i = 0; i < len; i++)
                droppedPeers.Dequeue ().Peer.CompactPeer (dropped.Span.Slice (i * 6, 6));

            (var message, var releaser) = PeerMessage.Rent<PeerExchangeMessage> ();
            message.Initialize (id.ExtensionSupports, added, addedDotF, dropped, memoryReleaser);
            id.MessageQueue.Enqueue (message, releaser);
        }

        public void Dispose ()
        {
            if (disposed)
                return;

            disposed = true;
            Manager.PeerConnected -= OnAdd;
        }

        #endregion
    }
}
