//
// BufferPool.cs
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
using System.Collections.Generic;
using System.Threading;

namespace MonoTorrent
{
    public partial class ByteBufferPool
    {
        const int AllocateDelta = 8;


        int FixedBufferSize { get; }
        SpinLocked<Queue<ByteBuffer>> Buffers { get; }


        /// <summary>
        /// The class that controls the allocating and deallocating of all byte[] buffers used in the engine.
        /// </summary>
        public ByteBufferPool (int bufferSize)
        {
            if (bufferSize == 0 || bufferSize < -1)
                throw new ArgumentOutOfRangeException (nameof (bufferSize));

            FixedBufferSize = bufferSize;
            Buffers = SpinLocked.Create (new Queue<ByteBuffer> (128));

            if (FixedBufferSize != -1) {
                using (Buffers.Enter (out var buffers))
                    AllocateBuffers (AllocateDelta * 4, buffers, FixedBufferSize);
            }
        }

        public Releaser Rent (int capacity, out Memory<byte> buffer)
        {
            var result = Rent (capacity, out ByteBuffer buf);
            buffer = buf.Memory;
            return result;
        }

        Releaser Rent (int capacity, out ByteBuffer buffer)
        {
            if (capacity < 0 || (FixedBufferSize != -1 && capacity > FixedBufferSize))
                throw new ArgumentOutOfRangeException (nameof (capacity));

            if (FixedBufferSize != -1) {
                using (Buffers.Enter (out var buffers)) {
                    if (buffers.Count == 0)
                        AllocateBuffers (AllocateDelta, buffers, FixedBufferSize);
                    buffer = buffers.Dequeue ();
                }
                return new Releaser (this, buffer);
            } else {
                using (Buffers.Enter (out var buffers)) {
                    for (int i = 0; i < buffers.Count; i++)
                        if ((buffer = buffers.Dequeue ()).Memory.Length >= capacity)
                            return new Releaser (this, buffer);
                        else
                            buffers.Enqueue (buffer);
                }
                buffer = new ByteBuffer (new ArraySegment<byte> (new byte[capacity]));
                return new Releaser (this, buffer);
            }
        }

        static void AllocateBuffers (int count, Queue<ByteBuffer> bufferQueue, int bufferSize)
        {
            // This code used to allocate a single buffer of size `bufferSize * count` which would
            // then be split into discrete segments to be consumed by the library. The intention
            // was to reduce pinning by forcibly allocating in the large object heap.
            //
            // .NET 5 has a new mechanism for allocating objects into the pinned heap. Let's use
            // that to reduce pinning related fragmentation and for older frameworks people can
            // just live with the pinning/fragmentation.
            //
            // This is safer than allocating one massive buffer which is placed in the large object heap
            // as there's no guarantee that a buffer won't be 'lost', and at the moment that could lead to
            // pretty poor memory utilisation if we keep losing segments of really large buffers.
            for (int i = 0; i < count; i++)
                bufferQueue.Enqueue (new ByteBuffer (new ArraySegment<byte> (GC.AllocateUninitializedArray<byte> (bufferSize, pinned: true))));
        }
    }
}
