//
// DiskWriter.cs
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
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using MonoTorrent;

using ReusableTasks;

namespace System
{
#if NETSTANDARD2_0 || NET472
    static class StreamExtensions
    {
        public static async ReusableTask<int> ReadAsync (this Stream stream, Memory<byte> buffer)
        {
            using (MemoryPool.Default.RentArraySegment (buffer.Length, out var segment)) {
                int result = await stream.ReadAsync (segment.Array, segment.Offset, buffer.Length);
                segment.AsSpan (0, result).CopyTo (buffer.Span);
                return result;
            }
        }
    }

    static class RandomNumberGeneratorExtensions
    {
        public static void GetBytes (this RandomNumberGenerator random, Span<byte> buffer)
        {
            using (MonoTorrent.MemoryPool.Default.RentArraySegment (buffer.Length, out ArraySegment<byte> segment)) {
                random.GetBytes (segment.Array, segment.Offset, buffer.Length);
                segment.Array.AsSpan (segment.Offset, buffer.Length).CopyTo (buffer);
            }
        }
    }
#endif
}
