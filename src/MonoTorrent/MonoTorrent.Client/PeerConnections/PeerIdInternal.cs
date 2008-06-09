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
    internal class PeerIdInternal //: IComparable<PeerIdInternal>
    {
        #region Member Variables

        private int connectTime;
        private PeerId peerId;
        private Peer peer;
        private PeerConnectionBase connection;
        private ClientEngine engine;
        private TorrentManager torrentManager;
        private PeerExchangeManager pexManager;

        private String disconnectReason;

        #endregion Member Variables


        #region Properties

        /// <summary>
        /// The connection associated with this peer
        /// </summary>
        public PeerConnectionBase Connection
        {
            get { return this.connection; }
            set
            {
                if (value != null && torrentManager != null)
                    value.BitField = new BitField(torrentManager.Torrent.Pieces.Count);
                this.connection = value;
            }
        }

        public ConnectionManager ConnectionManager
        {
            get { return this.engine.ConnectionManager; }
        }
        
        public int ConnectTime
        {
            get { return connectTime; }
            set { connectTime = value; }
        }

        public ClientEngine Engine
        {
            get { return this.engine; ; }
        }

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

        internal PeerExchangeManager PeerExchangeManager
        {
            get { return this.pexManager; }
            set { this.pexManager = value; }
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
                if (value != null)
                    this.engine = value.Engine;
                if (connection != null)
                    this.Connection.BitField = new BitField(value.Torrent.Pieces.Count);
            }
        }

        /// <summary>
        /// Reason for disconnecting from this peer.
        /// </summary>
        public String DisconnectReason
        {
            get { return this.disconnectReason; }
            set { this.disconnectReason = value; }
        }

        #endregion


        #region Constructors


        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="manager"></param>
        internal PeerIdInternal(Peer peer, TorrentManager manager)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");

            this.peer = peer;
            TorrentManager = manager;
        }
        #endregion


        #region Methods

        //public int CompareTo(PeerIdInternal other)
        //{
        //    return 
        //}

        public override bool Equals(object obj)
        {
            PeerIdInternal id = obj as PeerIdInternal;
            return id == null ? false : this.peer.ConnectionUri.Equals(id.peer.ConnectionUri);
        }

        public override int GetHashCode()
        {
            return this.peer.ConnectionUri.GetHashCode();
        }

        public override string ToString()
        {
            return this.peer.ConnectionUri.ToString();
        }

        internal void UpdatePublicStats()
        {
            peerId.UpdateStats(this);
        }

        #endregion Methods
    }
}
