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

namespace MonoTorrent.Client.PeerMessages
{
    /// <summary>
    /// 
    /// </summary>
    internal class PieceMessage : IPeerMessage
    {
        public const int MessageId = 7;
        private const int messageLength = 9;

        #region Member Variables
        /// <summary>
        /// The filemanager for this Piece
        /// </summary>
        private FileManager fileManager;
        private int dataOffset;


        /// <summary>
        /// The index of the piece
        /// </summary>
        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }
        private int pieceIndex;


        /// <summary>
        /// The start offset of the data
        /// </summary>
        public int StartOffset
        {
            get { return this.startOffset; }
        }
        private int startOffset;


        /// <summary>
        /// The length of the data
        /// </summary>
        public int BlockLength
        {
            get { return this.blockLength; }
        }
        private int blockLength;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new piece message
        /// </summary>
        public PieceMessage(FileManager manager)
        {
            this.fileManager = manager;
        }


        /// <summary>
        /// Creates a new piece message
        /// </summary>
        /// <param name="pieceIndex">The index of the piece</param>
        /// <param name="startOffset">The start offset in bytes of the block of data</param>
        /// <param name="blockLength">The length in bytes of the data</param>
        public PieceMessage(FileManager manager, int pieceIndex, int startOffset, int blockLength)
        {
            this.fileManager = manager;
            this.pieceIndex = pieceIndex;
            this.startOffset = startOffset;
            this.blockLength = blockLength;
        }
        #endregion


        #region Helper Methods
        /// <summary>
        /// Encodes the PieceMessage into the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to encode the message to</param>
        /// <param name="offset">The offset at which to start encoding the data to</param>
        /// <returns>The number of bytes encoded into the buffer</returns>
        public int Encode(byte[] buffer, int offset)
        {
            buffer[offset + 4] = (byte)MessageId;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(messageLength + blockLength)), 0, buffer, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.pieceIndex)), 0, buffer, offset + 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.startOffset)), 0, buffer, offset + 9, 4);

            long pieceOffset = this.startOffset + this.PieceIndex * this.fileManager.PieceLength;
            int bytesRead = this.fileManager.Read(buffer, offset + 13, pieceOffset, this.BlockLength);

            return (messageLength + bytesRead + 4);
        }


        /// <summary>
        /// Decodes a PieceMessage from the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to decode the message from</param>
        /// <param name="offset">The offset thats the message starts at</param>
        /// <param name="length">The maximum number of bytes to read from the buffer</param>
        public void Decode(byte[] buffer, int offset, int length)
        {
            this.pieceIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
            offset += 4;
            this.startOffset = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
            offset += 4;
            this.blockLength = length - offset;

            this.dataOffset = offset;
        }


        /// <summary>
        /// Performs any necessary actions required to process the message
        /// </summary>
        /// <param name="id">The Peer who's message will be handled</param>
        public void Handle(PeerConnectionID id)
        {
            int writeIndex = this.StartOffset + this.PieceIndex * this.fileManager.PieceLength;
            id.TorrentManager.PieceManager.RecievedPieceMessage(id, id.Peer.Connection.recieveBuffer, this.dataOffset, writeIndex, this.blockLength, this);
            //id.TorrentManager.PieceManager.RecievedPiece(id, this);
        }


        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public int ByteLength
        {
            get { return (messageLength + this.blockLength + 4); }
        }
        #endregion


        #region Overridden methods
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "PieceMessage";
        }

        public override bool Equals(object obj)
        {
            PieceMessage msg = obj as PieceMessage;

            if (msg == null)
                return false;

            return (this.pieceIndex == msg.pieceIndex
                    && this.startOffset == msg.startOffset
                    && this.blockLength == msg.blockLength
                    && this.dataOffset == msg.dataOffset
                    && this.fileManager == msg.fileManager);
        }

        public override int GetHashCode()
        {
            return (this.blockLength.GetHashCode()
                ^ this.dataOffset.GetHashCode()
                ^ this.pieceIndex.GetHashCode()
                ^ this.startOffset.GetHashCode()
                ^ this.fileManager.GetHashCode());
        }
        #endregion
    }
}