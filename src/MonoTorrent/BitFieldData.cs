//
// BitField.cs
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
using System.Text;

namespace MonoTorrent
{
    /// <summary>
    /// This class is for represting the Peer's bitfield
    /// </summary>
    [DebuggerDisplay ("{" + nameof (ToDebuggerString) + " ()}")]
    class BitFieldData : IEnumerable<bool>
    {
        public uint[] Data { get; }
        public int Length { get; }
        public int TrueCount { get; set; }

        Span<uint> Span => Data;

        public bool AllFalse => TrueCount == 0;

        public bool AllTrue => TrueCount == Length;

        public int LengthInBytes => (Length + 7) / 8;

        public double PercentComplete => (double) TrueCount / Length * 100.0;

        internal void From (ReadOnlySpan<byte> buffer)
        {
            int end = Length / 32;
            for (int i = 0; i < end; i++) {
                Data[i] = BinaryPrimitives.ReadUInt32BigEndian (buffer);
                buffer = buffer.Slice (4);
            }

            int shift = 24;
            for (int i = end * 32; i < Length; i += 8) {
                Data[Data.Length - 1] |= (uint) buffer[0] << shift;
                buffer = buffer.Slice (1);
                shift -= 8;
            }
            ZeroUnusedBits ();

            int count = 0;
            for (int i = 0; i < Data.Length; i++)
                count += CountBits (Data[i]);
            TrueCount = count;
        }

        internal void ZeroUnusedBits ()
        {
            int shift = Length % 32;
            if (shift != 0)
                Data[Data.Length - 1] &= uint.MaxValue << (32 - shift);
        }


        public BitFieldData (int length)
        {
            if (length < 1)
                throw new ArgumentOutOfRangeException (nameof (length), "Length must be greater than zero");

            Data = new uint[(length + 31) / 32];
            Length = length;
        }

        public BitFieldData (BitFieldData other)
        {
            if (other == null)
                throw new ArgumentNullException (nameof (other));

            Data = (uint[]) other.Data.Clone ();
            Length = other.Length;
            TrueCount = other.TrueCount;
        }

        public BitFieldData (ReadOnlySpan<byte> buffer, int length)
            : this (length)
        {
            if ((length + 31) / 32 > buffer.Length)
                throw new ArgumentOutOfRangeException ("The buffer was too small");
            From (buffer);
        }

        public BitFieldData (bool[] array)
            : this (array?.Length ?? throw new ArgumentNullException (nameof (array)))
        {
            for (int i = 0; i < array.Length; i++)
                this[i] = array[i];
        }

        public bool this[int index] {
            get {
                if (index < 0 || index >= Length)
                    throw new ArgumentOutOfRangeException (nameof (index));
                return TrueCount == Length || Get (index);
            }
            set {
                if (index < 0 || index >= Length)
                    throw new ArgumentOutOfRangeException (nameof (index));


                if (value) {
                    if ((Span[index >> 5] & (1 << (31 - (index & 31)))) == 0)// If it's not already true
                        TrueCount++;                                        // Increase true count
                    Span[index >> 5] |= ((uint) 1 << (31 - index & 31));
                } else {
                    if ((Span[index >> 5] & (1 << (31 - (index & 31)))) != 0)// If it's not already false
                        TrueCount--;                                        // Decrease true count
                    Span[index >> 5] &= ~((uint) 1 << (31 - (index & 31)));
                }
            }
        }

        public override bool Equals (object? obj)
            => obj is BitFieldData other
            && TrueCount == other.TrueCount
            && Span.SequenceEqual (other.Span);

        /// <summary>
        /// Returns the index of the first <see langword="true" /> bit in the bitfield.
        /// Returns -1 if no <see langword="true" /> bit is found. />
        /// </summary>
        /// <returns></returns>
        public int FirstTrue ()
            => FirstTrue (0, Length - 1);

