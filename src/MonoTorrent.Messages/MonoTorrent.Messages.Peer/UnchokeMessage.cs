//
// UnchokeMessage.sc
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
    public class UnchokeMessage : PeerMessage
    {
        public static UnchokeMessage Instance { get; } = new UnchokeMessage ();

        internal static readonly byte MessageId = 1;
        const int messageLength = 1;

        public override int ByteLength => (messageLength + 4);

        public UnchokeMessage ()
        {
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            // No decoding needed
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, messageLength);
            Write (ref buffer, MessageId);

            return written - buffer.Length;
        }

        public override bool Equals (object obj)
            => obj is UnchokeMessage;

        public override int GetHashCode ()
            => MessageId;

        public override string ToString ()
            => "UnchokeMessage";
    }
}
