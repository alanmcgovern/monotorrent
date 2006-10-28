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

            return this;
        }


        /// <summary>
        /// Performs binary AND on all the elements of this BitField against the elements of the supplied BitField
        /// </summary>
        /// <param name="value">The BitField with which to perform the operation against</param>
        /// <returns>Itself</returns>
        internal BitField And(BitField value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

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

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] &= ~value.array[i];

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

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] |= value.array[i];

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

            for (int i = 0; i < this.array.Length; i++)
                this.array[i] ^= value.array[i];

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
                this.array[index >> 5] |= (1 << (index & 31));
            else
                this.array[index >> 5] &= ~(1 << (index & 31));
        }


        /// <summary>
        /// Sets all values in the BitArray to the specified value
        /// </summary>
        /// <param name="value"></param>
        internal void SetAll(bool value)
        {
            if (value)
                for (int i = 0; i < this.array.Length; i++)
                    this.array[i] = ~0;

            else
                for (int i = 0; i < this.array.Length; i++)
                    this.array[i] = 0;

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
        internal bool AllFalse()
        {
            for (int i = 0; i < this.array.Length; i++)
                if (array[i] != 0)
                    return false;

            return true;
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

            for (int i = (startIndex/32); i <= (endIndex/32); i++)
            {
                if (this.array[i] == 0)        // This one has no true values
                    continue;

                start = i * 32;
                end = start + 32;
                end = (end > this.length) ? this.length : end;

                for (int j = start; j < end; j++)
                    if (Get(j))     // This piece is true
                        return j;
            }

            return -1;              // Nothing is true
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
        /// Decodes a BitField from the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer containing the BitField</param>
        /// <param name="offset">The offset at which to start decoding the BitField at</param>
        /// <param name="length">The maximum number of bytes to read while decoding the BitField</param>
#warning Check the remaining bits in the last byte to make sure they're 0. use the length parameter
        internal void FromArray(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            byte p = 128;

            for (int i = 0; i < this.length; i++)
            {
                this[i] = ((buffer[offset] & p) != 0);
                p >>= 1;

                if (p != 0)
                    continue;

                p = 128;
                offset++;
            }
        }

        internal void FromArray(int[] array)
        {
            this.array = array;
        }

        /// <summary>
        /// Returns the length of this message in bytes
        /// </summary>
        public int LengthInBytes
        {
            get { return ((int)Math.Ceiling(this.length / 8.0)); }      //8 bits in a byte.
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
            return this.array.GetHashCode();
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
