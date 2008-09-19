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

using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Common;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client
{
    /// <summary>
    /// This class is used to send each minute a peer excahnge message to peer who have enable this protocol
    /// </summary>
    public class PeerExchangeManager : IDisposable
    {
        #region Member Variables

        private PeerId id;
        private List<Peer> addedPeers;
        private List<Peer> droppedPeers;
        private bool disposed = false;
        private const int MAX_PEERS = 50;

        #endregion Member Variables

        #region Constructors

        internal PeerExchangeManager(PeerId id)
        {
            this.id = id;
			this.addedPeers = new List<Peer>();
			this.droppedPeers = new List<Peer>();
            id.TorrentManager.OnPeerFound += new EventHandler<PeerAddedEventArgs>(OnAdd);
            Start();
        }

        internal void OnAdd(object source, PeerAddedEventArgs e)
        {
            addedPeers.Add(e.Peer);
        }
        // TODO onDropped!
        #endregion


        #region Methods

        internal void Start()
        {
            ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromMinutes(1), delegate {
                if(disposed)
                    return false;
                OnTick ();
                return true;
            });
        }

        internal void OnTick()
        {
            int len = (addedPeers.Count <= MAX_PEERS) ? addedPeers.Count : MAX_PEERS;
            byte[] added = new byte[len * 6];
            byte[] addedDotF = new byte[len];
            for (int i = 0; i < len; i++)
            {
                addedPeers[i].CompactPeer(added, i * 6);
                if ((addedPeers[i].Encryption & (EncryptionTypes.RC4Full | EncryptionTypes.RC4Header)) != EncryptionTypes.None)
                {
                    addedDotF[i] = 0x01;
                }
                else
                {
                    addedDotF[i] = 0x00;
                }

                addedDotF[i] |= (byte)(addedPeers[i].IsSeeder ? 0x02 : 0x00);
            }
            addedPeers.RemoveRange(0, len);

            len = Math.Min(MAX_PEERS - len, droppedPeers.Count);

            byte[] dropped = new byte[len * 6];
            for (int i = 0; i < len; i++)
                droppedPeers[i].CompactPeer(dropped, i * 6);

            droppedPeers.RemoveRange(0, len);
            id.Enqueue(new PeerExchangeMessage(added, addedDotF, dropped));
        }

        public void Dispose()
        {
            if(disposed)
                return;

            disposed = true;
            id.TorrentManager.OnPeerFound -= new EventHandler<PeerAddedEventArgs>(OnAdd);
        }

        #endregion
    }
}
