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

namespace MonoTorrent.Messages.Peer
{
    public class PieceMessage : PeerMessage, IRentable
    {
        internal static MemoryPool BufferPool = MemoryPool.Default;

        public const byte MessageId = 7;
        const int messageLength = 9;

        /// <summary>
        /// The data associated with this block
        /// </summary>
        public Memory<byte> Data { get; private set; }

        ByteBufferPool.Releaser DataReleaser { get; set; }

        /// <summary>
        /// The index of the block from the piece which was requested.
        /// </summary>
        internal int BlockIndex => StartOffset / Constants.BlockSize;

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

        public PieceMessage ()
        {
        }

        public PieceMessage (int pieceIndex, int startOffset, int blockLength)
            => Initialize (pieceIndex, startOffset, blockLength);

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            PieceIndex = ReadInt (ref buffer);
            StartOffset = ReadInt (ref buffer);
            RequestLength = buffer.Length;

            if (RequestLength > 16384)
                Console.WriteLine ("Huh...");
            if (!Data.IsEmpty)
                Console.Write ("ooooops?");
            // This buffer will be freed after the PieceWriter has finished with it
            DataReleaser = BufferPool.Rent (RequestLength, out Memory<byte> memory);
            buffer.CopyTo (memory.Span);
            Data = memory;
        }

        public override int Encode (Span<byte> buffer)
        {
            int origLength = buffer.Length;

            Write (ref buffer, messageLength + RequestLength);
            Write (ref buffer, MessageId);
            Write (ref buffer, PieceIndex);
            Write (ref buffer, StartOffset);
            Write (ref buffer, Data.Span);

            return origLength - buffer.Length;
        }

        public override bool Equals (object? obj)
            => obj is PieceMessage message
                && message.PieceIndex == PieceIndex
                && message.StartOffset == StartOffset
                && message.RequestLength == RequestLength;

        public override int GetHashCode ()
            => RequestLength.GetHashCode () ^ PieceIndex.GetHashCode () ^ StartOffset.GetHashCode ();

        public PieceMessage Initialize (int pieceIndex, int startOffset, int blockLength)
        {
            (PieceIndex, StartOffset, RequestLength) = (pieceIndex, startOffset, blockLength);
            return this;
        }

        protected override void Reset ()
        {
            PieceIndex = Int32.MinValue;
            StartOffset = Int32.MinValue;
            RequestLength = Int32.MinValue;
            DataReleaser.Dispose ();
            (DataReleaser, Data) = (default, default);
        }

        public void SetData ((ByteBufferPool.Releaser releaser, Memory<byte> data) value)
        {
            if (!Data.IsEmpty)
                throw new Exception ("Der");
            (DataReleaser, Data) = value;
        } 

        public override string ToString ()
        {
            var sb = new System.Text.StringBuilder ();
            sb.Append ("PieceMessage ");
            sb.Append (" Index ");
            sb.Append (PieceIndex);
            sb.Append (" Offset ");
            sb.Append (StartOffset);
            sb.Append (" Length ");
            sb.Append (RequestLength);
            return sb.ToString ();
        }
    }
}
