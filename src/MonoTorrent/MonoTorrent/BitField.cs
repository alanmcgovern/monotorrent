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
    public class BitField : ICloneable, IEnumerable<bool>
    {
        #region Member Variables

        readonly int[] array;

        internal bool AllFalse => TrueCount == 0;

        internal bool AllTrue => TrueCount == Length;

        public int Length { get; }

        public double PercentComplete => (double) TrueCount / Length * 100.0;

        #endregion


        #region Constructors
        public BitField (byte[] array, int length)
            : this (length)
        {
            if (array == null)
                throw new ArgumentNullException (nameof (array));
            if (array.Length < 1)
                throw new ArgumentOutOfRangeException (nameof (array), "Array length must be greater than zero");

            FromArray (array, 0);
        }

        public BitField (int length)
        {
            if (length < 1)
                throw new ArgumentOutOfRangeException (nameof (length), "Length must be greater than zero");

            Length = length;
            array = new int[(length + 31) / 32];
        }

        public BitField (bool[] array)
        {
            if (array == null)
                throw new ArgumentNullException (nameof (array));

            if (array.Length < 1)
                throw new ArgumentOutOfRangeException ("The array must contain at least one element", nameof (array));

            Length = array.Length;
            this.array = new int[(array.Length + 31) / 32];
            for (int i = 0; i < array.Length; i++)
                Set (i, array[i]);
        }

        #endregion


        #region Methods BitArray

        public bool this[int index] {
            get => Get (index);
            internal set => Set (index, value);
        }

        object ICloneable.Clone ()
        {
            return Clone ();
        }

        public BitField Clone ()
        {
            var b = new BitField (Length);
            Buffer.BlockCopy (array, 0, b.array, 0, array.Length * 4);
            b.TrueCount = TrueCount;
            return b;
        }

        public BitField From (BitField value)
        {
            Check (value);
            Buffer.BlockCopy (value.array, 0, array, 0, array.Length * 4);
            TrueCount = value.TrueCount;
            return this;
        }

        public BitField Not ()
        {
            for (int i = 0; i < array.Length; i++)
                array[i] = ~array[i];

            TrueCount = Length - TrueCount;
            return this;
        }

        public BitField And (BitField value)
        {
            Check (value);

            for (int i = 0; i < array.Length; i++)
                array[i] &= value.array[i];

            Validate ();
            return this;
        }

        internal BitField NAnd (BitField value)
        {
            Check (value);

            for (int i = 0; i < array.Length; i++)
                array[i] &= ~value.array[i];

            Validate ();
            return this;
        }

        public BitField Or (BitField value)
        {
            Check (value);

            for (int i = 0; i < array.Length; i++)
                array[i] |= value.array[i];

            Validate ();
            return this;
        }

        public BitField Xor (BitField value)
        {
            Check (value);

            for (int i = 0; i < array.Length; i++)
                array[i] ^= value.array[i];

            Validate ();
            return this;
        }

        public override bool Equals (object obj)
        {
            if (!(obj is BitField bf) || this.array.Length != bf.array.Length || TrueCount != bf.TrueCount)
                return false;

            for (int i = 0; i < array.Length; i++)
                if (array[i] != bf.array[i])
                    return false;

            return true;
        }

        public int FirstTrue ()
        {
            return FirstTrue (0, Length);
        }

        public int FirstTrue (int startIndex, int endIndex)
        {
            int start;
            int end;

            // If the number of pieces is an exact multiple of 32, we need to decrement by 1 so we don't overrun the array
            // For the case when endIndex == 0, we need to ensure we don't go negative
            int loopEnd = Math.Min ((endIndex / 32), array.Length - 1);
            for (int i = (startIndex / 32); i <= loopEnd; i++) {
                if (array[i] == 0)        // This one has no true values
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

        public int FirstFalse ()
        {
            return FirstFalse (0, Length);
        }

        public int FirstFalse (int startIndex, int endIndex)
        {
            int start;
            int end;

            // If the number of pieces is an exact multiple of 32, we need to decrement by 1 so we don't overrun the array
            // For the case when endIndex == 0, we need to ensure we don't go negative
            int loopEnd = Math.Min ((endIndex / 32), array.Length - 1);
            for (int i = (startIndex / 32); i <= loopEnd; i++) {
                if (array[i] == ~0)        // This one has no false values
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
        internal void FromArray (byte[] buffer, int offset)
        {
            int end = Length / 32;
            for (int i = 0; i < end; i++)
                array[i] = (buffer[offset++] << 24) |
                           (buffer[offset++] << 16) |
                           (buffer[offset++] << 8) |
                           (buffer[offset++]);

            int shift = 24;
            for (int i = end * 32; i < Length; i += 8) {
                array[array.Length - 1] |= buffer[offset++] << shift;
                shift -= 8;
            }
            Validate ();
        }

        bool Get (int index)
        {
            if (index < 0 || index >= Length)
                throw new ArgumentOutOfRangeException (nameof (index));

            return (array[index >> 5] & (1 << (31 - (index & 31)))) != 0;
        }

        public IEnumerator<bool> GetEnumerator ()
        {
            for (int i = 0; i < Length; i++)
                yield return Get (i);
        }

        internal int CountTrue (BitField selector)
        {
            if (selector == null)
                throw new ArgumentNullException (nameof (selector));

            if (selector.Length != Length)
                throw new ArgumentException ("The selector should be the same length as this bitfield", nameof (selector));

            uint count = 0;
            for (int i = 0; i < array.Length; i++)
                count += CountBits ((uint) (array[i] & selector.array[i]));
            return (int) count;
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }

        public override int GetHashCode ()
        {
            int count = 0;
            for (int i = 0; i < array.Length; i++)
                count += array[i];

            return count;
        }

        public int LengthInBytes => (Length + 7) / 8;

        public BitField Set (int index, bool value)
        {
            if (index < 0 || index >= Length)
                throw new ArgumentOutOfRangeException (nameof (index));

            if (value) {
                if ((array[index >> 5] & (1 << (31 - (index & 31)))) == 0)// If it's not already true
                    TrueCount++;                                        // Increase true count
                array[index >> 5] |= (1 << (31 - index & 31));
            } else {
                if ((array[index >> 5] & (1 << (31 - (index & 31)))) != 0)// If it's not already false
                    TrueCount--;                                        // Decrease true count
                array[index >> 5] &= ~(1 << (31 - (index & 31)));
            }

            return this;
        }

        internal BitField SetTrue ((int startPiece, int endPiece) range)
        {
            for (int i = range.startPiece; i <= range.endPiece; i++)
                Set (i, true);
            return this;
        }

        internal BitField SetTrue (params int[] indices)
        {
            foreach (int index in indices)
                Set (index, true);
            return this;
        }

        internal BitField SetFalse (params int[] indices)
        {
            foreach (int index in indices)
                Set (index, false);
            return this;
        }

        internal BitField SetAll (bool value)
        {
            if (value) {
                for (int i = 0; i < array.Length; i++)
                    array[i] = ~0;
                Validate ();
            } else {
                for (int i = 0; i < array.Length; i++)
                    array[i] = 0;
                TrueCount = 0;
            }

            return this;
        }

        internal byte[] ToByteArray ()
        {
            byte[] data = new byte[LengthInBytes];
            ToByteArray (data, 0);
            return data;
        }

        internal void ToByteArray (byte[] buffer, int offset)
        {
            if (buffer == null)
                throw new ArgumentNullException (nameof (buffer));

            ZeroUnusedBits ();
            int end = Length / 32;
            for (int i = 0; i < end; i++) {
                buffer[offset++] = (byte) (array[i] >> 24);
                buffer[offset++] = (byte) (array[i] >> 16);
                buffer[offset++] = (byte) (array[i] >> 8);
                buffer[offset++] = (byte) (array[i] >> 0);
            }

            int shift = 24;
            for (int i = end * 32; i < Length; i += 8) {
                buffer[offset++] = (byte) (array[array.Length - 1] >> shift);
                shift -= 8;
            }
        }

        [ExcludeFromCodeCoverage]
        string ToDebuggerString ()
        {
            var sb = new StringBuilder (array.Length * 16);
            for (int i = 0; i < Length; i++) {
                sb.Append (Get (i) ? 'T' : 'F');
                sb.Append (' ');
            }

            return sb.ToString (0, sb.Length - 1);
        }

        public int TrueCount { get; set; }

        void Validate ()
        {
            ZeroUnusedBits ();

            // Update the population count
            uint count = 0;
            for (int i = 0; i < array.Length; i++)
                count += CountBits ((uint) array[i]);
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
                array[array.Length - 1] &= (-1 << shift);
        }

        void Check (BitField value)
        {
            MonoTorrent.Check.Value (value);
            if (Length != value.Length)
                throw new ArgumentException ("BitFields are of different lengths", nameof (value));
        }

        #endregion
    }
}
