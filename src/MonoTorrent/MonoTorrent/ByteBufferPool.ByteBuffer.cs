﻿//
// ByteBufferPool.ByteBuffer.cs
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
using System.Net.Sockets;

namespace MonoTorrent
{
    public partial class ByteBufferPool
    {
        internal sealed class ByteBuffer
        {
            // Used to prevent double-frees
            internal int Counter { get; set; }

            readonly SocketMemory socketMemory;

            public Memory<byte> Memory { get; private set; }
            public SocketMemory SocketMemory => socketMemory.IsEmpty ? throw new InvalidOperationException ("SocketMemory can only be used if the ByteBuffer has a socketAsyncArgs") : socketMemory;
            public Span<byte> Span => Memory.Span;

            public ArraySegment<byte> Segment { get; private set; }

            public ByteBuffer (Memory<byte> memory, bool createSocketAsyncArgs)
            {
                Memory = memory;
                if (createSocketAsyncArgs)
                    socketMemory = new SocketMemory (Memory, new SocketAsyncEventArgs ());
            }

            public ByteBuffer (ArraySegment<byte> segment)
            {
                Segment = segment;
            }
        }
    }
}
