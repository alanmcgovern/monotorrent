﻿//
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


using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace System
{
    static class MonoTorrentMemoryExtensions
    {
#if NETSTANDARD2_0 || NET472
        public static void NextBytes (this Random random, Span<byte> buffer)
        {
            while (buffer.Length >= 4) {
                Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian (buffer, random.Next ());
                buffer = buffer.Slice (4);
            }

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = (byte) random.Next ();
        }

        public static void SetBuffer(this SocketAsyncEventArgs args, Memory<byte> memory)
        {
            if (!MemoryMarshal.TryGetArray (memory, out ArraySegment<byte> segment))
                throw new InvalidOperationException ("Could not retrieve the underlying byte[] from the Memory<T>");
            args.SetBuffer (segment.Array, segment.Offset, segment.Count);
        }

        public static bool TryComputeHash (this HashAlgorithm hasher, ReadOnlySpan<byte> buffer, Span<byte> destination, out int written)
        {
            var array = new byte[Math.Min (buffer.Length, 4096)];
            while (buffer.Length > 0) {
                var sliceSize = Math.Min (array.Length, buffer.Length);
                buffer.Slice (0, sliceSize).CopyTo (array);
                hasher.TransformBlock (array, 0, array.Length, array, 0);
                buffer = buffer.Slice (sliceSize);
            }
            hasher.TransformFinalBlock (array, 0, 0);
            hasher.Hash.AsSpan ().CopyTo (destination);
            written = hasher.Hash.Length;
            return true;
        }
#endif
        

        public static void AppendData (this IncrementalHash incrementalHash, Memory<byte> buffer)
        {
#if NETSTANDARD2_0 || NET472
            if (!MemoryMarshal.TryGetArray (buffer, out ArraySegment<byte> array))
                throw new Exception ("Could not get the underyling byte[] for hashing");
            incrementalHash.AppendData (array.Array, array.Offset, array.Count);
#else
            incrementalHash.AppendData (buffer.Span);
#endif
        }

        public static int Read (this Stream stream, Memory<byte> memory)
        {
#if NETSTANDARD2_0 || NET472
            if (!MemoryMarshal.TryGetArray (memory, out ArraySegment<byte> segment))
                throw new InvalidOperationException ("Could not retrieve the underlying byte[] from the Memory<T>");
            return stream.Read (segment.Array, segment.Offset, segment.Count);
#else
            return stream.Read (memory.Span);
#endif
        }

        public static void Write (this Stream stream, ReadOnlyMemory<byte> memory)
        {
#if NETSTANDARD2_0
            if (!MemoryMarshal.TryGetArray (memory, out ArraySegment<byte> segment))
                throw new InvalidOperationException ("Could not retrieve the underlying byte[] from the Memory<T>");
            stream.Write (segment.Array, segment.Offset, segment.Count);
#else
            stream.Write (memory.Span);
#endif
        }
    }
}
