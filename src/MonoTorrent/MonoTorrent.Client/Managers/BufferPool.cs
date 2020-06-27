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
    class BufferPool
    {
        internal struct Releaser : IDisposable
        {
            internal byte[] Buffer;
            BufferPool Pool;

            internal Releaser (BufferPool pool, byte[] buffer)
            {
                Pool = pool;
                Buffer = buffer;
            }

            public void Dispose ()
            {
                Pool.Return (Buffer);
                Pool = null;
                Buffer = null;
            }
        }

        const int AllocateDelta = 8;

        const int SmallMessageBufferSize = 256;
        const int MediumMessageBufferSize = 2048;
        const int LargeMessageBufferSize = Piece.BlockSize + 32; // 16384 bytes + 32. Enough for a complete piece aswell as the overhead

        HashSet<byte[]> AllocatedBuffers { get; }
        Queue<byte[]> LargeMessageBuffers { get; }
        Queue<byte[]> MassiveBuffers { get; }
        Queue<byte[]> MediumMessageBuffers { get; }
        Queue<byte[]> SmallMessageBuffers { get; }

        /// <summary>
        /// The class that controls the allocating and deallocating of all byte[] buffers used in the engine.
        /// </summary>
        public BufferPool ()
        {
            AllocatedBuffers = new HashSet<byte[]> ();
            LargeMessageBuffers = new Queue<byte[]> ();
            MassiveBuffers = new Queue<byte[]> ();
            MediumMessageBuffers = new Queue<byte[]> ();
            SmallMessageBuffers = new Queue<byte[]> ();

            // Preallocate some of each buffer to help avoid heap fragmentation due to pinning
            AllocateBuffers (AllocateDelta, LargeMessageBuffers, LargeMessageBufferSize);
            AllocateBuffers (AllocateDelta, MediumMessageBuffers, MediumMessageBufferSize);
            AllocateBuffers (AllocateDelta, SmallMessageBuffers, SmallMessageBufferSize);
        }

        void Return (byte[] buffer)
        {
            if (buffer.Length == SmallMessageBufferSize)
                lock (SmallMessageBuffers)
                    SmallMessageBuffers.Enqueue (buffer);

            else if (buffer.Length == MediumMessageBufferSize)
                lock (MediumMessageBuffers)
                    MediumMessageBuffers.Enqueue (buffer);

            else if (buffer.Length == LargeMessageBufferSize)
                lock (LargeMessageBuffers)
                    LargeMessageBuffers.Enqueue (buffer);

            else if (buffer.Length > LargeMessageBufferSize)
                lock (MassiveBuffers)
                    MassiveBuffers.Enqueue (buffer);
        }

        public Releaser Rent (int minCapacity, out byte[] buffer)
        {
            if (minCapacity <= SmallMessageBufferSize)
                return Rent (SmallMessageBuffers, SmallMessageBufferSize, out buffer);

            if (minCapacity <= MediumMessageBufferSize)
                return Rent (MediumMessageBuffers, MediumMessageBufferSize, out buffer);

            if (minCapacity <= LargeMessageBufferSize)
                return Rent (LargeMessageBuffers, LargeMessageBufferSize, out buffer);

            lock (MassiveBuffers) {
                for (int i = 0; i < MassiveBuffers.Count; i++)
                    if ((buffer = MassiveBuffers.Dequeue ()).Length >= minCapacity)
                        return new Releaser (this, buffer);
                    else
                        MassiveBuffers.Enqueue (buffer);

                buffer = new byte[minCapacity];
                lock (AllocatedBuffers)
                    AllocatedBuffers.Add (buffer);
                return new Releaser (this, buffer);
            }
        }

        Releaser Rent (Queue<byte[]> buffers, int bufferSize, out byte[] buffer)
        {
            lock (buffers) {
                if (buffers.Count == 0)
                    AllocateBuffers (AllocateDelta, buffers, bufferSize);
                buffer = buffers.Dequeue ();
                return new Releaser (this, buffer);
            }
        }

        public bool Owns (byte[] buffer)
        {
            lock (AllocatedBuffers)
                return AllocatedBuffers.Contains (buffer);
        }

        void AllocateBuffers (int count, Queue<byte[]> bufferQueue, int bufferSize)
        {
            while (count-- > 0) {
                byte[] buffer = new byte[bufferSize];
                bufferQueue.Enqueue (buffer);
                lock (AllocatedBuffers)
                    AllocatedBuffers.Add (buffer);
            }
        }
    }
}
