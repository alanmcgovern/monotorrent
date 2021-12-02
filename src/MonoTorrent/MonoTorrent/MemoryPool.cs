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
    public partial class MemoryPool : ByteBufferPool
    {
        public static MemoryPool Default = new MemoryPool ();

        List<ByteBuffer> ArraySegments { get; }

        public MemoryPool ()
            : base (false)
        {
            ArraySegments = new List<ByteBuffer> ();

        }

        public ArraySegmentReleaser RentArraySegment (int capacity, out ArraySegment<byte> segment)
        {
            lock (ArraySegments) {
                for (int i = 0; i < ArraySegments.Count; i++) {
                    if (ArraySegments[i].Segment.Count >= capacity) {
                        var buffer = ArraySegments[i];
                        ArraySegments.RemoveAt (i);
                        segment = buffer.Segment;
                        return new ArraySegmentReleaser (ArraySegments, buffer);
                    }
                }
            }

            // The (only?) user of this in the engine is the code which generates randoms for the NET472
            // profile, and it uses buffers of size [96 + Rand(0, 512)]. If we always generate 1kB buffers
            // we can probably end up creating and reusing one buffer for the entire lifespan of the engine.
            segment = new ArraySegment<byte> (new byte[Math.Max (capacity, 1024)]);
            return new ArraySegmentReleaser (ArraySegments, new ByteBuffer (segment));
        }

        public (Releaser, Memory<byte>) Rent (int capacity)
        {
            var releaser = base.Rent (capacity, out Memory<byte> memory);
            return (releaser, memory.Slice (0, capacity));
        }

        public new Releaser Rent (int capacity, out Memory<byte> memory)
        {
            var releaser = base.Rent (capacity, out memory);
            memory = memory.Slice (0, capacity);
            return releaser;
        }
    }
}
