using System;
using System.Net;
using System.Security.Cryptography;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public static class AllowedFastAlgorithm
    {
        internal static readonly int AllowedFastPieceCount = 10;
        private static readonly SHA1 hasher = HashAlgoFactory.Create<SHA1>();

        internal static MonoTorrentCollection<int> Calculate(byte[] addressBytes, InfoHash infohash,
            uint numberOfPieces)
        {
            return Calculate(addressBytes, infohash, AllowedFastPieceCount, numberOfPieces);
        }

        internal static MonoTorrentCollection<int> Calculate(byte[] addressBytes, InfoHash infohash, int count,
            uint numberOfPieces)
        {
            var hashBuffer = new byte[24]; // The hash buffer to be used in hashing
            var results = new MonoTorrentCollection<int>(count);
            // The results array which will be returned

            // 1) Convert the bytes into an int32 and make them Network order
            var ip = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(addressBytes, 0));

            // 2) binary AND this value with 0xFFFFFF00 to select the three most sigificant bytes
            var ipMostSignificant = (int) (0xFFFFFF00 & ip);

            // 3) Make ipMostSignificant into NetworkOrder
            var ip2 = (uint) IPAddress.HostToNetworkOrder(ipMostSignificant);

            // 4) Copy ip2 into the hashBuffer
            Buffer.BlockCopy(BitConverter.GetBytes(ip2), 0, hashBuffer, 0, 4);

            // 5) Copy the infohash into the hashbuffer
            Buffer.BlockCopy(infohash.Hash, 0, hashBuffer, 4, 20);

            // 6) Keep hashing and cycling until we have AllowedFastPieceCount number of results
            // Then return that result
            while (true)
            {
                lock (hasher)
                    hashBuffer = hasher.ComputeHash(hashBuffer);

                for (var i = 0; i < 20; i += 4)
                {
                    var result = (uint) IPAddress.HostToNetworkOrder(BitConverter.ToInt32(hashBuffer, i));

                    result = result%numberOfPieces;
                    if (result > int.MaxValue)
                        return results;

                    results.Add((int) result);

                    if (count == results.Count)
                        return results;
                }
            }
        }
    }
}