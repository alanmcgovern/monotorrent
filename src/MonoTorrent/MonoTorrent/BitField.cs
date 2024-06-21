//
// BitField.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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
using System.Diagnostics.CodeAnalysis;

namespace MonoTorrent
{
    public sealed class BitField
    {
        ReadOnlyBitField? readOnlyWrapper;
        BitFieldData Data { get; }

        public bool AllFalse => Data.AllFalse;

        public bool AllTrue => Data.AllTrue;

        public int Length => Data.Length;

        public int LengthInBytes => Data.LengthInBytes;

        public double PercentComplete => Data.PercentComplete;

        internal Span<ulong> Span => Data.Data;

        public int TrueCount => Data.TrueCount;

        public bool this[int index] {
            get => Data[index];
            set => Data[index] = value;
        }

        ReadOnlyBitField? ReadOnlyWrapper => (readOnlyWrapper ??= ReadOnlyBitField.From (Data));

        public BitField (ReadOnlyBitField other)
        {
            Data = new BitFieldData (other.Length);
            Data.TrueCount = other.TrueCount;
            other.Span.CopyTo (Span);
        }

        public BitField (ReadOnlySpan<byte> array, int length)
            => Data = new BitFieldData (array, length);

        public BitField (int length)
            => Data = new BitFieldData (length);

        public BitField (bool[] array)
            => Data = new BitFieldData (array);

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

        public BitField And (ReadOnlyBitField value)
        {
            Data.And (value);
            return this;
        }

        public BitField From (ReadOnlySpan<byte> buffer)
        {
            Data.From (buffer);
            return this;
        }

        public BitField From (ReadOnlyBitField value)
        {
            Data.From (value);
            return this;
        }

        public BitField NAnd (ReadOnlyBitField value)
        {
            Data.NAnd (value);
            return this;
        }

        public BitField Not ()
        {
            Data.Not ();
            return this;
        }

        public BitField Or (ReadOnlyBitField value)
        {
            Data.Or (value);
            return this;
        }

        public BitField Xor (ReadOnlyBitField value)
        {
            Data.Xor (value);
            return this;
        }

        public BitField Set (int index, bool value)
        {
            Data[index] = value;
            return this;
        }

        public BitField SetAll (bool value)
        {
            Data.SetAll (value);
            return this;
        }

        public BitField SetFalse (params int[] indices)
        {
            Data.SetFalse (indices);
            return this;
        }

        public BitField SetTrue ((int startPiece, int endPiece) range)
        {
            Data.SetTrue (range);
            return this;
        }

        public BitField SetTrue (params int[] indices)
        {
            Data.SetTrue (indices);
            return this;
        }

        [return: NotNullIfNotNull ("bitfield")]
        public static implicit operator ReadOnlyBitField? (BitField? bitfield)
            => bitfield?.ReadOnlyWrapper;
    }
}
