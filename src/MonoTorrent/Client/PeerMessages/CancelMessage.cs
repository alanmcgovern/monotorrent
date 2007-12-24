//
// CancelMessage.cs
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
    /// 
    /// </summary>
    public class CancelMessage : IPeerMessageInternal, IPeerMessage
    {
        private const int messageLength = 13;
        public const int MessageId = 8;


        #region Member Variables
        /// <summary>
        /// The index of the piece
        /// </summary>
        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }
        private int pieceIndex;


        /// <summary>
        /// The offset in bytes of the block of data
        /// </summary>
        public int StartOffset
        {
            get { return this.startOffset; }
        }
        private int startOffset;


        /// <summary>
        /// The length in bytes of the block of data
        /// </summary>
        public int RequestLength
        {
            get { return this.requestLength; }
        }
        private int requestLength;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new CancelMessage
        /// </summary>
        public CancelMessage()
        {
        }


        /// <summary>
        /// Creates a new CancelMessage
        /// </summary>
        /// <param name="pieceIndex">The index of the piece to cancel</param>
        /// <param name="startOffset">The offset in bytes of the block of data to cancel</param>
        /// <param name="requestLength">The length in bytes of the block of data to cancel</param>
        public CancelMessage(int pieceIndex, int startOffset, int requestLength)
        {
            this.pieceIndex = pieceIndex;
            this.startOffset = startOffset;
            this.requestLength = requestLength;
        }
        #endregion


        #region Methods
        /// <summary>
        /// Encodes the CancelMessage into the supplied buffer
        /// </summary>
        /// <param name="id">The peer who we are about to send the message to</param>
        /// <param name="buffer">The buffer to encode the message to</param>
        /// <param name="offset">The offset at which to start encoding the data to</param>
        /// <returns>The number of bytes encoded into the buffer</returns>
        internal int Encode(ArraySegment<byte> buffer, int offset)
        {
            buffer.Array[buffer.Offset + offset + 4] = (byte)MessageId;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(messageLength)), 0, buffer.Array, buffer.Offset + offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.pieceIndex)), 0, buffer.Array, buffer.Offset + offset + 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.startOffset)), 0, buffer.Array, buffer.Offset + offset + 9, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.requestLength)), 0, buffer.Array, buffer.Offset + offset + 13, 4);

            return (messageLength + 4);
        }


        /// <summary>
        /// Decodes a RequestMessage from the supplied buffer
        /// </summary>
        /// <param name="id">The peer to decode the message from</param>
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
            offset += 4;
        }


        /// <summary>
        /// Performs any necessary actions required to process the message
        /// </summary>
        /// <param name="id">The Peer who's message will be handled</param>
        internal void Handle(PeerIdInternal id)
        {
            IPeerMessageInternal msg;
            for (int i = 0; i < id.Connection.QueueLength; i++)
            {
                msg = id.Connection.Dequeue();
                if (!(msg is PieceMessage))
                {
                    id.Connection.Enqueue(msg);
                    continue;
                }

                PieceMessage piece = msg as PieceMessage;
                if (!(piece.PieceIndex == this.pieceIndex && piece.StartOffset == this.startOffset && piece.RequestLength == this.requestLength))
                {
                    id.Connection.Enqueue(msg);
                }
                else
                {
                    id.Connection.IsRequestingPiecesCount--;
                }
            }
        }


        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public int ByteLength
        {
            get { return (messageLength + 4); }
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
            sb.Append("CancelMessage ");
            sb.Append(" Index ");
            sb.Append(this.pieceIndex);
            sb.Append(" Offset ");
            sb.Append(this.startOffset);
            sb.Append(" Length ");
            sb.Append(this.requestLength);
            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            CancelMessage msg = obj as CancelMessage;

            if (msg == null)
                return false;

            return (this.pieceIndex == msg.pieceIndex
                    && this.startOffset == msg.startOffset
                    && this.requestLength == msg.requestLength);
        }

        public override int GetHashCode()
        {
            return (this.pieceIndex.GetHashCode()
                ^ this.requestLength.GetHashCode()
                ^ this.startOffset.GetHashCode());
        }
        #endregion


        #region IPeerMessageInternal Explicit Calls

        int IPeerMessageInternal.Encode(ArraySegment<byte> buffer, int offset)
        {
            return this.Encode(buffer, offset);
        }

        void IPeerMessageInternal.Decode(ArraySegment<byte> buffer, int offset, int length)
        {
            this.Decode(buffer, offset, length);
        }

        void IPeerMessageInternal.Handle(PeerIdInternal id)
        {
            this.Handle(id);
        }

        int IPeerMessageInternal.ByteLength
        {
            get { return this.ByteLength; }
        }

        #endregion
    }
}