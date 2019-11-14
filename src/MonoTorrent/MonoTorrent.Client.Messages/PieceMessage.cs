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

namespace MonoTorrent.Client.Messages.Standard
{
    class PieceMessage : PeerMessage
    {
        internal static readonly byte MessageId = 7;
        private const int messageLength = 9;

        /// <summary>
        /// The data associated with this block
        /// </summary>
        internal byte[] Data { get; set; }

        /// <summary>
        /// The index of the block from the piece which was requested.
        /// </summary>
        internal int BlockIndex => StartOffset / Piece.BlockSize;

        /// <summary>
        /// The length of the message in bytes
        /// </summary>
        public override int ByteLength => messageLength + RequestLength + 4;

        /// <summary>
        /// The index of the piece which was requested
        /// </summary>
        public int PieceIndex { get; private set; }

        /// <summary>
        /// The byte offset of the block which was requested
        /// </summary>
        public int StartOffset { get; private set; }

        /// <summary>
        /// The length of the block which was requested
        /// </summary>
        public int RequestLength { get; private set; }

        public PieceMessage()
        {
        }

        public PieceMessage(int pieceIndex, int startOffset, int blockLength)
        {
            PieceIndex = pieceIndex;
            StartOffset = startOffset;
            RequestLength = blockLength;
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            PieceIndex = ReadInt(buffer, ref offset);
            StartOffset = ReadInt(buffer, ref offset);
            RequestLength = length - 8;

            // This buffer will be freed after the PieceWriter has finished with it
            Data = ClientEngine.BufferPool.Rent(RequestLength);
            Buffer.BlockCopy(buffer, offset, Data, 0, RequestLength);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;

            written += Write(buffer, written, messageLength + RequestLength);
            written += Write(buffer, written, MessageId);
            written += Write(buffer, written, PieceIndex);
            written += Write(buffer, written, StartOffset);
            written += Write(buffer, written, Data, 0, RequestLength);

            return CheckWritten(written - offset);
        }

        public override bool Equals(object obj)
        {
            return obj is PieceMessage message
                && message.PieceIndex == PieceIndex
                && message.StartOffset == StartOffset
                && message.RequestLength == RequestLength;
        }

        public override int GetHashCode()
            => RequestLength.GetHashCode() ^ PieceIndex.GetHashCode() ^ StartOffset.GetHashCode();

        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("PieceMessage ");
            sb.Append(" Index ");
            sb.Append(PieceIndex);
            sb.Append(" Offset ");
            sb.Append(StartOffset);
            sb.Append(" Length ");
            sb.Append(RequestLength);
            return sb.ToString();
        }
    }
}
