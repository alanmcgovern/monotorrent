//
// ReadOnlyBitField.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MonoTorrent
{
    public class ReadOnlyBitField
    {
        internal static ReadOnlyBitField From (BitFieldData data)
            => new ReadOnlyBitField (data);

        BitFieldData Data { get; }

        public bool AllFalse => Data.AllFalse;

        public bool AllTrue => Data.AllTrue;

        public int Length => Data.Length;

        public int LengthInBytes => Data.LengthInBytes;

        public double PercentComplete => Data.PercentComplete;

        internal ReadOnlySpan<ulong> Span => Data.Data;

        public int TrueCount => Data.TrueCount;

        public bool this[int index] {
            get => Data[index];
        }

        public ReadOnlyBitField (ReadOnlyBitField other)
            => Data = new BitFieldData (other?.Data ?? throw new ArgumentNullException (nameof (other)));

        public ReadOnlyBitField (ReadOnlySpan<byte> array, int length)
            => Data = new BitFieldData (array, length);

        public ReadOnlyBitField (int length)
            => Data = new BitFieldData (length);

        public ReadOnlyBitField (bool[] array)
            => Data = new BitFieldData (array);

        ReadOnlyBitField (BitFieldData data)
            => Data = data;

        public bool SequenceEqual (ReadOnlyBitField? other)
            => other != null && other.TrueCount == TrueCount && other.Span.SequenceEqual (Span);

        public int CountTrue (ReadOnlyBitField selector)
            => Data.CountTrue (selector);

        /// <summary>
        /// Returns the index of the first <see langword="false" /> bit in the bitfield.
        /// Returns -1 if no <see langword="false" /> bit is found. />
        /// </summary>
        /// <returns></returns>
        public int FirstFalse ()
            => Data.FirstFalse ();

        /// <summary>
        /// Returns the index of the first <see langword="false" /> bit between <paramref name="startIndex"/> and <paramref name="endIndex"/> (inclusive).
        /// Returns -1 if no <see langword="false" /> bit is found. />
        /// </summary>
        /// <param name="startIndex">The first index to check</param>
        /// <param name="endIndex">The last index to check</param>
        /// <returns></returns>
        public int FirstFalse (int startIndex, int endIndex)
            => Data.FirstFalse (startIndex, endIndex);

        /// <summary>
        /// Returns the index of the first <see langword="true" /> bit in the bitfield.
        /// Returns -1 if no <see langword="true" /> bit is found. />
        /// </summary>
        /// <returns></returns>
        public int FirstTrue ()
            => Data.FirstTrue ();

        /// <summary>
        /// Returns the index of the first <see langword="true" /> bit between <paramref name="startIndex"/> and <paramref name="endIndex"/> (inclusive).
        /// Returns -1 if no <see langword="true" /> bit is found. />
        /// </summary>
        /// <param name="startIndex">The first index to check</param>
        /// <param name="endIndex">The last index to check</param>
        /// <returns></returns>
        public int FirstTrue (int startIndex, int endIndex)
            => Data.FirstTrue (startIndex, endIndex);

        public byte[] ToBytes ()
            => Data.ToBytes ();

        public int ToBytes (Span<byte> buffer)
            => Data.ToBytes (buffer);
    }
}
