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
using System.Text;
using MonoTorrent.Client;

namespace MonoTorrent.Common
{
    /// <summary>
    /// This class is for represting the Peer's bitfield
    /// </summary>
    public class BitField : ICloneable, IEnumerable<bool>
    {
        #region Member Variables

        private int[] array;
        private int length;
        private int trueCount;

        internal bool AllFalse
        {
            get { return this.trueCount == 0; }
        }

        internal bool AllTrue
        {
            get { return this.trueCount == this.length; }
        }

        public int Length
        {
            get { return this.length; }
        }

        public double PercentComplete
        {
            get { return (double)this.trueCount / this.length * 100.0; }
        }

        #endregion


        #region Constructors
        public BitField(byte[] array, int length)
            : this(length)
        {
            FromArray(array, 0, array.Length);
        }

        public BitField(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException("length");

            this.length = length;
            this.array = new int[(length + 31) / 32];
        }

        public BitField(bool[] array)
        {
            this.length = array.Length;
            this.array = new int[(array.Length + 31) / 32];
            for (int i = 0; i < array.Length; i++)
                Set(i, array[i]);
        }

        #endregion


        #region Methods BitArray

        public bool this[int index]
        {
            get { return this.Get(index); }
            internal set { this.Set(index, value); }
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        public BitField Clone()
        {
            BitField b = new BitField(this.length);
            Buffer.BlockCopy(array, 0, b.array, 0, array.Length * 4);
            b.trueCount = trueCount;
            return b;
        }

        public BitField From(BitField value)
        {
            Check(value);
            Buffer.BlockCopy(value.array, 0, array, 0, array.Length * 4);
            trueCount = value.trueCount;
            return this;
        }

        public BitField Not()
        {
            for (int i = 0; i < this.array.Length; i++)
                this.array[i] = ~this.array[i];

            this.trueCount = this.length - this.trueCount;
            return this;
        }

        public BitField And(BitField value)
        {
            Check(value);

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] &= value.array[i];

            Validate();
            return this;
        }

        internal BitField NAnd(BitField value)
        {
            Check(value);

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] &= ~value.array[i];

            Validate();
            return this;
        }

        public BitField Or(BitField value)
        {
            Check(value);

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] |= value.array[i];

