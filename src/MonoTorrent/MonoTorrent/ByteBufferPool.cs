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


        Queue<ByteBuffer> LargeMessageBuffers { get; }
        Queue<ByteBuffer> MassiveBuffers { get; }
        Queue<ByteBuffer> SmallMessageBuffers { get; }


        /// <summary>
        /// The class that controls the allocating and deallocating of all byte[] buffers used in the engine.
        /// </summary>
        protected ByteBufferPool ()
        {
            LargeMessageBuffers = new Queue<ByteBuffer> ();
            MassiveBuffers = new Queue<ByteBuffer> ();
            SmallMessageBuffers = new Queue<ByteBuffer> ();

            // Preallocate some of each buffer to help avoid heap fragmentation due to pinning
            AllocateBuffers (AllocateDelta * 4, LargeMessageBuffers, LargeMessageBufferSize);
            AllocateBuffers (AllocateDelta * 4, SmallMessageBuffers, SmallMessageBufferSize);
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
            if (capacity <= SmallMessageBufferSize)
                return Rent (SmallMessageBuffers, SmallMessageBufferSize, out buffer);

            if (capacity <= LargeMessageBufferSize)
                return Rent (LargeMessageBuffers, LargeMessageBufferSize, out buffer);

            lock (MassiveBuffers) {
                for (int i = 0; i < MassiveBuffers.Count; i++)
                    if ((buffer = MassiveBuffers.Dequeue ()).Span.Length >= capacity)
                        return new Releaser (MassiveBuffers, buffer);
                    else
                        MassiveBuffers.Enqueue (buffer);

                buffer = new ByteBuffer (new ArraySegment<byte> (new byte[capacity]));
                return new Releaser (MassiveBuffers, buffer);
            }
        }

        Releaser Rent (Queue<ByteBuffer> buffers, int bufferSize, out ByteBuffer buffer)
        {
            lock (buffers) {
                if (buffers.Count == 0)
                    AllocateBuffers (AllocateDelta, buffers, bufferSize);
                buffer = buffers.Dequeue ();
                return new Releaser (buffers, buffer);
            }
        }

        void AllocateBuffers (int count, Queue<ByteBuffer> bufferQueue, int bufferSize)
        {
            var buffer = new byte[count * bufferSize];
            for (int i = 0; i < count; i++)
                bufferQueue.Enqueue (new ByteBuffer (new ArraySegment<byte> (buffer, i * bufferSize, bufferSize)));
        }
    }
}
