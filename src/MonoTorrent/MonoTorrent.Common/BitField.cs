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
            for (int i = 0; i < this.array.Length; i++)
                b.array[i] = this.array[i];

            b.trueCount = trueCount;
            return b;
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
            if (value == null)
                throw new ArgumentNullException("value");

            if (this.length != value.length)
                throw new ArgumentException("BitFields are of different lengths", "value");

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] &= value.array[i];

            Validate();
            return this;
        }

        internal BitField NAnd(BitField value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            if (this.length != value.length)
                throw new ArgumentException("BitFields are of different lengths", "value");

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] &= ~value.array[i];

            Validate();
            return this;
        }

        public BitField Or(BitField value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            if (this.length != value.length)
                throw new ArgumentException("BitFields are of different lengths", "value");

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] |= value.array[i];

            Validate();
            return this;
        }

        public BitField Xor(BitField value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            if (this.length != value.length)
                throw new ArgumentException("BitFields are of different lengths", "value");

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] ^= value.array[i];

            Validate();
            return this;
        }

        public override bool Equals(object obj)
        {
            BitField bf = obj as BitField;

            if (bf == null || this.array.Length != bf.array.Length)
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
            byte p = 128;
            bool temp = false;
            this.trueCount = 0;

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            // Decode the bitfield from the buffer
            for (int i = 0; i < this.length; i++)
            {
                temp = ((buffer[offset] & p) != 0);
                this.Set(i, temp);
                p >>= 1;

                if (p != 0)
                    continue;

                p = 128;
                offset++;
            }

            // If true, there are no extra bits
            if (this.length % 8 == 0)
                return;

            // Make sure all extra bits are set to zero
            for (int i = this.length; i < this.length + (8 - this.length % 8); i++)
            {
                temp = ((buffer[offset] & p) != 0);
                if (temp)
                    Logger.Log(null, "BitField - Invalid bitfield received, high bits not set to zero. Attempting to continue...");

                p >>= 1;

                if (p != 0)
                    continue;

                p = 128;
                offset++;
            }
        }

        bool Get(int index)
        {
            if (index < 0 || index >= length)
                throw new ArgumentOutOfRangeException("index");

            return (this.array[index >> 5] & (1 << (index & 31))) != 0;
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
            get { return ((int)Math.Ceiling(this.length / 8.0)); }      //8 bits in a byte.
        }

        void Set(int index, bool value)
        {
            if (index < 0 || index >= length)
                throw new ArgumentOutOfRangeException("index");

            if (value)
            {
                if ((this.array[index >> 5] & (1 << (index & 31))) == 0)// If it's not already true
                    trueCount++;                                        // Increase true count
                this.array[index >> 5] |= (1 << (index & 31));
            }
            else
            {
                if ((this.array[index >> 5] & (1 << (index & 31))) != 0)// If it's not already false
                    trueCount--;                                        // Decrease true count
                this.array[index >> 5] &= ~(1 << (index & 31));
            }
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

            Validate();

            int byteindex = offset;
            byte temp = 0;
            byte position = 128;
            for (int i = 0; i < this.length; i++)
            {
                if (this[i])
                    temp |= position;

                position >>= 1;

                if (position == 0)              // Current byte is full.
                {
                    buffer[byteindex] = temp;     // Add byte into the array
                    position = 128;             // Reset position to the high bit
                    temp = 0;                   // reset temp = 0
                    byteindex++;                // advance position in the array by 1
                }
            }
            if (position != 128)                // We need to add in the last byte
                buffer[byteindex] = temp;
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

        private void Validate()
        {
            // Zero the unused bits
            array[array.Length - 1] &= ~(-1 << length % 32);

            // Update the population count
            trueCount = 0;
            for (int i = 0; i < array.Length; i++)
            {
                uint v = (uint)array[i];
                v = v - ((v >> 1) & 0x55555555);
                v = (v & 0x33333333) + ((v >> 2) & 0x33333333);
                trueCount += (int)(((v + (v >> 4) & 0xF0F0F0F) * 0x1010101) >> 24);
            }
        }

        #endregion
    }
}
