//
// RequestMessage.cs
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
    public class RequestMessage : PeerMessage, IRentable
    {
        internal const byte MessageId = 6;
        const int messageLength = 13;

        // According to BEP52 this any peer which requests blocks larger than 16KiB should have it's connection closed.
        //
        // 'request' messages contain an index, begin, and length .... All current implementations use 2^14 (16 kiB), and close connections which request an amount greater than that.
        //
        public static readonly int MaxSize = 16 * 1024;
        public static readonly int MinSize = 1;

        public override int ByteLength => messageLength + 4;
        public int PieceIndex { get; protected set; }
        public int RequestLength { get; protected set; }
        public int StartOffset { get; protected set; }

        public RequestMessage ()
        {
        }

        public RequestMessage (int pieceIndex, int startOffset, int requestLength)
        {
            Initialize (new BlockInfo (pieceIndex, startOffset, requestLength));
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            PieceIndex = ReadInt (ref buffer);
            StartOffset = ReadInt (ref buffer);
            RequestLength = ReadInt (ref buffer);
        }

        public void Decode (ref ReadOnlySpan<byte> buffer)
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

        public int Encode (ref Span<byte> buffer)
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
            => obj is RequestMessage msg
            && PieceIndex == msg.PieceIndex
            && StartOffset == msg.StartOffset
            && RequestLength == msg.RequestLength;

        public override int GetHashCode ()
            => PieceIndex.GetHashCode () ^ RequestLength.GetHashCode () ^ StartOffset.GetHashCode ();

        public void Initialize (BlockInfo request)
        {
            PieceIndex = request.PieceIndex;
            StartOffset = request.StartOffset;
            RequestLength = request.RequestLength;
        }

        public override string ToString ()
        {

            var sb = new System.Text.StringBuilder ();
            sb.Append ("RequestMessage ");
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
