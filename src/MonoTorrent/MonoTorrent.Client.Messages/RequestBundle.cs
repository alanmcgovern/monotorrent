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

using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PiecePicking;

namespace MonoTorrent.Client.Messages
{
    class RequestBundle : PeerMessage
    {
        MutableRequestMessage Message { get; }
        IList<PieceRequest> Requests { get; }

        internal RequestBundle(IList<PieceRequest> requests)
        {
            Message = new MutableRequestMessage();
            Requests = requests;
        }

        public override int ByteLength => Message.ByteLength * Requests.Count;

        public override void Decode(byte[] buffer, int offset, int length)
        {
            throw new InvalidOperationException();
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;

            for (int i = 0; i < Requests.Count; i++)
            {
                Message.PieceIndex = Requests[i].PieceIndex;
                Message.RequestLength = Requests[i].RequestLength;
                Message.StartOffset = Requests[i].StartOffset;
                written += Message.Encode(buffer, written);
            }

            return CheckWritten(written - offset);
        }

        public IEnumerable<RequestMessage> ToRequestMessages ()
        {
            foreach (var req in Requests)
                yield return new RequestMessage (req.PieceIndex, req.StartOffset, req.RequestLength);
        }
    }
}
