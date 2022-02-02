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
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;

namespace MonoTorrent.Client
{
    static class AllowedFastAlgorithm
    {
        static readonly int AllowedFastPieceCount = 10;

        internal static List<int> Calculate (SHA1 hasher, byte[] addressBytes, InfoHashes infohashes, uint numberOfPieces)
        {
            return Calculate (hasher, addressBytes, infohashes, AllowedFastPieceCount, numberOfPieces);
        }

        internal static List<int> Calculate (SHA1 hasher, byte[] addressBytes, InfoHashes infohashes, int count, uint numberOfPieces)
        {
            // BEP52: Support V2 torrents
            var infohash = infohashes.V1;
            if (infohash == null)
                return new List<int> ();

            byte[] hashBuffer = new byte[24];                // The hash buffer to be used in hashing
            var results = new List<int> (count);  // The results array which will be returned

            // 1) Convert the bytes into an int32 and make them Network order
            int ip = IPAddress.HostToNetworkOrder (BitConverter.ToInt32 (addressBytes, 0));

            // 2) binary AND this value with 0xFFFFFF00 to select the three most sigificant bytes
            int ipMostSignificant = (int) (0xFFFFFF00 & ip);

            // 3) Make ipMostSignificant into NetworkOrder
            uint ip2 = (uint) IPAddress.HostToNetworkOrder (ipMostSignificant);

            // 4) Copy ip2 into the hashBuffer
            Buffer.BlockCopy (BitConverter.GetBytes (ip2), 0, hashBuffer, 0, 4);

            // 5) Copy the infohash into the hashbuffer
            infohash.Span.CopyTo (hashBuffer.AsSpan (4, 20));

            // 6) Keep hashing and cycling until we have AllowedFastPieceCount number of results
            // Then return that result
            while (true) {
                hashBuffer = hasher.ComputeHash (hashBuffer);

                for (int i = 0; i < 20; i += 4) {
                    uint result = (uint) IPAddress.HostToNetworkOrder (BitConverter.ToInt32 (hashBuffer, i));

                    result %= numberOfPieces;
                    if (result > int.MaxValue)
                        return results;

                    results.Add ((int) result);

                    if (count == results.Count)
                        return results;
                }
            }
        }
    }
}
