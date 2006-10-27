using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace MonoTorrent.Client
{
    internal enum BufferType
    {
        SmallMessageBuffer,
        LargeMessageBuffer
    }

    internal class BufferManager
    {
        public const int SmallMessageBufferSize = 256;
        public const int LargeMessageBufferSize = ((1 << 14) + 32);

        private Queue<byte[]> smallMessageBuffers;
        private Queue<byte[]> largeMessageBuffers;

        public BufferManager()
        {
            this.largeMessageBuffers = new Queue<byte[]>();
            this.smallMessageBuffers = new Queue<byte[]>();
        }

        private int largebuffers = 0;
        private int smallbuffers = 0;
        public byte[] GetBuffer(BufferType type)
        {
            if (type == BufferType.SmallMessageBuffer)
                lock (this.smallMessageBuffers)
                {
                    if (this.smallMessageBuffers.Count == 0)
                    {
                        Debug.WriteLine((++smallbuffers).ToString() + " Small Buffers");
                        return new byte[SmallMessageBufferSize];
                    }
                    else
                    {
                        //return new byte[SmallMessageBufferSize];
                        return this.smallMessageBuffers.Dequeue();
                    }
                }

            else if (type == BufferType.LargeMessageBuffer)
                lock (this.largeMessageBuffers)
                {
                    if (this.largeMessageBuffers.Count == 0)
                    {
                        Debug.WriteLine((++largebuffers).ToString() + " Large Buffers");
                        return new byte[LargeMessageBufferSize];
                    }
                    else
                    {
                        //return new byte[LargeMessageBufferSize];
                        return this.largeMessageBuffers.Dequeue();
                    }
                }

            else
                throw new Exception("Error: Illegal buffer supplied");
#warning Don't throw a System.Exception. Rethink the whole exception thing to allow for better handling of my own exceptions
        }

        public void FreeBuffer(byte[] buffer)
        {
            //buffer = null;
            //return;

            if (buffer == null)
                return;

            if (buffer.Length == SmallMessageBufferSize)
                lock (this.smallMessageBuffers)
                    this.smallMessageBuffers.Enqueue(buffer);

            else if (buffer.Length == LargeMessageBufferSize)
                lock (this.largeMessageBuffers)
                    this.largeMessageBuffers.Enqueue(buffer);

            else
                throw new Exception("That buffer wasn't created by this manager");
#warning Don't throw a System.Exception. Rethink the whole exception thing to allow for better handling of my own exceptions
        }
    }
}
