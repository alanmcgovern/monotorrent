//
// HandshakeMessage.cs
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

namespace MonoTorrent.Client.Messages.PeerMessages
{
    /// <summary>
    /// 
    /// </summary>
    public class HandshakeMessage : MonoTorrent.Client.Messages.PeerMessage
    {
        private const byte FastPeersFlag = 0x04;


        #region Member Variables
        /// <summary>
        /// The length of the protocol string
        /// </summary>
        public int ProtocolStringLength
        {
            get { return this.protocolStringLength; }
        }
        private int protocolStringLength;


        /// <summary>
        /// The protocol string to send
        /// </summary>
        public string ProtocolString
        {
            get { return this.protocolString; }
        }
        private string protocolString;


        /// <summary>
        /// The infohash of the torrent
        /// </summary>
        public byte[] InfoHash
        {
            get { return this.infoHash; }
        }
        internal byte[] infoHash;


        /// <summary>
        /// The ID of the peer
        /// </summary>
        public string PeerId
        {
            get { return this.peerId; }
        }
        private string peerId;


        /// <summary>
        /// True if the peer supports the Bittorrent FastPeerExtensions
        /// </summary>
        public bool SupportsFastPeer
        {
            get { return this.supportsFastPeer; }
        }
        private bool supportsFastPeer;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new HandshakeMessage
        /// </summary>
        public HandshakeMessage()
        {
            this.supportsFastPeer = ClientEngine.SupportsFastPeer;
        }


        /// <summary>
        /// Creates a new HandshakeMessage
        /// </summary>
        /// <param name="infoHash">The infohash of the torrent</param>
        /// <param name="peerId">The ID of the peer</param>
        /// <param name="protocolString">The protocol string to send</param>
        public HandshakeMessage(byte[] infoHash, string peerId, string protocolString)
            : this()
        {
            this.infoHash = infoHash;
            this.peerId = peerId;
            this.protocolString = protocolString;
            this.protocolStringLength = protocolString.Length;
            this.supportsFastPeer = ClientEngine.SupportsFastPeer;
        }
        #endregion


        #region Methods
        public override int Encode(byte[] buffer, int offset)
        {
            int i = offset;

            // Copy in the length of the protocol string
            buffer[i] = (byte)protocolString.Length;
            i++;

            // Copy in the protocol string
            System.Text.Encoding.ASCII.GetBytes(protocolString, 0, protocolString.Length, buffer, i);
            i += protocolString.Length;

            // The 8 reserved bits are here. Make sure they are zeroed.
            for (int j = 20; j < 28; j++)
                buffer[j] = 0;
            if (ClientEngine.SupportsFastPeer)
                buffer[i + 7] |= FastPeersFlag;
            i += 8;

            // Copy in the infohash
            Buffer.BlockCopy(infoHash, 0, buffer, i, infoHash.Length);
            i += infoHash.Length;

            // Copy in the peerId
            System.Text.Encoding.ASCII.GetBytes(peerId, 0, peerId.Length, buffer, i);
            i += System.Text.Encoding.ASCII.GetByteCount(peerId);

            return i - offset;
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            int i = offset;
            protocolStringLength = (int)buffer[i];                  // First byte is length

            // #warning Fix this hack - is there a better way of verifying the protocol string? Hack
            if (protocolStringLength != VersionInfo.ProtocolStringV100.Length)
                protocolStringLength = VersionInfo.ProtocolStringV100.Length;
            i++;

            protocolString = System.Text.Encoding.ASCII.GetString(buffer, i, protocolStringLength);
            i += protocolStringLength;                                // Next bytes are protocol string
            i += 8;                                                   // 8 reserved bytes
            this.infoHash = new byte[20];

            Buffer.BlockCopy(buffer, i, this.infoHash, 0, infoHash.Length);
            i += infoHash.Length;                                   // 20 byte infohash

            peerId = System.Text.Encoding.ASCII.GetString(buffer, i, 20);
            i += 20;                                                // 20 byte peerid

            CheckForSupports(buffer, offset + protocolStringLength + 1);
        }


        private void CheckForSupports(byte[] buffer, int reservedBytesStartIndex)
        {
            this.supportsFastPeer = (FastPeersFlag & buffer[reservedBytesStartIndex + 7]) != 0;
            //int bitNumber = 0;
            //for (int i = reservedBytesStartIndex; i < reservedBytesStartIndex + 8; i++)
            //{
            //    if (buffer[i] != 0)
            //    {
            //        for (int j = 7; j >= 0; j--)
            //        {
            //            int temp = 1 << j;
            //            if ((buffer[i] & temp) > 0)
            //            {
            //                int value = ((i - reservedBytesStartIndex) * 8 + 7 - j) + 1;
            //                System.Diagnostics.Debug.Write("\n");
            //                System.Diagnostics.Debug.Write(value.ToString());
            //                System.Diagnostics.Debug.Write("\t" + this.PeerId);
            //            }
            //        }
            //    }
            //}
        }


        internal override void Handle(PeerIdInternal id)
        {
            if (!this.protocolString.Equals(VersionInfo.ProtocolStringV100))
            {
                Logger.Log(id, "Invalid protocol string: " + this.protocolString);
                throw new ProtocolException("Invalid protocol string");
            }

            // If we got the peer as a "compact" peer, then the peerid will be empty. In this case
            // we just copy the one that is in the handshake. 
            if (string.IsNullOrEmpty(id.Peer.PeerId))
                id.Peer.PeerId = this.peerId;

            // If the infohash doesn't match, dump the connection
            if (!Toolbox.ByteMatch(this.infoHash, id.TorrentManager.Torrent.InfoHash))
            {
                Logger.Log(id, "Invalid infohash");
                throw new TorrentException("Invalid infohash. Not tracking this torrent");
            }

            // If the peer id's don't match, dump the connection. This is due to peers faking usually
            if (id.Peer.PeerId != this.peerId)
            {
                Logger.Log(id, "Invalid peerid");
                throw new TorrentException("Supplied PeerID didn't match the one the tracker gave us");
            }

            // Attempt to parse the application that the peer is using
            id.Connection.ClientApp = new Software(this.peerId);
            id.Connection.SupportsFastPeer = this.supportsFastPeer;

            // If they support fast peers, create their list of allowed pieces that they can request off me
            if (this.supportsFastPeer && id.TorrentManager != null)
                id.Connection.AmAllowedFastPieces = AllowedFastAlgorithm.Calculate(id.Connection.AddressBytes, id.TorrentManager.Torrent.InfoHash, (uint)id.TorrentManager.Torrent.Pieces.Count);

        }


        public override int ByteLength
        {
            get { return 68; }
        }
        #endregion


        #region Overridden Methods
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("HandshakeMessage ");
            sb.Append(" PeerID ");
            sb.Append(this.peerId);
            sb.Append(" FastPeer ");
            sb.Append(this.supportsFastPeer);
            return sb.ToString();
        }


        public override bool Equals(object obj)
        {
            HandshakeMessage msg = obj as HandshakeMessage;

            if (msg == null)
                return false;

            if (!Toolbox.ByteMatch(this.infoHash, msg.infoHash))
                return false;

            return (this.peerId == msg.peerId
                    && this.protocolString == msg.protocolString
                    && this.supportsFastPeer == msg.supportsFastPeer);
        }


        public override int GetHashCode()
        {
            return (this.infoHash.GetHashCode() ^ this.peerId.GetHashCode() ^ this.protocolString.GetHashCode());
        }
        #endregion
    }
}