            Validate();
            return this;
        }

        public BitField Xor(BitField value)
        {
            Check(value);

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] ^= value.array[i];

            Validate();
            return this;
        }

        public override bool Equals(object obj)
        {
            BitField bf = obj as BitField;

            if (bf == null || this.array.Length != bf.array.Length || TrueCount != bf.TrueCount)
                return false;

            for (int i = 0; i < this.array.Length; i++)
                if (array[i] != bf.array[i])
                    return false;

            return true;
        }

        public int FirstTrue()
        {
            return this.FirstTrue(0, this.length);
        }

        public int FirstTrue(int startIndex, int endIndex)
        {
            int start;
            int end;

            // If the number of pieces is an exact multiple of 32, we need to decrement by 1 so we don't overrun the array
            // For the case when endIndex == 0, we need to ensure we don't go negative
            int loopEnd = Math.Min((endIndex / 32), array.Length - 1);
            for (int i = (startIndex / 32); i <= loopEnd; i++)
            {
                if (this.array[i] == 0)        // This one has no true values
                    continue;

                start = i * 32;
                end = start + 32;
                start = (start < startIndex) ? startIndex : start;
                end = (end > this.length) ? this.length : end;
                end = (end > endIndex) ? endIndex : end;
                if (end == Length && end > 0)
                    end--;

                for (int j = start; j <= end; j++)
                    if (Get(j))     // This piece is true
                        return j;
            }

            return -1;              // Nothing is true
        }

        public int FirstFalse()
        {
            return FirstFalse(0, Length);
        }

        public int FirstFalse(int startIndex, int endIndex)
        {
            int start;
            int end;

            // If the number of pieces is an exact multiple of 32, we need to decrement by 1 so we don't overrun the array
            // For the case when endIndex == 0, we need to ensure we don't go negative
            int loopEnd = Math.Min((endIndex / 32), array.Length - 1);
            for (int i = (startIndex / 32); i <= loopEnd; i++)
            {
                if (this.array[i] == ~0)        // This one has no false values
                    continue;

                start = i * 32;
                end = start + 32;
                start = (start < startIndex) ? startIndex : start;
                end = (end > this.length) ? this.length : end;
                end = (end > endIndex) ? endIndex : end;
                if (end == Length && end > 0)
                    end--;

                for (int j = start; j <= end; j++)
                    if (!Get(j))     // This piece is true
                        return j;
            }

            return -1;              // Nothing is true
        }
        internal void FromArray(byte[] buffer, int offset, int length)
        {
            int end = Length / 32;
            for (int i = 0; i < end; i++)
                array[i] = (buffer[offset++] << 24) |
                           (buffer[offset++] << 16) |
                           (buffer[offset++] << 8) |
                           (buffer[offset++] << 0);

            int shift = 24;
            for (int i = end * 32; i < Length; i += 8)
            {
                array[array.Length - 1] |= buffer[offset++] << shift;
                shift -= 8;
            }
            Validate();
        }

        bool Get(int index)
        {
            if (index < 0 || index >= length)
                throw new ArgumentOutOfRangeException("index");

            return (this.array[index >> 5] & (1 << (31 - (index & 31)))) != 0;
        }

        public IEnumerator<bool> GetEnumerator()
        {
            for (int i = 0; i < this.length; i++)
                yield return Get(i);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override int GetHashCode()
        {
            int count = 0;
            for (int i = 0; i < this.array.Length; i++)
                count += this.array[i];

            return count;
        }

        public int LengthInBytes
        {
            get { return (this.length + 7) / 8; }      //8 bits in a byte.
        }

        public BitField Set(int index, bool value)
        {
            if (index < 0 || index >= length)
                throw new ArgumentOutOfRangeException("index");

            if (value)
            {
                if ((this.array[index >> 5] & (1 << (31 - (index & 31)))) == 0)// If it's not already true
                    trueCount++;                                        // Increase true count
                this.array[index >> 5] |= (1 << (31 - index & 31));
            }
            else
            {
                if ((this.array[index >> 5] & (1 << (31 - (index & 31)))) != 0)// If it's not already false
                    trueCount--;                                        // Decrease true count
                this.array[index >> 5] &= ~(1 << (31 - (index & 31)));
            }

            return this;
        }

        internal BitField SetTrue(params int[] indices)
        {
            foreach (int index in indices)
                Set(index, true);
            return this;
        }

        internal BitField SetFalse(params int[] indices)
        {
            foreach (int index in indices)
                Set(index, false);
            return this;
        }

        internal BitField SetAll(bool value)
        {
            if (value)
            {
                for (int i = 0; i < this.array.Length; i++)
                    this.array[i] = ~0;
                Validate();
            }

            else
            {
                for (int i = 0; i < this.array.Length; i++)
                    this.array[i] = 0;
                this.trueCount = 0;
            }

            return this;
        }

        internal byte[] ToByteArray()
        {
            byte[] data = new byte[LengthInBytes];
            ToByteArray(data, 0);
            return data;
        }

        internal void ToByteArray(byte[] buffer, int offset)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            ZeroUnusedBits();
            int end = Length / 32;
            for (int i = 0; i < end; i++)
            {
                buffer[offset++] = (byte)(array[i] >> 24);
                buffer[offset++] = (byte)(array[i] >> 16);
                buffer[offset++] = (byte)(array[i] >> 8);
                buffer[offset++] = (byte)(array[i] >> 0);
            }

            int shift = 24;
            for (int i = end * 32; i < Length; i += 8)
            {
                buffer[offset++] = (byte)(array[array.Length - 1] >> shift);
                shift -= 8;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(this.array.Length * 16);
            for (int i = 0; i < Length; i++)
            {
                sb.Append(Get(i) ? 'T' : 'F');
                sb.Append(' ');
            }

            return sb.ToString(0, sb.Length - 1);
        }

        public int TrueCount
        {
            get { return this.trueCount; }
        }

        void Validate()
        {
            ZeroUnusedBits();

            // Update the population count
            uint count = 0;
            for (int i = 0; i < array.Length; i++)
            {
                uint v = (uint)array[i];
                v = v - ((v >> 1) & 0x55555555);
                v = (v & 0x33333333) + ((v >> 2) & 0x33333333);
                count += (((v + (v >> 4) & 0xF0F0F0F) * 0x1010101)) >> 24;
            }
            trueCount = (int)count ;
        }

        void ZeroUnusedBits()
        {
            if (array.Length == 0)
                return;

            // Zero the unused bits
            int shift = 32 - length % 32;
            if (shift != 0)
                array[array.Length - 1] &= (-1 << shift);
        }

        void Check(BitField value)
        {
            MonoTorrent.Check.Value(value);
            if (length != value.length)
                throw new ArgumentException("BitFields are of different lengths", "value");
        }

        #endregion
    }
}
