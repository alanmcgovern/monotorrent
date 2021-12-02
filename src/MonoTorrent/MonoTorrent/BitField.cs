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
using System.Runtime.InteropServices;
using System.Text;

namespace MonoTorrent
{
    /// <summary>
    /// This class is for represting the Peer's bitfield
    /// </summary>
    [DebuggerDisplay ("{" + nameof (ToDebuggerString) + " ()}")]
    public class BitField : IEnumerable<bool>
    {
        private protected readonly int[] Data;

        public int Length { get; }

        public int TrueCount { get; private set; }

        public bool AllFalse => TrueCount == 0;

        public bool AllTrue => TrueCount == Length;

        public int LengthInBytes => (Length + 7) / 8;

        public double PercentComplete => (double) TrueCount / Length * 100.0;


        #region Constructors
        public BitField (BitField other)
        {
            if (other == null)
                throw new ArgumentNullException (nameof (other));

            Data = new int[other.Data.Length];
            Length = other.Length;

            From (other);
        }

        public BitField (ReadOnlySpan<byte> buffer, int length)
            : this (length)
        {
            if ((length + 31) / 32 > buffer.Length)
                throw new ArgumentOutOfRangeException ("The buffer was too small");
            From (buffer);
        }

        public BitField (int length)
        {
            if (length < 1)
                throw new ArgumentOutOfRangeException (nameof (length), "Length must be greater than zero");

            Length = length;
            Data = new int[(length + 31) / 32];
        }

        public BitField (bool[] array)
        {
            if (array == null)
                throw new ArgumentNullException (nameof (array));

            if (array.Length < 1)
                throw new ArgumentOutOfRangeException ("The array must contain at least one element", nameof (array));

            Length = array.Length;
            this.Data = new int[(array.Length + 31) / 32];
            for (int i = 0; i < array.Length; i++)
                Set (i, array[i]);
        }

        #endregion


        #region Methods BitArray

        public bool this[int index] {
            get {
                if (index < 0 || index >= Length)
                    throw new ArgumentOutOfRangeException (nameof (index));
                return TrueCount == Length || Get (index);
            }
            private protected set => Set (index, value);
        }

        internal BitField From (ReadOnlySpan<byte> buffer)
        {
            int end = Length / 32;
            for (int i = 0; i < end; i++) {
                Data[i] = BinaryPrimitives.ReadInt32BigEndian (buffer);
                buffer = buffer.Slice (4);
            }

            int shift = 24;
            for (int i = end * 32; i < Length; i += 8) {
                Data[Data.Length - 1] |= buffer[0] << shift;
                buffer = buffer.Slice (1);
                shift -= 8;
            }
            Validate ();
            return this;
        }

        private protected BitField From (BitField value)
        {
            Check (value);
            Buffer.BlockCopy (value.Data, 0, Data, 0, Data.Length * 4);
            TrueCount = value.TrueCount;
            return this;
        }

        private protected BitField Not ()
        {
            for (int i = 0; i < Data.Length; i++)
                Data[i] = ~Data[i];

            TrueCount = Length - TrueCount;
            return this;
        }

        private protected BitField And (BitField value)
        {
            Check (value);

            for (int i = 0; i < Data.Length; i++)
                Data[i] &= value.Data[i];

            Validate ();
            return this;
        }

        private protected BitField NAnd (BitField value)
        {
            Check (value);

            for (int i = 0; i < Data.Length; i++)
                Data[i] &= ~value.Data[i];

            Validate ();
            return this;
        }

        private protected BitField Or (BitField value)
        {
            Check (value);

            for (int i = 0; i < Data.Length; i++)
                Data[i] |= value.Data[i];

            Validate ();
            return this;
        }

        private protected BitField Xor (BitField value)
        {
            Check (value);

            for (int i = 0; i < Data.Length; i++)
                Data[i] ^= value.Data[i];

            Validate ();
            return this;
        }

        public override bool Equals (object obj)
            => obj is BitField other
            && TrueCount == other.TrueCount
            && Data.AsSpan ().SequenceEqual (other.Data);

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
            int loopEnd = Math.Min ((endIndex / 32), Data.Length - 1);
            for (int i = (startIndex / 32); i <= loopEnd; i++) {
                if (Data[i] == 0)        // This one has no true values
                    continue;

                start = i * 32;
                end = start + 32;
                start = (start < startIndex) ? startIndex : start;
                end = (end > Length) ? Length : end;
                end = (end > endIndex) ? endIndex : end;
                if (end == Length && end > 0)
                    end--;

                for (int j = start; j <= end; j++)
                    if (Get (j))     // This piece is true
                        return j;
            }

