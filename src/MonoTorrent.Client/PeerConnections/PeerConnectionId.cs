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
namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class PeerConnectionID : IComparable<PeerConnectionID>
    {
        #region Member Variables
        /// <summary>
        /// 
        /// </summary>
        public Peer Peer
        {
            get { return this.peer; }
            set { this.peer = value; }
        }
        private Peer peer;

        /// <summary>
        /// 
        /// </summary>
        public TorrentManager TorrentManager
        {
            get { return this.torrentManager; }
            set
            {
                this.torrentManager = value;
                this.peer.Connection.BitField = new BitField(value.Torrent.Pieces.Length);
            }
        }
        private TorrentManager torrentManager;

        internal SocketError ErrorCode;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new PeerConnectionID
        /// </summary>
        /// <param name="peer"></param>
        public PeerConnectionID(Peer peer)
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
        public PeerConnectionID(Peer peer, TorrentManager manager)
        {
            this.peer = peer;
            this.torrentManager = manager;
        }
        #endregion


        #region Overridden Methods
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            PeerConnectionID id2 = obj as PeerConnectionID;
            if (id2 == null)
                return false;

            return (this.peer.Equals(id2.peer));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return this.peer.GetHashCode();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this.peer.Location;
        }
        #endregion


        #region IComparable<PeerConnectionID> Members

        public int CompareTo(PeerConnectionID other)
        {
            return this.peer.Location.CompareTo(other.peer.Location);
        }

        #endregion
    }
}
