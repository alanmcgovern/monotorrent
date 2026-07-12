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

namespace MonoTorrent
{
    public abstract partial class ByteBufferPool
    {
        public static int SmallMessageBufferSize => 256;
        public static int LargeMessageBufferSize => Constants.BlockSize + 32;

        const int AllocateDelta = 8;

        readonly BufferPool[] Pools;

        internal sealed class BufferPool
        {
            internal int BufferSize { get; }
            internal SpinLocked<Queue<ByteBuffer>> Buffers { get; }

            internal BufferPool (int bufferSize)
            {
                BufferSize = bufferSize;
                Buffers = SpinLocked.Create (new Queue<ByteBuffer> (bufferSize == -1 ? 16 : 128));
            }

            internal ByteBuffer Rent (int capacity)
            {
                using (Buffers.Enter (out var buffers)) {
                    if (BufferSize == -1) {
                        for (int i = 0; i < buffers.Count; i++) {
                            var buffer = buffers.Dequeue ();
                            if (buffer.Memory.Length >= capacity)
                                return buffer;
                            buffers.Enqueue (buffer);
                        }

                        return CreateBuffer (capacity);
                    }

                    if (buffers.Count == 0)
                        AllocateBuffers (AllocateDelta, buffers);
                    return buffers.Dequeue ();
                }
            }

            internal void Return (ByteBuffer buffer)
            {
                using (Buffers.Enter (out var buffers))
                    buffers.Enqueue (buffer);
            }

            internal void AllocateBuffers (int count)
            {
                using (Buffers.Enter (out var buffers))
                    AllocateBuffers (count, buffers);
            }

            void AllocateBuffers (int count, Queue<ByteBuffer> buffers)
            {
                for (int i = 0; i < count; i++)
                    buffers.Enqueue (CreateBuffer (BufferSize));
            }

            ByteBuffer CreateBuffer (int bufferSize)
                => new ByteBuffer (this, new ArraySegment<byte> (GC.AllocateUninitializedArray<byte> (bufferSize, pinned: BufferSize != -1)));
        }

        /// <summary>
        /// Creates a pool containing one pool for each requested buffer size. A size of <c>-1</c>
        /// creates a variable-sized pool which can satisfy requests larger than the fixed pools.
        /// </summary>
        protected ByteBufferPool (params int[] bufferSizes)
        {
            ArgumentNullException.ThrowIfNull (bufferSizes);
            if (bufferSizes.Length == 0)
                throw new ArgumentException ("At least one buffer size must be specified.", nameof (bufferSizes));

            Pools = new BufferPool[bufferSizes.Length];
            for (int i = 0; i < bufferSizes.Length; i++) {
                if (bufferSizes[i] == 0 || bufferSizes[i] < -1)
                    throw new ArgumentOutOfRangeException (nameof (bufferSizes), "Buffer sizes must be positive, or -1 for variable-sized buffers.");
                Pools[i] = new BufferPool (bufferSizes[i]);
                if (bufferSizes[i] != -1)
                    Pools[i].AllocateBuffers (AllocateDelta * 4);
            }
        }

        protected Releaser Rent (int capacity, out Memory<byte> buffer)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException (nameof (capacity));

            BufferPool? selected = null;
            foreach (var pool in Pools) {
                if (pool.BufferSize == -1) {
                    selected ??= pool;
                } else if (pool.BufferSize >= capacity && (selected == null || selected.BufferSize == -1 || pool.BufferSize < selected.BufferSize)) {
                    selected = pool;
                }
            }

            if (selected == null)
                throw new ArgumentOutOfRangeException (nameof (capacity), "No configured buffer pool can satisfy this capacity.");

            var byteBuffer = selected.Rent (capacity);
            buffer = byteBuffer.Memory;
            return new Releaser (byteBuffer);
        }
    }
}
