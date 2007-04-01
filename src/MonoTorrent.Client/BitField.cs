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

namespace MonoTorrent.Client
{
    /// <summary>
    /// This class is for represting the Peer's bitfield
    /// </summary>
    public class BitField : ICloneable, IEnumerable<bool>
    {
        #region Member Variables
        /// <summary>
        /// Returns the length of the BitField
        /// </summary>
        public int Length
        {
            get { return this.length; }
        }
        private int length;


        /// <summary>
        /// The internal int array for the BitField
        /// </summary>
        internal int[] Array
        {
            get { return this.array; }
        }
        private int[] array;


        /// <summary>
        /// Returns the number of elements in the array which are "true" (i.e. the bit is set to 1)
        /// </summary>
        //public int TrueCount
       // {
       //     get { return this.trueCount; }
       // }
        private int trueCount;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new BitField
        /// </summary>
        /// <param name="length">The length of the BitField</param>
        public BitField(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException("length");

            this.length = length;
            this.array = new int[(length + 31) / 32];
        }
        #endregion


        #region Methods BitArray
        /// <summary>
        /// Returns the value of the BitField at the specified index
        /// </summary>
        /// <param name="index">The index of the BitField to check</param>
        /// <returns></returns>
        public bool this[int index]
        {
            get { return this.Get(index); }
            internal set { this.Set(index, value); }
        }


        /// <summary>
        /// Clones the BitField
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            BitField b = new BitField(this.length);
            for (int i = 0; i < this.array.Length; i++)
                b.array[i] = this.array[i];

            return b;
        }


        /// <summary>
        /// Performs binary NOT on all the elements of the bitarray
        /// </summary>
        /// <returns>Itself</returns>
        internal BitField Not()
        {
            for (int i = 0; i < this.array.Length; i++)
                this.array[i] = ~this.array[i];

			this.trueCount = this.length - this.trueCount;
            return this;
        }


        /// <summary>
        /// Performs binary AND on all the elements of this BitField against the elements of the supplied BitField
        /// </summary>
        /// <param name="value">The BitField with which to perform the operation against</param>
        /// <returns>Itself</returns>
        internal BitField And(BitField value)
        {
			AndFast(value);
			UpdateTrueCount();
			return this;
        }


		internal BitField AndFast(BitField value)
		{
			if (value == null)
				throw new ArgumentNullException("value");

			if (this.length != value.length)
				throw new ArgumentException("BitFields are of different lengths", "value");

			for (int i = 0; i < this.array.Length; i++)
				this.array[i] &= value.array[i];

			return this;
		}

        /// <summary>
        /// Performs binary NAND on all the elements of this bitarray against the elements of the supplied BitField
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal BitField NAnd(BitField value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            if (this.length != value.length)
                throw new ArgumentException("BitFields are of different lengths", "value");

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] &= ~value.array[i];

