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

using MonoTorrent.Messages.Peer.FastPeer;

namespace MonoTorrent.Messages.Peer
{
    public class AllowedFastBundle : PeerMessage, IRentable
    {
        static readonly MemoryPool Pool = MemoryPool.Default;
        readonly AllowedFastMessage RequestMessage = new AllowedFastMessage ();

        ByteBufferPool.Releaser RequestsMemoryReleaser;

        Memory<byte> AllowedFastIndicesMemory;
        Span<int> AllowedFastIndices => MemoryMarshal.Cast<byte, int> (AllowedFastIndicesMemory.Span);

        public override int ByteLength => RequestMessage.ByteLength * AllowedFastIndices.Length;

        public AllowedFastBundle ()
        {
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            throw new InvalidOperationException ();
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            for (int i = 0; i < AllowedFastIndices.Length; i++) {
                RequestMessage.Initialize (AllowedFastIndices[i]);
                buffer = buffer.Slice (RequestMessage.Encode (buffer));
            }

            return written - buffer.Length;
        }

        public void Initialize (ReadOnlySpan<int> allowedFastIndexes)
        {
            var usedSize = MemoryMarshal.AsBytes (allowedFastIndexes).Length;
            RequestsMemoryReleaser = Pool.Rent (usedSize, out AllowedFastIndicesMemory);
            allowedFastIndexes.CopyTo (AllowedFastIndices);
        }

        protected override void Reset ()
        {
            base.Reset ();
            RequestsMemoryReleaser.Dispose ();
            (RequestsMemoryReleaser, AllowedFastIndicesMemory) = (default, default);
        }
    }
}
