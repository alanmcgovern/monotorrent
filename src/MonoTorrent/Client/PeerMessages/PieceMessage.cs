//
// PieceMessage.cs
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
using MonoTorrent.Common;

namespace MonoTorrent.Client.PeerMessages
{
    /// <summary>
    /// 
    /// </summary>
    public class PieceMessage : IPeerMessageInternal, IPeerMessage
    {
        public const int MessageId = 7;
        private const int messageLength = 9;

        #region Private Fields
        private int dataOffset;
        private TorrentManager manager;
        private int pieceIndex;
        private int startOffset;
        private int requestLength;

        private ArraySegment<byte> data;
        #endregion

        #region Properties

        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public int ByteLength
        {
            get { return (messageLength + this.requestLength + 4); }
        }


        /// <summary>
        /// The offset in the buffer at which the piece starts
        /// </summary>
        internal int DataOffset
        {
            get { return this.dataOffset; }
        }


        /// <summary>
        /// The index of the piece
        /// </summary>
        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }


        /// <summary>
        /// The length of the piece in bytes
        /// </summary>
        public int PieceLength
        {
            get { return this.manager.FileManager.PieceLength; }
        }


        /// <summary>
        /// The start offset of the data
        /// </summary>
        public int StartOffset
        {
            get { return this.startOffset; }
        }


        /// <summary>
        /// The length of the data
        /// </summary>
        public int RequestLength
        {
            get { return this.requestLength; }
        }

        int IPeerMessageInternal.ByteLength
        {
            get { return this.ByteLength; }
        }


        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new piece message
        /// </summary>
        internal PieceMessage(TorrentManager manager)
        {
            this.manager = manager;
        }


        /// <summary>
        /// Creates a new piece message
        /// </summary>
        /// <param name="pieceIndex">The index of the piece</param>
        /// <param name="startOffset">The start offset in bytes of the block of data</param>
        /// <param name="blockLength">The length in bytes of the data</param>
        internal PieceMessage(TorrentManager manager, int pieceIndex, int startOffset, int blockLength)
        {
            this.manager = manager;
            this.pieceIndex = pieceIndex;
            this.startOffset = startOffset;
            this.requestLength = blockLength;
        }

        #endregion

        #region Methods
        /// <summary>
        /// Decodes a PieceMessage from the supplied buffer
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
            this.requestLength = length - offset;

            this.dataOffset = offset;

            this.data = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref this.data, buffer.Count);
            Buffer.BlockCopy(buffer.Array, buffer.Offset, this.data.Array, this.data.Offset, buffer.Count);
        }


        /// <summary>
        /// Decodes a PieceMessage from the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to decode the message from</param>
        /// <param name="offset">The offset thats the message starts at</param>
        /// <param name="length">The maximum number of bytes to read from the buffer</param>
        void IPeerMessageInternal.Decode(ArraySegment<byte> buffer, int offset, int length)
        {
            this.Decode(buffer, offset, length);
        }


        /// <summary>
        /// Encodes the PieceMessage into the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to encode the message to</param>
        /// <param name="offset">The offset at which to start encoding the data to</param>
        /// <returns>The number of bytes encoded into the buffer</returns>
        internal int Encode(ArraySegment<byte> buffer, int offset)
        {
            int bytesRead = 0;
            buffer.Array[buffer.Offset + offset + 4] = (byte)MessageId;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(messageLength + requestLength)), 0, buffer.Array, buffer.Offset + offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.pieceIndex)), 0, buffer.Array, buffer.Offset + offset + 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.startOffset)), 0, buffer.Array, buffer.Offset + offset + 9, 4);

            long pieceOffset = (long)this.PieceIndex * this.manager.FileManager.PieceLength + this.startOffset;
            bytesRead = this.manager.FileManager.Read(buffer.Array, buffer.Offset + offset + 13, pieceOffset, this.RequestLength);

            if (bytesRead != this.RequestLength)
                throw new MessageException("Could not read required data");

            return (messageLength + bytesRead + 4);
        }


        /// <summary>
        /// Encodes the PieceMessage into the supplied buffer
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
            PieceMessage msg = obj as PieceMessage;
            return (msg == null) ? false : (this.pieceIndex == msg.pieceIndex
                                            && this.startOffset == msg.startOffset
                                            && this.requestLength == msg.requestLength
                                            && this.dataOffset == msg.dataOffset
                                            && this.manager == msg.manager);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return (this.requestLength.GetHashCode()
                ^ this.dataOffset.GetHashCode()
                ^ this.pieceIndex.GetHashCode()
                ^ this.startOffset.GetHashCode()
                ^ this.manager.GetHashCode());
        }


        /// <summary>
        /// Performs any necessary actions required to process the message
        /// </summary>
        /// <param name="id">The Peer who's message will be handled</param>
        internal void Handle(PeerIdInternal id)
        {
            try
            {
                id.TorrentManager.PieceManager.ReceivedPieceMessage(id, this.data, this);

                // Keep adding new piece requests to this peers queue until we reach the max pieces we're allowed queue
                while (id.TorrentManager.PieceManager.AddPieceRequest(id)) { }

                if (!id.Connection.ProcessingQueue)
                {
                    id.Connection.ProcessingQueue = true;
                    id.ConnectionManager.MessageHandler.EnqueueSend(id);
                }
            }
            finally
            {
                ClientEngine.BufferManager.FreeBuffer(ref this.data);
            }
        }


        /// <summary>
        /// Performs any necessary actions required to process the message
        /// </summary>
        /// <param name="id">The Peer who's message will be handled</param>
        void IPeerMessageInternal.Handle(PeerIdInternal id)
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
            sb.Append("PieceMessage ");
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