            return -1;              // Nothing is true
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
            int loopEnd = Math.Min ((endIndex / 32), Data.Length - 1);
            for (int i = (startIndex / 32); i <= loopEnd; i++) {
                if (Data[i] == ~0)        // This one has no false values
                    continue;

                start = i * 32;
                end = start + 32;
                start = (start < startIndex) ? startIndex : start;
                end = (end > Length) ? Length : end;
                end = (end > endIndex) ? endIndex : end;
                if (end == Length && end > 0)
                    end--;

                for (int j = start; j <= end; j++)
                    if (!Get (j))     // This piece is true
                        return j;
            }

            return -1;              // Nothing is true
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        bool Get (int index)
            => (Data[index >> 5] & (1 << (31 - (index & 31)))) != 0;

        public IEnumerator<bool> GetEnumerator ()
        {
            for (int i = 0; i < Length; i++)
                yield return Get (i);
        }

        public int CountTrue (BitField selector)
        {
            if (selector == null)
                throw new ArgumentNullException (nameof (selector));

            if (selector.Length != Length)
                throw new ArgumentException ("The selector should be the same length as this bitfield", nameof (selector));

            uint count = 0;
            for (int i = 0; i < Data.Length; i++)
                count += CountBits ((uint) (Data[i] & selector.Data[i]));
            return (int) count;
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }

        public override int GetHashCode ()
        {
            int count = 0;
            for (int i = 0; i < Data.Length; i++)
                count += Data[i];

            return count;
        }

        private protected BitField Set (int index, bool value)
        {
            if (index < 0 || index >= Length)
                throw new ArgumentOutOfRangeException (nameof (index));

            if (value) {
                if ((Data[index >> 5] & (1 << (31 - (index & 31)))) == 0)// If it's not already true
                    TrueCount++;                                        // Increase true count
                Data[index >> 5] |= (1 << (31 - index & 31));
            } else {
                if ((Data[index >> 5] & (1 << (31 - (index & 31)))) != 0)// If it's not already false
                    TrueCount--;                                        // Decrease true count
                Data[index >> 5] &= ~(1 << (31 - (index & 31)));
            }

            return this;
        }

        private protected BitField SetTrue ((int startPiece, int endPiece) range)
        {
            for (int i = range.startPiece; i <= range.endPiece; i++)
                Set (i, true);
            return this;
        }

        private protected BitField SetTrue (params int[] indices)
        {
            foreach (int index in indices)
                Set (index, true);
            return this;
        }

        private protected BitField SetFalse (params int[] indices)
        {
            foreach (int index in indices)
                Set (index, false);
            return this;
        }

        private protected BitField SetAll (bool value)
        {
            if ((TrueCount == Length && value) || (!value && TrueCount == 0))
                return this;

            if (value) {
                for (int i = 0; i < Data.Length; i++)
                    Data[i] = ~0;
                Validate ();
            } else {
                for (int i = 0; i < Data.Length; i++)
                    Data[i] = 0;
                TrueCount = 0;
            }

            return this;
        }

        public byte[] ToByteArray ()
        {
            byte[] data = new byte[LengthInBytes];
            ToBytes (data);
            return data;
        }

        public void ToBytes (Span<byte> buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException (nameof (buffer));

            ZeroUnusedBits ();
            int end = Length / 32;
            int offset = 0;
            for (int i = 0; i < end; i++) {
                buffer[offset++] = (byte) (Data[i] >> 24);
                buffer[offset++] = (byte) (Data[i] >> 16);
                buffer[offset++] = (byte) (Data[i] >> 8);
                buffer[offset++] = (byte) (Data[i] >> 0);
            }

            int shift = 24;
            for (int i = end * 32; i < Length; i += 8) {
                buffer[offset++] = (byte) (Data[Data.Length - 1] >> shift);
                shift -= 8;
            }
        }

        [ExcludeFromCodeCoverage]
        string ToDebuggerString ()
        {
            var sb = new StringBuilder (Data.Length * 16);
            for (int i = 0; i < Length; i++) {
                sb.Append (Get (i) ? 'T' : 'F');
                sb.Append (' ');
            }

            return sb.ToString (0, sb.Length - 1);
        }

        private protected void Validate ()
        {
            ZeroUnusedBits ();

            // Update the population count
            uint count = 0;
            for (int i = 0; i < Data.Length; i++)
                count += CountBits ((uint) Data[i]);
            TrueCount = (int) count;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        static uint CountBits (uint v)
        {
            v -= (v >> 1) & 0x55555555;
            v = (v & 0x33333333) + ((v >> 2) & 0x33333333);
            return (((v + (v >> 4) & 0xF0F0F0F) * 0x1010101)) >> 24;
        }

        void ZeroUnusedBits ()
        {
            int shift = 32 - Length % 32;
            if (shift != 0)
                Data[Data.Length - 1] &= (-1 << shift);
        }

        void Check (BitField value)
        {
            if (value is null)
                throw new ArgumentNullException (nameof (value));

            if (Length != value.Length)
                throw new ArgumentException ("BitFields are of different lengths", nameof (value));
        }

        #endregion
    }
}
