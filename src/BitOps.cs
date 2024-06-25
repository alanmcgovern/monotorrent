//
// BitOps.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2024 Alan McGovern
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
using System.Runtime.CompilerServices;

namespace System.Numerics
{
    // Provides some key bit operations for older .NET implementations.
    // Currently provides implementations of integer based Log and Power operations.
    static class BitOps
    {
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static uint RoundUpToPowerOf2 (uint value)
        {
#if NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP3_0 || NET5_0
            --value;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
#else
            return BitOperations.RoundUpToPowerOf2 (value);
#endif
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static int FloorLog2 (uint value)
        {
#if NETSTANDARD2_0 || NETSTANDARD2_1
            Span<uint> b = stackalloc uint[] { 0x2, 0xC, 0xF0, 0xFF00, 0xFFFF0000 };
            Span<uint> S = stackalloc uint[] { 1, 2, 4, 8, 16 };

            uint result = 0; // result of log2(v) will go here
            for (int i = 4; i >= 0; i--) // unroll for speed...
            {
                if ((value & b[i]) != 0) {
                    value >>= (int)S[i];
                    result |= S[i];
                }
            }
            return (int) result;
#else
            return BitOperations.Log2 (value);
#endif
        }

        public static int FloorLog2 (ulong value)
        {
            if (value > UInt32.MaxValue)
                return 32 + FloorLog2 (value >> 32);
            return FloorLog2 (value >> 32);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static int CeilLog2 (uint value)
            => FloorLog2 (BitOps.RoundUpToPowerOf2 (value));

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static int CeilLog2 (ulong value)
        {
            if (value > UInt32.MaxValue)
                return 32 + CeilLog2 (value >> 32);

            return CeilLog2 ((uint) value);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static int CeilLog10 (ulong v)
        {
            Span<ulong> PowersOf10 = stackalloc ulong[] {
                1ul,
                10ul,
                100ul,
                1000ul,
                10000ul,
                100000ul,
                1000000ul,
                10000000ul,
                100000000ul,
                1000000000ul,
                10000000000ul,
                100000000000ul,
                1000000000000ul,
                10000000000000ul,
                100000000000000ul,
                1000000000000000ul,
                10000000000000000ul,
                100000000000000000ul,
                1000000000000000000ul,
                10000000000000000000ul};

            var tmp = ((CeilLog2 (v) + 1) * 1233) >> 12; // (use a lg2 method from above)
            return tmp - (v < PowersOf10[tmp] ? 1 : 0);
        }
    }
}
