//
// ClientPeer.cs
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



using System;
using MonoTorrent.Common;
using System.Net;

namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class ClientPeer : Peer, IPeer
    {
        #region Member Variables
        /// <summary>
        /// The connection associated with this peer
        /// </summary>
        public PeerConnectionBase Connection
        {
            get { return this.connection; }
            set { this.connection = value; }
        }
        private PeerConnectionBase connection;


        /// <summary>
        /// The current status of the peer
        /// </summary>
        public PeerStatus Status
        {
            get { return this.status; }
            set { this.status = value; }
        }
        private PeerStatus status;


        /// <summary>
        /// The type of peer
        /// </summary>
        public PeerType PeerType
        {
            get { return this.peerType; }
            internal set { this.peerType = value; }
        }
        private PeerType peerType;


        /// <summary>
        /// 
        /// </summary>
        public bool AmInterested
        {
            get { return this.amInterested; }
            set { this.amInterested = value; }
        }
        private bool amInterested;


        /// <summary>
        /// 
        /// </summary>
        public bool AmChoking
        {
            get { return this.amChoking; }
            set { this.amChoking = value; }
        }
        private bool amChoking;


        /// <summary>
        /// 
        /// </summary>
        public bool IsChoking
        {
            get { return this.isChoking; }
            set { this.isChoking = value; }
        }
        private bool isChoking;


        /// <summary>
        /// 
        /// </summary>
        public bool IsInterested
        {
            get { return this.isInterested; }
            set { this.isInterested = value; }
        }
        private bool isInterested;


        /// <summary>
        /// 
        /// </summary>
        public int IsRequestingPiecesCount
        {
            get { return this.isRequestingPiecesCount; }
            set { this.isRequestingPiecesCount = value; }
        }
        private int isRequestingPiecesCount;


        /// <summary>
        /// 
        /// </summary>
        public int AmRequestingPiecesCount
        {
            get { return this.amRequestingPiecesCount; }
            set { this.amRequestingPiecesCount = value; }
        }
        private int amRequestingPiecesCount;


        /// <summary>
        /// True if we are currently processing the peers message queue
        /// </summary>
        public bool ProcessingQueue
        {
            get { return this.processingQueue; }
            set { this.processingQueue = value; }
        }
        private bool processingQueue;


        /// <summary>
        /// The number of pieces that we've sent the peer.
        /// </summary>
        public int PiecesSent
        {
            get { return this.piecesSent; }
            internal set { this.piecesSent = value; }
        }
        private int piecesSent;


        /// <summary>
        /// The peers bitfield
        /// </summary>
        public BitField BitField
        {
            get { return this.bitField; }
            set { this.bitField = value; }
        }
        private BitField bitField;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new ClientPeer
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the peer</param>
        /// <param name="port">The port the peer is listening on</param>
        /// <param name="peerId">The PeerID</param>
        public ClientPeer(string ipAddress, int port, string peerId)
            : this(new IPEndPoint(IPAddress.Parse(ipAddress), port), peerId)
        {

        }


        /// <summary>
        /// Creates a new ClientPeer
        /// </summary>
        /// <param name="remoteHost">The IPEndpoint of the peer</param>
        /// <param name="peerId">The PeerID</param>
        public ClientPeer(IPEndPoint remoteHost, string peerId)
            : base(remoteHost, peerId)
        {
            this.status = PeerStatus.Available;
            this.AmInterested = false;
            this.AmChoking = true;
            this.IsInterested = false;
            this.IsChoking = true;
            this.peerType = PeerType.Unknown;
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
            ClientPeer peer2 = obj as ClientPeer;

            if (peer2 == null)
                return false;

            return this.PeerEndpoint.Equals(peer2.PeerEndpoint);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return this.PeerEndpoint.GetHashCode();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this.PeerEndpoint.ToString();
        }
        #endregion
    }
}