            UpdateTrueCount();
            return this;
        }


        /// <summary>
        /// Performs binary OR on all the elements of this BitField against the elements of the supplied BitField
        /// </summary>
        /// <param name="value">The BitField with which to perform the operation against</param>
        /// <returns>Itself</returns>
        internal BitField Or(BitField value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            if (this.length != value.length)
                throw new ArgumentException("BitFields are of different lengths", "value");

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] |= value.array[i];

            UpdateTrueCount();
            return this;
        }


        /// <summary>
        /// Performs binary EXCLUSIVE OR on all the elements of this BitField against the elements of the supplied BitField
        /// </summary>
        /// <param name="value">The BitField with which to perform the operation against</param>
        /// <returns>Itself</returns>
        internal BitField Xor(BitField value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            if (this.length != value.length)
                throw new ArgumentException("BitFields are of different lengths", "value");

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] ^= value.array[i];

            UpdateTrueCount();
            return this;
        }


        /// <summary>
        /// Returns the value of the BitField at the specified index
        /// </summary>
        /// <param name="index">The index to return</param>
        /// <returns></returns>
        internal bool Get(int index)
        {
            if (index < 0 || index >= length)
                throw new ArgumentOutOfRangeException("index");

            return (this.array[index >> 5] & (1 << (index & 31))) != 0;
        }


        /// <summary>
        /// Sets the value of the BitField at the specified index
        /// </summary>
        /// <param name="index">The index to set</param>
        /// <param name="value">The value to set</param>
        internal void Set(int index, bool value)
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


        /// <summary>
        /// Sets all values in the BitArray to the specified value
        /// </summary>
        /// <param name="value"></param>
        internal void SetAll(bool value)
        {
            if (value)
            {
                for (int i = 0; i < this.array.Length; i++)
                    this.array[i] = ~0;
                this.trueCount = this.length;
            }

            else
            {
                for (int i = 0; i < this.array.Length; i++)
                    this.array[i] = 0;
                this.trueCount = 0;
            }
        }


        private void SetLastBitsFalse()
        {
            // clear out the remaining space
            int end = ((int)((this.length + 31) / 32)) * 32;
            for (int i = this.length; i < end; ++i)
                this.array[i >> 5] &= ~(1 << (i & 31));
        }
        

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerator<bool> GetEnumerator()
        {
            for (int i = 0; i < this.length; i++)
                yield return Get(i);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < this.length; i++)
                yield return Get(i);
        }
        #endregion


        #region BitField specific methods

        /// <summary>
        /// Returns True if all the elements in the BitField are false
        /// </summary>
        /// <returns></returns>
        internal bool AllFalse
        {
            get { return this.trueCount == 0; }
        }


		internal bool AllFalseSecure()
		{
			SetLastBitsFalse();
			for (int i = 0; i < this.array.Length; i++)
				if (this.array[i] != 0)
					return false;

			return true;
		}


        /// <summary>
        /// Returns true if all the elements in the bitfield are true
        /// </summary>
        internal bool AllTrue
        {
            get { return this.trueCount == this.length; }
        }


        /// <summary>
        /// Returns the first index of the BitField that is true. If no elements are true, returns -1
        /// </summary>
        /// <returns></returns>
        internal int FirstTrue()
        {
            return this.FirstTrue(0, this.length);
        }


        /// <summary>
        /// Returns the first index of the BitField that is true between the start and end index
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <returns></returns>
        internal int FirstTrue(int startIndex, int endIndex)
        {
            int start;
            int end;

            for (int i = (startIndex / 32); i <= (endIndex / 32); i++)
            {
                if (this.array[i] == 0)        // This one has no true values
                    continue;

                start = i * 32;
                end = start + 32;
                start = (start < startIndex) ? startIndex : start;
                end = (end > this.length) ? this.length : end;
                end = (end > endIndex) ? endIndex : end;

                for (int j = start; j < end; j++)
                    if (Get(j))     // This piece is true
                        return j;
            }

            return -1;              // Nothing is true
        }


        /// <summary>
        /// Decodes a BitField from the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer containing the BitField</param>
        /// <param name="offset">The offset at which to start decoding the BitField at</param>
        /// <param name="length">The maximum number of bytes to read while decoding the BitField</param>
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

            // Make sure all extra bits are set to zero
            for (int i = this.length; i < this.length + this.length % 8; i++)
            {
                temp = ((buffer[offset] & p) != 0);
                if (temp)
                    throw new MessageException("Invalid bitfield received");

                p >>= 1;

                if (p != 0)
                    continue;

                p = 128;
                offset++;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="array"></param>
        internal void FromArray(int[] array, int length)
        {
            this.trueCount = 0;
            this.length = length;
            this.array = array;
            for (int i = 0; i < this.length; i++)
                if (this.Get(i))
                    trueCount++;
        }


        /// <summary>
        /// Returns the length of this message in bytes
        /// </summary>
        public int LengthInBytes
        {
            get { return ((int)Math.Ceiling(this.length / 8.0)); }      //8 bits in a byte.
        }


        /// <summary>
        /// Returns the percentage of pieces that are true
        /// </summary>
        internal double PercentComplete
        {
            get { return (double)this.trueCount / this.length * 100.0; }
        }


        /// <summary>
        /// Encodes the bitfield to a byte array
        /// </summary>
        /// <param name="buffer">The buffer to encode the BitField to</param>
        /// <param name="offset">The index to start encoding at</param>
        internal void ToByteArray(byte[] buffer, int offset)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            SetLastBitsFalse();

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

        
        /// <summary>
        /// Updates the truecount after the bitfield has been altered through And(), Or() etc
        /// </summary>
        private void UpdateTrueCount()
        {
            int capacity = sizeof(int) * 8;

            for (int i = 0; i < this.array.Length; i++)
                if (this.array[i] == 0)
                {
                    continue;
                }
                else if (this.array[i] == ~0)
                {
                    this.trueCount += capacity;
                }
                else
                {
                    int startIndex = i * 32;
                    int endIndex = (i + 1) * 32;
                    endIndex = endIndex > this.length ? this.length : endIndex;
                    for (int j = startIndex; j < endIndex; j++)
                        if (Get(j))
                            trueCount++;
                }
        }



        public int TrueCount
        {
            get { return this.trueCount; }
        }
        #endregion


        #region Overridden methods

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


        public override int GetHashCode()
        {
            int count = 0;
            for (int i = 0; i < this.array.Length; i++)
                count += this.array[i];

            return count;
        }


        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(this.array.Length * 16);
            for (int i = 0; i < this.array.Length; i++)
            {
                sb.Append(array[i].ToString());
                sb.Append(',');
            }

            return sb.ToString(0, sb.Length - 1);
        }
        #endregion
    }
}