        /// <summary>
        /// Returns the index of the first <see langword="true" /> bit between <paramref name="startIndex"/> and <paramref name="endIndex"/> (inclusive).
        /// Returns -1 if no <see langword="true" /> bit is found. />
        /// </summary>
        /// <param name="startIndex">The first index to check</param>
        /// <param name="endIndex">The last index to check</param>
        /// <returns></returns>
        public int FirstTrue (int startIndex, int endIndex)
        {
            if (startIndex < 0 || startIndex >= Length)
                throw new IndexOutOfRangeException (nameof (startIndex));
            if (endIndex < 0 || endIndex >= Length)
                throw new IndexOutOfRangeException (nameof (endIndex));

            if (AllTrue)
                return startIndex;
            if (AllFalse)
                return -1;

            int start;
            int end;

            // If the number of pieces is an exact multiple of 32, we need to decrement by 1 so we don't overrun the array
            // For the case when endIndex == 0, we need to ensure we don't go negative
            int loopEnd = Math.Min ((endIndex / 32), Span.Length - 1);
            for (int i = (startIndex / 32); i <= loopEnd; i++) {
                if (Span[i] == 0)        // This one has no true values
                    continue;

                start = i * 32;
                end = start + 31;

#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET472
                start = (start < startIndex) ? startIndex : start;
                end = (end > Length) ? Length : end;
                end = (end > endIndex) ? endIndex : end;
                if (end == Length && end > 0)
                    end--;

                for (int j = start; j <= end; j++)
                    if (Get (j))     // This piece is true
                        return j;
#else
                var mask = uint.MaxValue;
                if (start < startIndex)
                    mask &= uint.MaxValue >> (startIndex - start);
                if (end > endIndex)
                    mask &= uint.MaxValue << (end - endIndex);
                mask &= Span[i];
                if (mask == 0)
                    continue;
                return System.Numerics.BitOperations.LeadingZeroCount (mask) + start;
#endif
            }

            return -1;              // Nothing is true
        }

        internal void From (ReadOnlyBitField value)
        {
            if (value is null)
                throw new ArgumentNullException (nameof (value));

            if (Length != value.Length)
                throw new ArgumentException ("BitFields are of different lengths", nameof (value));

            value.Span.CopyTo (Span);
            TrueCount = value.TrueCount;
        }

        /// <summary>
        /// Returns the index of the first <see langword="false" /> bit in the bitfield.
        /// Returns -1 if no <see langword="false" /> bit is found. />
        /// </summary>
        /// <returns></returns>
        public int FirstFalse ()
            => FirstFalse (0, Length - 1);

