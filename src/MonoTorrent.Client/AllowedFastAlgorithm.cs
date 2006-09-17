
using System;
using System.Collections.Generic;
using MonoTorrent.Client.Encryption;
using System.Net;
using System.Security.Cryptography;

namespace MonoTorrent.Client
{
	/// <summary>
	/// Description of AllowedFastAlgorithm.
	/// </summary>
	public class AllowedFastAlgorithm
    {
        public const int AllowedFastPieceCount = 10;
        private static SHA1Managed hasher = new SHA1Managed();

		public static UInt32[] Calculate(byte[] addressBytes, byte[] infohash, int numberOfResults, UInt32 numberOfPieces)
		{
            byte[] hashBuffer = new byte[24];   // The hash buffer to be used in hashing
            uint[] results = new uint[numberOfResults];   // The results array which will be returned

            // 1) Convert the bytes into an int32 and make them Network order
            int ip = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(addressBytes, 0));
            // 2) binary AND this value with 0xFFFFFF00 to select the three most sigificant bytes
            int ipMostSignificant = (int)(0xFFFFFF00 & ip);

            // 3) Make ipMostSignificant into NetworkOrder
            UInt32 ip2 = (UInt32)IPAddress.HostToNetworkOrder(ipMostSignificant);

            // 4) Copy ip2 into the hashBuffer
            Buffer.BlockCopy(BitConverter.GetBytes(ip2), 0, hashBuffer, 0, 4);

            // 5) Copy the infohash into the hashbuffer
            Buffer.BlockCopy(infohash, 0, hashBuffer, 4, 20);

            // 6) Keep hashing and cycling until we have AllowedFastPieceCount number of results
            // Then return that result
            int j = 0;
            while (true)
            {
                lock(hasher)
                    hashBuffer = hasher.ComputeHash(hashBuffer);

                for (int i = 0; i < 20; i += 4)
                {
                    UInt32 result = (UInt32)IPAddress.HostToNetworkOrder(BitConverter.ToInt32(hashBuffer, i));
                    results[j++] = result % numberOfPieces;
                    if (j == results.Length)
                        return results;
                }
            }

		}
	}
}
