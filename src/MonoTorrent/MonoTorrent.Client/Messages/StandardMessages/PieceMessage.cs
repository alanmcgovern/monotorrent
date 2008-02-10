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

namespace MonoTorrent.Client.Messages.Standard
{
    public class PieceMessage : PeerMessage
    {
        public const byte MessageId = 7;
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
        public override int ByteLength
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


        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new piece message
        /// </summary>
        public PieceMessage(TorrentManager manager)
        {
            this.manager = manager;
        }


        /// <summary>
        /// Creates a new piece message
        /// </summary>
        /// <param name="pieceIndex">The index of the piece</param>
        /// <param name="startOffset">The start offset in bytes of the block of data</param>
        /// <param name="blockLength">The length in bytes of the data</param>
        public PieceMessage(TorrentManager manager, int pieceIndex, int startOffset, int blockLength)
        {
            this.manager = manager;
            this.pieceIndex = pieceIndex;
            this.startOffset = startOffset;
            this.requestLength = blockLength;
        }

        #endregion

        #region Methods
        public override void Decode(byte[] buffer, int offset, int length)
        {
            this.pieceIndex = ReadInt(buffer, offset);
            offset += 4;
            this.startOffset = ReadInt(buffer, offset);
            offset += 4;
            this.requestLength = length - 9;

            this.dataOffset = offset;

            // This buffer will be freed after the PieceWriter has finished with it
            this.data = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref this.data, requestLength);
            Buffer.BlockCopy(buffer, offset, this.data.Array, this.data.Offset, requestLength);
        }


        public override int Encode(byte[] buffer, int offset)
        {
            int bytesRead = 0;
            int written = Write(buffer, offset, messageLength + requestLength);
            written += Write(buffer, offset + 4, MessageId);
            written += Write(buffer, offset + 5, pieceIndex);
            written += Write(buffer, offset + 9, startOffset);

            long pieceOffset = (long)this.PieceIndex * this.manager.FileManager.PieceLength + this.startOffset;
            bytesRead = this.manager.FileManager.Read(buffer, offset + 13, pieceOffset, this.RequestLength);

            if (bytesRead != this.RequestLength)
                throw new MessageException("Could not read required data");
            written += bytesRead;

            CheckWritten(written);
            return written;
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
        internal override void Handle(PeerIdInternal id)
        {
            PieceData d = new PieceData(data, pieceIndex, startOffset, requestLength, id);
            id.TorrentManager.PieceManager.ReceivedPieceMessage(d);

            // Keep adding new piece requests to this peers queue until we reach the max pieces we're allowed queue
            while (id.TorrentManager.PieceManager.AddPieceRequest(id)) { }

            if (!id.Connection.ProcessingQueue)
            {
                id.Connection.ProcessingQueue = true;
                id.ConnectionManager.MessageHandler.EnqueueSend(id);
            }
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
