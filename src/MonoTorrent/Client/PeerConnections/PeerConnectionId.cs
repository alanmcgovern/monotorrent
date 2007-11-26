//
// PeerConnectionId.cs
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




using System.Net.Sockets;
using System;
using MonoTorrent.Common;
namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    internal class PeerIdInternal : IComparable<PeerIdInternal>
    {
        #region Member Variables

        public string NulledAt = string.Empty;
        private PeerId peerId;
        private Peer peer;
        private TorrentManager torrentManager;

        #endregion Member Variables


        #region Properties

        public ConnectionManager ConnectionManager
        {
            get { return this.torrentManager.Engine.ConnectionManager; }
        }

        public ClientEngine Engine
        {
            get { return torrentManager.Engine; }
        }

        internal SocketError ErrorCode;

        internal PeerId PublicId
        {
            get { return this.peerId; }
            set { this.peerId = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        internal Peer Peer
        {
            get { return this.peer; }
            set { this.peer = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        public TorrentManager TorrentManager
        {
            get { return this.torrentManager; }
            set
            {
                this.torrentManager = value;
                this.peer.Connection.BitField = new BitField(value.Torrent.Pieces.Count);
            }
        }

        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new PeerConnectionID
        /// </summary>
        /// <param name="peer"></param>
        internal PeerIdInternal(Peer peer)
        {
            this.peer = peer;
            this.torrentManager = null;
            this.peer.Connection.BitField = null;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="manager"></param>
        internal PeerIdInternal(Peer peer, TorrentManager manager)
        {
            this.peer = peer;
            this.torrentManager = manager;
        }
        #endregion


        #region Methods

        public int CompareTo(PeerIdInternal other)
        {
            return this.peer.Location.CompareTo(other.peer.Location);
        }

        public override bool Equals(object obj)
        {
            PeerIdInternal id = obj as PeerIdInternal;
            return id == null ? false : this.peer.Location == id.peer.Location;
        }

        public override int GetHashCode()
        {
            return this.peer.Location.GetHashCode();
        }

        public override string ToString()
        {
            return this.peer.Location;
        }

        internal void UpdatePublicStats()
        {
            peerId.UpdateStats(this);
        }

        #endregion Methods
    }
}
