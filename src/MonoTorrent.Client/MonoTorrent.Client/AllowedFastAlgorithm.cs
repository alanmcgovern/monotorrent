//
// AllowedFastAlgorithm.cs
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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;

namespace MonoTorrent.Client
{
    static class AllowedFastAlgorithm
    {
        static readonly int AllowedFastPieceCount = 10;

        internal static ReadOnlyMemory<int> Calculate (SHA1 hasher, ReadOnlySpan<byte> addressBytes, InfoHashes infohashes, uint numberOfPieces)
        {
            return Calculate (hasher, addressBytes, infohashes, AllowedFastPieceCount, numberOfPieces);
        }

        static ReadOnlyMemory<int> Calculate (SHA1 hasher, ReadOnlySpan<byte> addressBytes, InfoHashes infohashes, int count, uint numberOfPieces)
        {
            // BEP52: Support V2 torrents
            var infohash = infohashes.V1;
            if (infohash == null)
                return Array.Empty<int> ();

            Span<byte> hashBuffer = stackalloc byte[24];
            Memory<int> results = new int[count];

            // 1) Convert the bytes into an int32 and make them Network order
            int ip = BinaryPrimitives.ReadInt32BigEndian (addressBytes);

            // 2) binary AND this value with 0xFFFFFF00 to select the three most sigificant bytes
            int ipMostSignificant = (int) (0xFFFFFF00 & ip);

            // 4) Copy ip2 into the hashBuffer
            BinaryPrimitives.TryWriteInt32BigEndian (hashBuffer.Slice (0, 4), ipMostSignificant);

            // 5) Copy the infohash into the hashbuffer
            infohash.Span.CopyTo (hashBuffer.Slice (4, 20));

            // 6) Keep hashing and cycling until we have AllowedFastPieceCount number of results
            // Then return that result
            int resultsCount = 0;
            while (true) {
                if (!hasher.TryComputeHash (hashBuffer, hashBuffer, out int bytesWritten))
                    throw new InvalidOperationException ();
                hashBuffer = hashBuffer.Slice (0, bytesWritten);

                for (int i = 0; i < 20; i += 4) {
                    uint result = BinaryPrimitives.ReadUInt32BigEndian (hashBuffer.Slice (i, 4));

                    result %= numberOfPieces;
                    if (result > int.MaxValue)
                        return results;

                    results.Span[resultsCount++] = (int) result;
                    if (count == resultsCount)
                        return results;
                }
            }
        }
    }
}
