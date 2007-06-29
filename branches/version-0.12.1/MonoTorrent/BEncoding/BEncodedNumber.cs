//
// BEncodedNumber.cs
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
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Class representing a BEncoded number
    /// </summary>
    public class BEncodedNumber : BEncodedValue, IComparable<BEncodedNumber>
    {
        #region Member Variables
        /// <summary>
        /// The value of the BEncodedNumber
        /// </summary>
        public long Number
        {
            get { return number; }
            set { number = value; }
        }
        private long number;
        #endregion


        #region Constructors
        public BEncodedNumber()
            : this(0)
        {
        }

        /// <summary>
        /// Create a new BEncoded number with the given value
        /// </summary>
        /// <param name="initialValue">The inital value of the BEncodedNumber</param>
        public BEncodedNumber(long value)
        {
            this.number = value;
        }

        public static implicit operator BEncodedNumber(long value)
        {
            return new BEncodedNumber(value);
        }
        #endregion


        #region Encode/Decode Methods

        /// <summary>
        /// Encodes this number to the supplied byte[] starting at the supplied offset
        /// </summary>
        /// <param name="buffer">The buffer to write the data to</param>
        /// <param name="offset">The offset to start writing the data at</param>
        /// <returns></returns>
        public override int Encode(byte[] buffer, int offset)
        {
            int written = 0;
            buffer[offset + written] = (byte)'i';
            written++;
            written += System.Text.Encoding.UTF8.GetBytes(this.number.ToString(), 0, this.number.ToString().Length, buffer, offset + written);
            buffer[offset + written] = (byte)'e';
            written++;
            return written;
        }


        /// <summary>
        /// Decodes a BEncoded number from the supplied BinaryReader
        /// </summary>
        /// <param name="reader">BinaryReader containing a BEncoded Number</param>
        internal override void DecodeInternal(BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            StringBuilder sb;
            try
            {
                sb = new StringBuilder(8);
                if (reader.ReadByte() != 'i')              // remove the leading 'i'
                    throw new BEncodingException("Invalid data found. Aborting.");

                while ((reader.PeekChar() != -1) && ((char)reader.PeekChar() != 'e'))
                    sb.Append((char)reader.ReadByte());

                if (reader.ReadByte() != 'e')        //remove the trailing 'e'
                    throw new BEncodingException("Invalid data found. Aborting.");

                this.number = long.Parse(sb.ToString());
            }
            catch (BEncodingException ex)
            {
                throw new BEncodingException("Couldn't decode number", ex);
            }
            catch
            {
                throw new BEncodingException("Couldn't decode number");
            }
        }
        #endregion


        #region Helper Methods
        /// <summary>
        /// Returns the length of the encoded string in bytes
        /// </summary>
        /// <returns></returns>
        public override int LengthInBytes()
        {
            return System.Text.Encoding.UTF8.GetByteCount('i' + this.number.ToString() + 'e');
        }


        public int CompareTo(object other)
        {
            if (other is BEncodedNumber || other is long || other is int)
                return CompareTo((BEncodedNumber)other);

            return -1;
        }

        public int CompareTo(BEncodedNumber other)
        {
            if (other == null)
                throw new ArgumentNullException("other");

            return this.number.CompareTo(other.number);
        }


        public int CompareTo(long other)
        {
            return this.number.CompareTo(other);
        }
        #endregion


        #region Overridden Methods
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            BEncodedNumber obj2 = obj as BEncodedNumber;
            if (obj2 == null)
                return false;

            return (this.number == obj2.number);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return this.number.GetHashCode();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return (this.number.ToString());
        }
        #endregion
    }
}
