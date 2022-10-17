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

namespace MonoTorrent.Messages.Peer
{
    /// <summary>
    /// 
    /// </summary>
    public class CancelMessage : PeerMessage, IRentable
    {
        const int messageLength = 13;
        internal const byte MessageId = 8;

        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength => (messageLength + 4);

        /// <summary>
        /// The index of the piece
        /// </summary>
        public int PieceIndex { get; private set; }

        /// <summary>
        /// The offset in bytes of the block of data
        /// </summary>
        public int StartOffset { get; private set; }

        /// <summary>
        /// The length in bytes of the block of data
        /// </summary>
        public int RequestLength { get; private set; }

        /// <summary>
        /// Creates a new CancelMessage
        /// </summary>
        public CancelMessage ()
        {
        }

        /// <summary>
        /// Creates a new CancelMessage
        /// </summary>
        /// <param name="pieceIndex">The index of the piece to cancel</param>
        /// <param name="startOffset">The offset in bytes of the block of data to cancel</param>
        /// <param name="requestLength">The length in bytes of the block of data to cancel</param>
        public CancelMessage (int pieceIndex, int startOffset, int requestLength)
        {
            PieceIndex = pieceIndex;
            StartOffset = startOffset;
            RequestLength = requestLength;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            PieceIndex = ReadInt (ref buffer);
            StartOffset = ReadInt (ref buffer);
            RequestLength = ReadInt (ref buffer);
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, messageLength);
            Write (ref buffer, MessageId);
            Write (ref buffer, PieceIndex);
            Write (ref buffer, StartOffset);
            Write (ref buffer, RequestLength);

            return written - buffer.Length;
        }

        public override bool Equals (object? obj)
            => obj is CancelMessage msg
            && PieceIndex == msg.PieceIndex
            && StartOffset == msg.StartOffset
            && RequestLength == msg.RequestLength;

        public override int GetHashCode ()
            => PieceIndex.GetHashCode ()
             ^ RequestLength.GetHashCode ()
             ^ StartOffset.GetHashCode ();

        public void Initialize (int pieceIndex, int startOffset, int requestLength)
            => (PieceIndex, StartOffset, RequestLength) = (pieceIndex, startOffset, requestLength);

        public override string ToString ()
        {
            var sb = new System.Text.StringBuilder ();
            sb.Append ("CancelMessage ");
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
