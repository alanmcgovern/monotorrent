using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Tracker
{
    public class ByteComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] first, byte[] second)
        {
            if (first == null) return second == null;
            if (second == null) return first == null;

            if (first.Length != second.Length)
                return false;

            // The majority of what we'll be comparing are 20 byte arrays, so i believe
            // there'll be faster performance if i unroll the loop. I'll need to benchmark
            // that though
            if (first.Length == 20)
            {
                return first[0] == second[0]
                    && first[1] == second[1]
                    && first[2] == second[2]
                    && first[3] == second[3]
                    && first[4] == second[4]
                    && first[5] == second[5]
                    && first[6] == second[6]
                    && first[7] == second[7]
                    && first[8] == second[8]
                    && first[9] == second[9]
                    && first[10] == second[10]
                    && first[11] == second[11]
                    && first[12] == second[12]
                    && first[13] == second[13]
                    && first[14] == second[14]
                    && first[15] == second[15]
                    && first[16] == second[16]
                    && first[17] == second[17]
                    && first[18] == second[18]
                    && first[19] == second[19];
            }
            else
            {
                for (int i = 0; i < first.Length; i++)
                    if (first[i] != second[i])
                        return false;
            }
            return true;
        }

        public int GetHashCode(byte[] array)
        {
            int result = 0;

            if (array == null)
                return result;

            // Equality is based generally on checking 20 positions, checking 4 should be enough
            // for the hashcode
            if (array.Length > 3)
            {
                result ^= array[0];
                result ^= array[1] << 8;
                result ^= array[2] << 16;
                result ^= array[3] << 24;
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                    result ^= array[i] << i;
            }

            return result;
        }
    }
}
