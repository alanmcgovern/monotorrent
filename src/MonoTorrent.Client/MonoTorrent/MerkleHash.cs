//
// Torrent.cs
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
using System.Security.Cryptography;

namespace MonoTorrent
{
    static class MerkleHash
    {
        static Dictionary<long, ReadOnlyMemory<byte>> FinalLayerHash { get; } = CreateFinalHashPerLayer ();

        static Dictionary<long, ReadOnlyMemory<byte>> CreateFinalHashPerLayer ()
        {
            using var hasher = IncrementalHash.CreateHash (HashAlgorithmName.SHA256);
            byte[] buffer = new byte[32];

            Dictionary<long, ReadOnlyMemory<byte>> results = new Dictionary<long, ReadOnlyMemory<byte>> ();
            results[Constants.BlockSize] = (byte[]) buffer.Clone ();
            for (long i = Constants.BlockSize * 2; results.Count < 49; i *= 2) {
                hasher.AppendData (buffer);
                hasher.AppendData (buffer);
                if (!hasher.TryGetHashAndReset (buffer, out int written) || written != 32)
                    throw new Exception ("Critical failure");
                results[i] = (byte[]) buffer.Clone ();
            }
            return results;
        }

        public static bool TryHash (IncrementalHash hasher, ReadOnlyMemory<byte> src, long startLayerLength, Span<byte> computedHash, out int written)
            => TryHash (hasher, src, startLayerLength, -1, computedHash, out written);

        public static bool TryHash(IncrementalHash hasher, ReadOnlyMemory<byte> src, long startLayerLength, long endLayerLength, Span<byte> computedHash, out int written)
        {
            using var _ = MemoryPool.Default.Rent (((src.Length + 63) / 64) * 32, out Memory<byte> dest);
            while ((endLayerLength == -1 && src.Length != 32) || (endLayerLength != -1 && startLayerLength < endLayerLength)) {
                for (int i = 0; i < src.Length / 64; i++) {
                    hasher.AppendData (src.Slice (i * 64, 64));
                    if (!hasher.TryGetHashAndReset (dest.Slice (i * 32, 32).Span, out written) || written != 32)
                        return false;
                }
                if (src.Length % 64 == 32) {
                    hasher.AppendData (src.Slice (src.Length - 32, 32));
                    hasher.AppendData (FinalLayerHash[startLayerLength]);
                    if (!hasher.TryGetHashAndReset (dest.Slice (dest.Length - 32, 32).Span, out written) || written != 32)
                        return false;
                }
                src = dest;
                dest = dest.Slice (0, ((dest.Length + 63) / 64) * 32);
                startLayerLength *= 2;
            }

            if (src.Length != 32)
                throw new InvalidOperationException ("Derpo");

            written = 32;
            src.Span.Slice (0, written).CopyTo (computedHash);
            return true;
        }
    }
}