        /// <summary>
        /// Returns the index of the first <see langword="false" /> bit between <paramref name="startIndex"/> and <paramref name="endIndex"/> (inclusive).
        /// Returns -1 if no <see langword="false" /> bit is found. />
        /// </summary>
        /// <param name="startIndex">The first index to check</param>
        /// <param name="endIndex">The last index to check</param>
        /// <returns></returns>
        public int FirstFalse (int startIndex, int endIndex)
        {
            if (startIndex < 0 || startIndex >= Length)
                throw new IndexOutOfRangeException (nameof (startIndex));
            if (endIndex < 0 || endIndex >= Length)
                throw new IndexOutOfRangeException (nameof (endIndex));

            int start;
            int end;

            if (AllTrue)
                return -1;
            if (AllFalse)
                return 0;

            // If the number of pieces is an exact multiple of 32, we need to decrement by 1 so we don't overrun the array
            // For the case when endIndex == 0, we need to ensure we don't go negative
            int loopEnd = Math.Min ((endIndex / 32), Span.Length - 1);
            for (int i = (startIndex / 32); i <= loopEnd; i++) {
                if (Span[i] == uint.MaxValue)        // This one has no false values
                    continue;

                start = i * 32;
                end = start + 31;

#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET472
                start = (start < startIndex) ? startIndex : start;
                end = (end > Length) ? Length : end;
                end = (end > endIndex) ? endIndex : end;
                if (end == Length && end > 0)
                    end--;

                for (int j = start; j <= end; j++)
                    if (!Get (j))     // This piece is false
                        return j;
#else
                var mask = uint.MaxValue;
                if (start < startIndex)
                    mask &= uint.MaxValue >> (startIndex - start);
                if (end > endIndex)
                    mask &= uint.MaxValue << (end - endIndex);
                mask &= ~Span[i];
                if (mask == 0)
                    continue;
                return System.Numerics.BitOperations.LeadingZeroCount (mask) + start;
#endif
            }

            return -1;              // Nothing is true
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        bool Get (int index)
            => (Span[index >> 5] & (1 << (31 - (index & 31)))) != 0;

        public IEnumerator<bool> GetEnumerator ()
        {
            for (int i = 0; i < Length; i++)
                yield return Get (i);
        }

        public int CountTrue (ReadOnlyBitField selector)
        {
            if (selector == null)
                throw new ArgumentNullException (nameof (selector));

            if (selector.Length != Length)
                throw new ArgumentException ("The selector should be the same length as this bitfield", nameof (selector));

            int count = 0;
            var data = Span;
            var selectorData = selector.Span;
            for (int i = 0; i < data.Length && i < selectorData.Length; i++)
                count += CountBits (data[i] & selectorData[i]);
            return count;
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }

        public override int GetHashCode ()
        {
            uint count = 0;
            for (int i = 0; i < Span.Length; i++)
                count += Span[i];

            return (int) count;
        }

        public byte[] ToBytes ()
        {
            byte[] data = new byte[LengthInBytes];
            ToBytes (data);
            return data;
        }

        public int ToBytes (Span<byte> buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException (nameof (buffer));
            if (buffer.Length < LengthInBytes)
                throw new ArgumentOutOfRangeException ($"The buffer must be able to store at least {LengthInBytes} bytes");


            int end = Length / 32;
            int offset = 0;
            for (int i = 0; i < end; i++) {
                buffer[offset++] = (byte) (Span[i] >> 24);
                buffer[offset++] = (byte) (Span[i] >> 16);
                buffer[offset++] = (byte) (Span[i] >> 8);
                buffer[offset++] = (byte) (Span[i] >> 0);
            }

            int shift = 24;
            for (int i = end * 32; i < Length; i += 8) {
                buffer[offset++] = (byte) (Span[Span.Length - 1] >> shift);
                shift -= 8;
            }

            return offset;
        }

        [ExcludeFromCodeCoverage]
        string ToDebuggerString ()
        {
            var sb = new StringBuilder (Span.Length * 16);
            for (int i = 0; i < Length; i++) {
                sb.Append (Get (i) ? 'T' : 'F');
                sb.Append (' ');
            }

            return sb.ToString (0, sb.Length - 1);
        }

        public void SetTrue ((int startPiece, int endPiece) range)
        {
            for (int i = range.startPiece; i <= range.endPiece; i++)
                this[i] = true;
        }

        public void SetTrue (params int[] indices)
        {
            foreach (int index in indices)
                this[index] = true;
        }

        public void SetFalse (params int[] indices)
        {
            foreach (int index in indices)
                this[index] = false;
        }

        public void SetAll (bool value)
        {
            if ((TrueCount == Length && value) || (!value && TrueCount == 0))
                return;

            if (value) {
                Span.Fill (uint.MaxValue);
                ZeroUnusedBits ();
                TrueCount = Length;
            } else {
                Span.Fill (0);
                TrueCount = 0;
            }
        }

        public void Not ()
        {
            var data = Span;
            for (int i = 0; i < data.Length; i++)
                data[i] = ~data[i];

            ZeroUnusedBits ();
            TrueCount = Length - TrueCount;
        }

        public void And (ReadOnlyBitField value)
        {
            Check (value);

            var data = Span;
            var valueData = value.Span;
            int count = 0;
            for (int i = 0; i < data.Length && i < valueData.Length; i++) {
                var result = data[i] & valueData[i];
                count += CountBits (result);
                data[i] = result;
            }
            TrueCount = count;
        }

        public void NAnd (ReadOnlyBitField value)
        {
            Check (value);
            if (AllFalse || value.AllFalse)
                return;

            if (value.AllTrue) {
                SetAll (false);
            } else {
                int count = 0;
                var data = Span;
                var valueData = value.Span;
                for (int i = 0; i < data.Length && i < valueData.Length; i++) {
                    var result = data[i] & (~valueData[i]);
                    count += CountBits (result);
                    data[i] = result;
                }
                TrueCount = count;
            }
        }

        public void Or (ReadOnlyBitField value)
        {
            Check (value);

            int count = 0;
            var data = Span;
            var valueData = value.Span;
            for (int i = 0; i < data.Length && i < valueData.Length; i++) {
                var result = data[i] | valueData[i];
                count += CountBits (result);
                data[i] = result;
            }
            TrueCount = count;
        }

        public void Xor (ReadOnlyBitField value)
        {
            Check (value);

            int count = 0;
            var data = Span;
            var valueData = value.Span;
            for (int i = 0; i < data.Length && i < valueData.Length; i++) {
                var result = data[i] ^ valueData[i];
                count += CountBits (result);
                data[i] = result;
            }
            TrueCount = count;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        private protected static int CountBits (uint v)
        {
#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET472
            v -= (v >> 1) & 0x55555555;
            v = (v & 0x33333333) + ((v >> 2) & 0x33333333);
            return (int) (((v + (v >> 4) & 0xF0F0F0F) * 0x1010101) >> 24);
#else
            return System.Numerics.BitOperations.PopCount (v);
#endif
        }

        void Check (ReadOnlyBitField value)
        {
            if (value is null)
                throw new ArgumentNullException (nameof (value));

            if (Length != value.Length)
                throw new ArgumentException ("BitFields are of different lengths", nameof (value));
        }

        void Check (BitFieldData value)
        {
            if (value is null)
                throw new ArgumentNullException (nameof (value));

            if (Length != value.Length)
                throw new ArgumentException ("BitFields are of different lengths", nameof (value));
        }
    }
}
