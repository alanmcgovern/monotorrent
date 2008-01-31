//
// BufferManager.cs
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
using System.Text;
using System.Diagnostics;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public enum BufferType
    {
        SmallMessageBuffer,
        MediumMessageBuffer,
        LargeMessageBuffer,
        MassiveBuffer
    }

    public class BufferManager
    {
        public const int SmallMessageBufferSize = 1 << 8;               // 256 bytes
        public const int MediumMessageBufferSize = 1 << 11;             // 2048 bytes
        public const int LargeMessageBufferSize = Piece.BlockSize + 32; // 16384 bytes + 32. Enough for a complete piece aswell as the overhead
        
        public static readonly ArraySegment<byte> EmptyBuffer = new System.ArraySegment<byte>(new byte[0]);

        private Queue<ArraySegment<byte>> largeMessageBuffers;
        private Queue<ArraySegment<byte>> mediumMessageBuffers;
        private Queue<ArraySegment<byte>> smallMessageBuffers;
        private Queue<ArraySegment<byte>> massiveBuffers;

        private List<BitField> bitfields;

        /// <summary>
        /// The class that controls the allocating and deallocating of all byte[] buffers used in the engine.
        /// </summary>
        public BufferManager()
        {
            this.bitfields = new List<BitField>();
            this.massiveBuffers = new Queue<ArraySegment<byte>>();
            this.largeMessageBuffers = new Queue<ArraySegment<byte>>();
            this.mediumMessageBuffers = new Queue<ArraySegment<byte>>();
            this.smallMessageBuffers = new Queue<ArraySegment<byte>>();

            // Preallocate some of each buffer to help avoid heap fragmentation due to pinning
            this.AllocateBuffers(20, BufferType.LargeMessageBuffer);
            this.AllocateBuffers(10, BufferType.MediumMessageBuffer);
            this.AllocateBuffers(20, BufferType.SmallMessageBuffer);
        }

        internal BitField GetBitfield(int length)
        {
            lock (this.bitfields)
            {
                for (int i = 0; i < bitfields.Count; i++)
                {
                    if (this.bitfields[i].Length == length)
                    {
                        BitField b = this.bitfields[i];
                        this.bitfields.RemoveAt(i);
                        return b;
                    }
                }

                return new BitField(length);
            }
        }

        internal void FreeBitfield(ref BitField b)
        {
            lock (this.bitfields)
                this.bitfields.Add(b);

            b = null;
        }

        /// <summary>
        /// Allocates an existing buffer from the pool
        /// </summary>
        /// <param name="buffer">The byte[]you want the buffer to be assigned to</param>
        /// <param name="type">The type of buffer that is needed</param>
        private void GetBuffer(ref ArraySegment<byte> buffer, BufferType type)
        {
            // We check to see if the buffer already there is the empty buffer. If it isn't, then we have
            // a buffer leak somewhere and the buffers aren't being freed properly.
            if (buffer != EmptyBuffer)
                throw new TorrentException("The old Buffer should have been recovered before getting a new buffer");

            // If we're getting a small buffer and there are none in the pool, just return a new one.
            // Otherwise return one from the pool.
            if (type == BufferType.SmallMessageBuffer)
                lock (this.smallMessageBuffers)
                {
                    if (this.smallMessageBuffers.Count == 0)
                        this.AllocateBuffers(8, BufferType.SmallMessageBuffer);
                    buffer = this.smallMessageBuffers.Dequeue();
                }

            else if (type == BufferType.MediumMessageBuffer)
                lock (this.mediumMessageBuffers)
                {
                    if (this.mediumMessageBuffers.Count == 0)
                        this.AllocateBuffers(8, BufferType.MediumMessageBuffer);
                    buffer = this.mediumMessageBuffers.Dequeue();
                }
           
            // If we're getting a large buffer and there are none in the pool, just return a new one.
            // Otherwise return one from the pool.
            else if (type == BufferType.LargeMessageBuffer)
                lock (this.largeMessageBuffers)
                {
                    if (this.largeMessageBuffers.Count == 0)
                        this.AllocateBuffers(8, BufferType.LargeMessageBuffer);
                    buffer = this.largeMessageBuffers.Dequeue();
                }

            else
                throw new TorrentException("You cannot directly request a massive buffer");
        }


        /// <summary>
        /// Allocates an existing buffer from the pool
        /// </summary>
        /// <param name="buffer">The byte[]you want the buffer to be assigned to</param>
        /// <param name="type">The type of buffer that is needed</param>
        public void GetBuffer(ref ArraySegment<byte> buffer, int minCapacity)
        {
            if (buffer != EmptyBuffer)
                throw new TorrentException("The old Buffer should have been recovered before getting a new buffer");

            if (minCapacity <= SmallMessageBufferSize)
                GetBuffer(ref buffer, BufferType.SmallMessageBuffer);

            else if (minCapacity <= MediumMessageBufferSize)
                GetBuffer(ref buffer, BufferType.MediumMessageBuffer);

            else if (minCapacity <= LargeMessageBufferSize)
                GetBuffer(ref buffer, BufferType.LargeMessageBuffer);

            else
            {
                Logger.Log(null, "Allocating a massive buffer: " + minCapacity.ToString());
                lock (this.massiveBuffers)
                {
                    for (int i = 0; i < massiveBuffers.Count; i++)
                        if ((buffer = massiveBuffers.Dequeue()).Count >= minCapacity)
                            return;
                        else
                            massiveBuffers.Enqueue(buffer);

                    buffer = new ArraySegment<byte>(new byte[minCapacity], 0, minCapacity);
                }
            }
        }


        /// <summary>
        /// Returns a buffer to the pool after it has finished being used.
        /// </summary>
        /// <param name="buffer">The buffer to add back into the pool</param>
        /// <returns></returns>
        public void FreeBuffer(ref ArraySegment<byte> buffer)
        {
            // If true, the buffer has already been freed, so we just return
            if (buffer == EmptyBuffer)
                return;

            if (buffer.Count == SmallMessageBufferSize)
                lock (this.smallMessageBuffers)
                    this.smallMessageBuffers.Enqueue(buffer);

            else if (buffer.Count == MediumMessageBufferSize)
                lock (this.mediumMessageBuffers)
                    this.mediumMessageBuffers.Enqueue(buffer);

            else if (buffer.Count == LargeMessageBufferSize)
                lock (this.largeMessageBuffers)
                    this.largeMessageBuffers.Enqueue(buffer);

            else if (buffer.Count > LargeMessageBufferSize)
                lock (this.massiveBuffers)
                    this.massiveBuffers.Enqueue(buffer);

            // All buffers should be allocated in this class, so if something else is passed in that isn't the right size
            // We just throw an exception as someone has done something wrong.
            else
                throw new TorrentException("That buffer wasn't created by this manager");

            buffer = EmptyBuffer; // After recovering the buffer, we send the "EmptyBuffer" back as a placeholder
        }


        private void AllocateBuffers(int number, BufferType type)
        {
            if (type == BufferType.LargeMessageBuffer)
                while (number > 0)
				{
					byte[] buffer = new byte[LargeMessageBufferSize * 6];
					for(int i=0; i < 6; i++)
						this.largeMessageBuffers.Enqueue(new ArraySegment<byte>(buffer, i * LargeMessageBufferSize, LargeMessageBufferSize));
					number -= 6;
				}

            else if (type == BufferType.MediumMessageBuffer)
                while (number > 0)
				{
					byte[] buffer = new byte[MediumMessageBufferSize * 6];
					for(int i=0; i < 6; i++)
						this.mediumMessageBuffers.Enqueue(new ArraySegment<byte>(buffer, i * MediumMessageBufferSize, MediumMessageBufferSize));
					number -= 6;
				}

            else if (type == BufferType.SmallMessageBuffer)
                while (number > 0)
				{
					byte[] buffer = new byte[SmallMessageBufferSize * 6];
					for(int i=0; i < 6; i++)
						this.smallMessageBuffers.Enqueue(new ArraySegment<byte>(buffer, i * SmallMessageBufferSize, SmallMessageBufferSize));
					number -= 6;
				}

            else
                 throw new ArgumentException("Unsupported BufferType detected");
        }

        internal BitField GetClonedBitfield(BitField bitfield)
        {
            BitField clone = GetBitfield(bitfield.Length);
            Buffer.BlockCopy(bitfield.Array, 0, clone.Array, 0, clone.Array.Length * 4);
            return clone;
        }
    }
}
