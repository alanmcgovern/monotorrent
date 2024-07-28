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
    public abstract partial class ByteBufferPool
    {
        public static int SmallMessageBufferSize => 256;
        public static int LargeMessageBufferSize => Constants.BlockSize + 32;

        const int AllocateDelta = 8;


        SpinLocked<Stack<ByteBuffer>> LargeMessageBuffers { get; }
        SpinLocked<Queue<ByteBuffer>> MassiveBuffers { get; }
        SpinLocked<Stack<ByteBuffer>> SmallMessageBuffers { get; }


        /// <summary>
        /// The class that controls the allocating and deallocating of all byte[] buffers used in the engine.
        /// </summary>
        protected ByteBufferPool ()
        {
            LargeMessageBuffers = SpinLocked.Create (new Stack<ByteBuffer> (128));
            MassiveBuffers = SpinLocked.Create (new Queue<ByteBuffer> (16));
            SmallMessageBuffers = SpinLocked.Create (new Stack<ByteBuffer> (128));

            // Preallocate some of each buffer to help avoid heap fragmentation due to pinning
            using (LargeMessageBuffers.Enter (out var buffers))
                AllocateBuffers (AllocateDelta * 4, buffers, LargeMessageBufferSize);
            using (SmallMessageBuffers.Enter (out var buffers))
                AllocateBuffers (AllocateDelta * 4, buffers, SmallMessageBufferSize);
        }

        protected Releaser Rent (int capacity, out Memory<byte> buffer)
        {
            var result = Rent (capacity, out ByteBuffer buf);
            buffer = buf.Memory;
            return result;
        }

        protected Releaser Rent (int capacity, out ArraySegment<byte> segment)
        {
            var result = Rent (capacity, out ByteBuffer buf);
            segment = buf.Segment;
            return result;
        }

        Releaser Rent (int capacity, out ByteBuffer buffer)
        {
            if (capacity <= SmallMessageBufferSize) {
                using (SmallMessageBuffers.Enter (out var buffers)) {
                    if (buffers.Count == 0)
                        AllocateBuffers (AllocateDelta, buffers, SmallMessageBufferSize);
                    buffer = buffers.Pop ();
                }
                return new Releaser (this, buffer);
            } else if (capacity <= LargeMessageBufferSize) {
                using (LargeMessageBuffers.Enter (out var buffers)) {
                    if (buffers.Count == 0)
                        AllocateBuffers (AllocateDelta, buffers, LargeMessageBufferSize);
                    buffer = buffers.Pop ();
                }
                return new Releaser (this, buffer);
            } else {
                using (MassiveBuffers.Enter (out var buffers)) {
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

        static void AllocateBuffers (int count, Stack<ByteBuffer> bufferQueue, int bufferSize)
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
#if NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP3_0 || NET472
            for (int i = 0; i < count; i++)
                bufferQueue.Push(new ByteBuffer (new ArraySegment<byte> (new byte[bufferSize])));
#else
            for (int i = 0; i < count; i++)
                bufferQueue.Push (new ByteBuffer (new ArraySegment<byte> (GC.AllocateUninitializedArray<byte> (bufferSize, pinned: true))));
#endif
        }
    }
}
