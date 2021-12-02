//
// ByteBuffer.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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
using System.Runtime.InteropServices;

namespace MonoTorrent
{
    public readonly struct SocketMemory
    {
        public readonly Memory<byte> Memory;
        public readonly SocketAsyncEventArgs SocketArgs;

        public bool IsEmpty => Memory.IsEmpty;

        public int Length => Memory.Length;

        public Span<byte> Span
            => Memory.Span;

        internal SocketMemory (Memory<byte> memory, SocketAsyncEventArgs socketArgs)
        {
            Memory = memory;
            SocketArgs = socketArgs;
        }

        public Span<byte> AsSpan ()
            => Memory.Span;

        public Span<byte> AsSpan (int start)
           => Memory.Span.Slice (start);

        public Span<byte> AsSpan (int start, int length)
           => Memory.Span.Slice (start, length);

        public SocketMemory Slice (int start)
            => new SocketMemory (Memory.Slice (start), SocketArgs);

        public SocketMemory Slice (int start, int length)
            => new SocketMemory (Memory.Slice (start, length), SocketArgs);
    }
}
