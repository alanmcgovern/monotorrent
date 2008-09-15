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
using System.IO;

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

        internal int BlockIndex
        {
            get { return this.startOffset / Piece.BlockSize; }
        }

        public override int ByteLength
        {
            get { return (messageLength + this.requestLength + 4); }
        }
        
        internal int DataOffset
        {
            get { return this.dataOffset; }
        }

        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }

        public int PieceLength
        {
            get { return this.manager.FileManager.PieceLength; }
        }

        public int StartOffset
        {
            get { return this.startOffset; }
        }

        public int RequestLength
        {
            get { return this.requestLength; }
        }

        #endregion


        #region Constructors

        public PieceMessage(TorrentManager manager)
        {
            this.manager = manager;
        }

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
            this.requestLength = length - 8;

            this.dataOffset = offset;

            // This buffer will be freed after the PieceWriter has finished with it
            this.data = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref this.data, requestLength);
            Buffer.BlockCopy(buffer, offset, this.data.Array, this.data.Offset, requestLength);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int bytesRead = 0;
            int written = offset;

            written += Write(buffer, written, messageLength + requestLength);
            written += Write(buffer, written, MessageId);
            written += Write(buffer, written, pieceIndex);
            written += Write(buffer, written, startOffset);

            long pieceOffset = (long)this.PieceIndex * this.manager.FileManager.PieceLength + this.startOffset;
            bytesRead = this.manager.FileManager.Read(buffer, written, pieceOffset, this.RequestLength);

            if (bytesRead != this.RequestLength)
                throw new MessageException("Could not read required data");
            written += bytesRead;

            CheckWritten(written - offset);
            return written - offset;
        }

        public override bool Equals(object obj)
        {
            PieceMessage msg = obj as PieceMessage;
            return (msg == null) ? false : (this.pieceIndex == msg.pieceIndex
                                            && this.startOffset == msg.startOffset
                                            && this.requestLength == msg.requestLength);
        }

        public override int GetHashCode()
        {
            return (this.requestLength.GetHashCode()
                ^ this.dataOffset.GetHashCode()
                ^ this.pieceIndex.GetHashCode()
                ^ this.startOffset.GetHashCode()
                ^ this.manager.GetHashCode());
        }

        internal override void Handle(PeerId id)
        {
            id.PiecesReceived++;

            string path = id.TorrentManager.FileManager.SavePath;
            BufferedIO d = new BufferedIO(data, pieceIndex, BlockIndex, requestLength, id.TorrentManager.Torrent.PieceLength, id.TorrentManager.Torrent.Files, path);
            d.Id = id;
            id.TorrentManager.PieceManager.ReceivedPieceMessage(d);

            // Keep adding new piece requests to this peers queue until we reach the max pieces we're allowed queue
            while (id.TorrentManager.PieceManager.AddPieceRequest(id)) { }

            if (!id.ProcessingQueue)
            {
                id.ProcessingQueue = true;
                MessageHandler.EnqueueSend(id);
            }
        }

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
