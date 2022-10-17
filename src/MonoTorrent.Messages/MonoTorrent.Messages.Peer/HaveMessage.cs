//
// HaveMessage.cs
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
    /// Represents a "Have" message
    /// </summary>
    public class HaveMessage : PeerMessage, IRentable
    {
        internal const byte MessageId = 4;
        const int messageLength = 5;

        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength => messageLength + 4;

        /// <summary>
        /// The index of the piece that you "have"
        /// </summary>
        public int PieceIndex { get; set; }

        /// <summary>
        /// Creates a new HaveMessage
        /// </summary>
        public HaveMessage ()
        {
        }

        /// <summary>
        /// Creates a new HaveMessage
        /// </summary>
        /// <param name="pieceIndex">The index of the piece that you "have"</param>
        public HaveMessage (int pieceIndex)
            => PieceIndex = pieceIndex;

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, messageLength);
            Write (ref buffer, MessageId);
            Write (ref buffer, PieceIndex);

            return written - buffer.Length;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            PieceIndex = ReadInt (ref buffer);
        }

        public override bool Equals (object? obj)
            => obj is HaveMessage msg
            && PieceIndex == msg.PieceIndex;

        public override int GetHashCode ()
            => PieceIndex.GetHashCode ();

        public void Initialize (int index)
            => PieceIndex = index;

        public override string ToString ()
        {
            var sb = new System.Text.StringBuilder ();
            sb.Append ("HaveMessage ");
            sb.Append (" Index ");
            sb.Append (PieceIndex);
            return sb.ToString ();
        }
    }
}
