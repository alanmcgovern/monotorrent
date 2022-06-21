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
using System.Diagnostics.CodeAnalysis;

namespace MonoTorrent
{
    public partial class MemoryPool : ByteBufferPool
    {
        public static readonly MemoryPool Default = new MemoryPool ();

        public MemoryPool ()
        {
        }

        public new Releaser Rent (int capacity, out Memory<byte> memory)
        {
            var releaser = base.Rent (capacity, out memory);
            memory = memory.Slice (0, capacity);
#if DEBUG
            memory.Span.Fill (255);
#endif
            return releaser;
        }

        public new Releaser Rent (int capacity, [NotNullWhen(true)] out ArraySegment<byte> segment)
        {
            var releaser = base.Rent (capacity, out segment);
            segment = new ArraySegment<byte> (segment.Array!, segment.Offset, capacity);
#if DEBUG
            segment.AsSpan ().Fill (255);
#endif
            return releaser;
        }
    }
}
