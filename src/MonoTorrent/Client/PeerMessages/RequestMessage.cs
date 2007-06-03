//
// RequestMessage.cs
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
using System.Net;

namespace MonoTorrent.Client.PeerMessages
{
    /// <summary>
    /// Represents a "Request" message
    /// </summary>
    public class RequestMessage : IPeerMessageInternal, IPeerMessage
    {
        public const int MessageId = 6;
        private const int messageLength = 13;

        private const int MaxSize = 65536;
        private const int MinSize = 4096;

        #region Private Fields
        private int startOffset;
        private int pieceIndex;
        private int requestLength;
        #endregion


        #region Public Properties

        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public int ByteLength
        {
            get { return (messageLength + 4); }
        }


        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        int IPeerMessageInternal.ByteLength
        {
            get { return this.ByteLength; }
        }


        /// <summary>
        /// The offset in bytes of the block of data
        /// </summary>
        public int StartOffset
        {
            get { return this.startOffset; }
        }


        /// <summary>
        /// The index of the piece
        /// </summary>
        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }


        /// <summary>
        /// The length of the block of data
        /// </summary>
        public int RequestLength
        {
            get { return this.requestLength; }
        }

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new RequestMessage
        /// </summary>
        public RequestMessage()
        {
        }


        /// <summary>
        /// Creates a new RequestMessage
        /// </summary>
        /// <param name="pieceIndex">The index of the piece to request</param>
        /// <param name="startOffset">The offset in bytes of the block of data to request</param>
        /// <param name="requestLength">The length of the block of data to request</param>
        public RequestMessage(int pieceIndex, int startOffset, int requestLength)
        {
            this.pieceIndex = pieceIndex;
            this.startOffset = startOffset;
            this.requestLength = requestLength;
        }

        #endregion


        #region Methods
        /// <summary>
        /// Decodes a RequestMessage from the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to decode the message from</param>
        /// <param name="offset">The offset thats the message starts at</param>
        /// <param name="length">The maximum number of bytes to read from the buffer</param>
        internal void Decode(ArraySegment<byte> buffer, int offset, int length)
        {
            this.pieceIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer.Array, buffer.Offset + offset));
            offset += 4;
            this.startOffset = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer.Array, buffer.Offset + offset));
            offset += 4;
            this.requestLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer.Array, buffer.Offset + offset));
        }


        /// <summary>
        /// Decodes a RequestMessage from the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to decode the message from</param>
        /// <param name="offset">The offset thats the message starts at</param>
        /// <param name="length">The maximum number of bytes to read from the buffer</param>
        void IPeerMessageInternal.Decode(ArraySegment<byte> buffer, int offset, int length)
        {
            this.Decode(buffer, offset, length);
        }


        /// <summary>
        /// Encodes the RequestMessage into the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to encode the message to</param>
        /// <param name="offset">The offset at which to start encoding the data to</param>
        /// <returns>The number of bytes encoded into the buffer</returns>
        internal int Encode(ArraySegment<byte> buffer, int offset)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(messageLength)), 0, buffer.Array, buffer.Offset + offset, 4);
            buffer.Array[buffer.Offset + offset + 4] = (byte)MessageId;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.pieceIndex)), 0, buffer.Array, buffer.Offset + offset + 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.startOffset)), 0, buffer.Array, buffer.Offset + offset + 9, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.requestLength)), 0, buffer.Array, buffer.Offset + offset + 13, 4);

            return (messageLength + 4);
        }


        /// <summary>
        /// Encodes the RequestMessage into the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to encode the message to</param>
        /// <param name="offset">The offset at which to start encoding the data to</param>
        /// <returns>The number of bytes encoded into the buffer</returns>
        int IPeerMessageInternal.Encode(ArraySegment<byte> buffer, int offset)
        {
            return this.Encode(buffer, offset);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            RequestMessage msg = obj as RequestMessage;
            return (msg == null) ? false : (this.pieceIndex == msg.pieceIndex
                                            && this.startOffset == msg.startOffset
                                            && this.requestLength == msg.requestLength);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return (this.pieceIndex.GetHashCode() ^ this.requestLength.GetHashCode() ^ this.startOffset.GetHashCode());
        }


        /// <summary>
        /// Performs any necessary actions required to process the message
        /// </summary>
        /// <param name="id">The Peer who's message will be handled</param>
        internal void Handle(PeerId id)
        {
            // If we are not on the last piece and the user requested a stupidly big/small amount of data
            // we will close the connection
            if (id.TorrentManager.Torrent.Pieces.Count != (this.pieceIndex + 1))
                if (this.requestLength > (MaxSize) || this.requestLength < MinSize)
                    throw new MessageException("Request length invalid");

            PieceMessage m = new PieceMessage(id.TorrentManager.FileManager, this.PieceIndex, this.startOffset, this.requestLength);

            // If we're not choking the peer, enqueue the message right away
            if (!id.Peer.Connection.AmChoking)
            {
                id.Peer.Connection.IsRequestingPiecesCount++;
                id.Peer.Connection.Enqueue(m);
            }

            // If the peer supports fast peer and the requested piece is one of the allowed pieces, enqueue it
            // otherwise send back a reject request message
            else if (id.Peer.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer)
            {
                if (id.Peer.Connection.AmAllowedFastPieces.Contains((uint)this.pieceIndex))
                {
                    id.Peer.Connection.IsRequestingPiecesCount++;
                    id.Peer.Connection.Enqueue(m);
                }
                else
                    id.Peer.Connection.Enqueue(new RejectRequestMessage(m));
            }
        }


        /// <summary>
        /// Performs any necessary actions required to process the message
        /// </summary>
        /// <param name="id">The Peer who's message will be handled</param>
        void IPeerMessageInternal.Handle(PeerId id)
        {
            this.Handle(id);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("RequestMessage ");
            sb.Append(" Index ");
            sb.Append(this.pieceIndex);
            sb.Append(" Offset ");
            sb.Append(this.startOffset);
            sb.Append(" Length ");
            sb.Append(this.requestLength);
            return sb.ToString();
        }
        #endregion
    }
}