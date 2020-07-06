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

namespace MonoTorrent.Client
{
    public class ByteBufferPool
    {
        internal struct Releaser : IDisposable
        {
            internal ByteBuffer Buffer;
            Queue<ByteBuffer> Pool;

            internal Releaser (Queue<ByteBuffer> pool, ByteBuffer buffer)
            {
                Pool = pool;
                Buffer = buffer;
            }

            public void Dispose ()
            {
                if (Pool == null)
                    return;

                if (Buffer == null)
                    throw new InvalidOperationException ("This buffer has been double-freed, which implies it was used after a previews free.");

                lock (Pool)
                    Pool.Enqueue (Buffer);
                Pool = null;
            }
        }

        const int AllocateDelta = 8;

        const int SmallMessageBufferSize = 256;
        const int MediumMessageBufferSize = 2048;
        const int LargeMessageBufferSize = Piece.BlockSize + 32; // 16384 bytes + 32. Enough for a complete piece aswell as the overhead

        Queue<ByteBuffer> LargeMessageBuffers { get; }
        Queue<ByteBuffer> MassiveBuffers { get; }
        Queue<ByteBuffer> MediumMessageBuffers { get; }
        Queue<ByteBuffer> SmallMessageBuffers { get; }

        /// <summary>
        /// The class that controls the allocating and deallocating of all byte[] buffers used in the engine.
        /// </summary>
        public ByteBufferPool ()
        {
            LargeMessageBuffers = new Queue<ByteBuffer> ();
            MassiveBuffers = new Queue<ByteBuffer> ();
            MediumMessageBuffers = new Queue<ByteBuffer> ();
            SmallMessageBuffers = new Queue<ByteBuffer> ();

            // Preallocate some of each buffer to help avoid heap fragmentation due to pinning
            AllocateBuffers (AllocateDelta, LargeMessageBuffers, LargeMessageBufferSize);
            AllocateBuffers (AllocateDelta, MediumMessageBuffers, MediumMessageBufferSize);
            AllocateBuffers (AllocateDelta, SmallMessageBuffers, SmallMessageBufferSize);
        }

        internal Releaser Rent (int minCapacity, out byte[] buffer)
        {
            var result = Rent (minCapacity, out ByteBuffer dataBuffer);
            buffer = dataBuffer.Data;
            return result;
        }

        internal Releaser Rent (int minCapacity, out ByteBuffer buffer)
        {
            if (minCapacity <= SmallMessageBufferSize)
                return Rent (SmallMessageBuffers, SmallMessageBufferSize, out buffer);

            if (minCapacity <= MediumMessageBufferSize)
                return Rent (MediumMessageBuffers, MediumMessageBufferSize, out buffer);

            if (minCapacity <= LargeMessageBufferSize)
                return Rent (LargeMessageBuffers, LargeMessageBufferSize, out buffer);

            lock (MassiveBuffers) {
                for (int i = 0; i < MassiveBuffers.Count; i++)
                    if ((buffer = MassiveBuffers.Dequeue ()).Data.Length >= minCapacity)
                        return new Releaser (MassiveBuffers, buffer);
                    else
                        MassiveBuffers.Enqueue (buffer);

                buffer = new ByteBuffer(minCapacity);
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
            while (count-- > 0)
                bufferQueue.Enqueue (new ByteBuffer (bufferSize));
        }
    }
}
