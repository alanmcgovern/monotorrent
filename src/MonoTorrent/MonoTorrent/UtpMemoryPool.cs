using System;

namespace MonoTorrent
{
    public sealed class UtpMemoryPool
    {
        public const int BufferSize = 1500;

        public static readonly UtpMemoryPool Default = new UtpMemoryPool ();

        readonly ByteBufferPool Pool = new ByteBufferPool (BufferSize);

        public ByteBufferPool.Releaser Rent (out Memory<byte> memory)
        {
            var releaser = Pool.Rent (BufferSize, out memory);
#if DEBUG
            memory.Span.Fill (255);
#endif
            return releaser;
        }
    }
}
