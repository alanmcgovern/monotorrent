//
// AllowedFastMessage.cs
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
using System.Text;

namespace MonoTorrent.Messages.Peer.FastPeer
{
    public class AllowedFastMessage : PeerMessage, IRentable, IFastPeerMessage
    {
        internal const byte MessageId = 0x11;
        readonly int messageLength = 5;

        public override int ByteLength => messageLength + 4;
        public int PieceIndex { get; private set; }

        public AllowedFastMessage ()
        {
        }

        public AllowedFastMessage (int pieceIndex)
        {
            PieceIndex = pieceIndex;
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, messageLength);
            Write (ref buffer, MessageId);
            Write (ref buffer, PieceIndex);

            return written - buffer.Length;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
            => PieceIndex = ReadInt (ref buffer);

        public override bool Equals (object? obj)
            => obj is AllowedFastMessage other
            && other.PieceIndex == PieceIndex;

        public override int GetHashCode ()
            => PieceIndex;

        public void Initialize (int pieceIndex)
            => PieceIndex = pieceIndex;

        public override string ToString ()
        {
            var sb = new StringBuilder (24);
            sb.Append ("AllowedFast");
            sb.Append (" Index: ");
            sb.Append (PieceIndex);
            return sb.ToString ();
        }
    }
}
