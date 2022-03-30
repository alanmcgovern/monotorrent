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
        readonly List<Peer> addedPeers;
        readonly List<Peer> droppedPeers;
        bool disposed;
        const int MAX_PEERS = 50;

        TorrentManager Manager { get; }

        #endregion Member Variables

        #region Constructors

        internal PeerExchangeManager (TorrentManager manager, PeerId id)
        {
            Manager = manager;
            this.id = id;

            addedPeers = new List<Peer> ();
            droppedPeers = new List<Peer> ();
            manager.OnPeerFound += OnAdd;
        }

        internal void OnAdd (object? source, PeerAddedEventArgs e)
        {
            addedPeers.Add (e.Peer);
        }
        // TODO onDropped!
        #endregion


        #region Methods

        internal void OnTick ()
        {
            if (!Manager.Settings.AllowPeerExchange)
                return;

            int len = (addedPeers.Count <= MAX_PEERS) ? addedPeers.Count : MAX_PEERS;
            byte[] added = new byte[len * 6];
            byte[] addedDotF = new byte[len];
            for (int i = 0; i < len; i++) {
                addedPeers[i].CompactPeer (added.AsSpan (i * 6, 6));
                if (EncryptionTypes.SupportsRC4 (addedPeers[i].AllowedEncryption)) {
                    addedDotF[i] = 0x01;
                } else {
                    addedDotF[i] = 0x00;
                }

                addedDotF[i] |= (byte) (addedPeers[i].IsSeeder ? 0x02 : 0x00);
            }
            addedPeers.RemoveRange (0, len);

            len = Math.Min (MAX_PEERS - len, droppedPeers.Count);

            byte[] dropped = new byte[len * 6];
            for (int i = 0; i < len; i++)
                droppedPeers[i].CompactPeer (dropped.AsSpan (i * 6, 6));

            droppedPeers.RemoveRange (0, len);

            (var message, var releaser) = PeerMessage.Rent<PeerExchangeMessage> ();
            message.Initialize (id.ExtensionSupports, new ReadOnlyMemory<byte> (added), new ReadOnlyMemory<byte> (addedDotF), new ReadOnlyMemory<byte> (dropped));
            id.MessageQueue.Enqueue (message, releaser);
        }

        public void Dispose ()
        {
            if (disposed)
                return;

            disposed = true;
            Manager.OnPeerFound -= OnAdd;
        }

        #endregion
    }
}
