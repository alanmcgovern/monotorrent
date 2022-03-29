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
using System.Runtime.InteropServices;

namespace MonoTorrent.Messages.Peer
{
    public class RequestBundle : PeerMessage
    {
        static readonly MemoryPool Pool = MemoryPool.Default;
        static readonly int RequestMessageLength = new RequestMessage ().ByteLength;

        ByteBufferPool.Releaser RequestsMemoryReleaser;
        Memory<byte> RequestsMemory;
        Span<BlockInfo> Requests => MemoryMarshal.Cast<byte, BlockInfo> (RequestsMemory.Span);

        public override int ByteLength => RequestMessageLength * Requests.Length;

        public RequestBundle ()
        {
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            throw new InvalidOperationException ();
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            using (Rent (out RequestMessage message)) {
                for (int i = 0; i < Requests.Length; i++) {
                    message.Initialize (Requests[i]);
                    buffer = buffer.Slice (message.Encode (buffer));
                }
            }

            return written - buffer.Length;
        }

        public void Initialize (Span<BlockInfo> requests)
        {
            RequestsMemoryReleaser = Pool.Rent (MemoryMarshal.AsBytes (requests).Length, out Memory<byte> memory);
            RequestsMemory = memory;
            requests.CopyTo (Requests);
        }

        protected override void Reset ()
        {
            base.Reset ();
            RequestsMemoryReleaser.Dispose ();
            (RequestsMemoryReleaser, RequestsMemory) = (default, default);
        }
    }
}
