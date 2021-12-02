//
// RequestBundle.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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
using System.Collections.Generic;

namespace MonoTorrent.Messages.Peer
{
    class RequestBundle : PeerMessage
    {
        MutableRequestMessage Message { get; }
        IList<BlockInfo> Requests { get; }

        internal RequestBundle (IList<BlockInfo> requests)
        {
            Message = new MutableRequestMessage ();
            Requests = requests;
        }

        public override int ByteLength => Message.ByteLength * Requests.Count;

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            throw new InvalidOperationException ();
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            for (int i = 0; i < Requests.Count; i++) {
                Message.PieceIndex = Requests[i].PieceIndex;
                Message.RequestLength = Requests[i].RequestLength;
                Message.StartOffset = Requests[i].StartOffset;
                buffer = buffer.Slice (Message.Encode (buffer));
            }

            return written - buffer.Length;
        }

        public IEnumerable<RequestMessage> ToRequestMessages ()
        {
            foreach (BlockInfo req in Requests)
                yield return new RequestMessage (req.PieceIndex, req.StartOffset, req.RequestLength);
        }
    }
}
