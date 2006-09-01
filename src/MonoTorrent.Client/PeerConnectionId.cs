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



using MonoTorrent.Common;
namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class PeerConnectionID : IPeerConnectionID
    {
        #region Member Variables
        /// <summary>
        /// 
        /// </summary>
        public ClientPeer Peer
        {
            get { return this.peer; }
            set { this.peer = value; }
        }
        private ClientPeer peer;

        /// <summary>
        /// 
        /// </summary>
        public TorrentManager TorrentManager
        {
            get { return this.torrentManager; }
            set
            {
                this.torrentManager = value;
                this.peer.BitField = new BitField(value.Torrent.Pieces.Length);
            }
        }
        private TorrentManager torrentManager;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new PeerConnectionID
        /// </summary>
        /// <param name="peer"></param>
        public PeerConnectionID(ClientPeer peer)
        {
            this.peer = peer;
            this.torrentManager = null;
            this.peer.BitField = null;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="manager"></param>
        public PeerConnectionID(ClientPeer peer, TorrentManager manager)
        {
            this.peer = peer;
            this.torrentManager = manager;
            this.peer.BitField = new BitField(manager.Torrent.Pieces.Length);
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
        #endregion


        IPeer IPeerConnectionID.Peer
        {
            get { return this.peer; }
        }

        ITorrentManager IPeerConnectionID.TorrentManager
        {
            get { return this.torrentManager; }
        }
    }
}