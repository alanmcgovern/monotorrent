//
// BEncodedString.cs
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
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Class representing a BEncoded string
    /// </summary>
    public class BEncodedString : BEncodedValue, IComparable<BEncodedString>
    {
        #region Member Variables

        /// <summary>
        /// The value of the BEncodedString
        /// </summary>
        public string Text
        {
            get { return Encoding.UTF8.GetString(textBytes); }
            set { textBytes = Encoding.UTF8.GetBytes(value); }
        }

        /// <summary>
        /// The underlying byte[] associated with this BEncodedString
        /// </summary>
        public byte[] TextBytes
        {
            get { return this.textBytes; }
        }
        private byte[] textBytes;
        #endregion


        #region Constructors
        /// <summary>
        /// Create a new BEncodedString using UTF8 encoding
        /// </summary>
        public BEncodedString()
            : this(new byte[0])
        {
        }

        /// <summary>
        /// Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value"></param>
        public BEncodedString(char[] value)
            : this(System.Text.Encoding.UTF8.GetBytes(value))
        {
        }




        /// <summary>
        /// Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value">Initial value for the string</param>
        public BEncodedString(string value)
            : this(System.Text.Encoding.UTF8.GetBytes(value))
        {
        }


        /// <summary>
        /// Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value"></param>
        public BEncodedString(byte[] value)
        {
            this.textBytes = value;
        }


        public static implicit operator BEncodedString(string value)
        {
            return new BEncodedString(value);
        }
        public static implicit operator BEncodedString(char[] value)
        {
            return new BEncodedString(value);
        }
        public static implicit operator BEncodedString(byte[] value)
        {
            return new BEncodedString(value);
        }
        #endregion


        #region Encode/Decode Methods


        /// <summary>
        /// Encodes the BEncodedString to a byte[] using the supplied Encoding
        /// </summary>
        /// <param name="buffer">The buffer to encode the string to</param>
        /// <param name="offset">The offset at which to save the data to</param>
        /// <param name="e">The encoding to use</param>
        /// <returns>The number of bytes encoded</returns>
        public override int Encode(byte[] buffer, int offset)
        {
            string output = this.textBytes.Length + ":";
            int written = System.Text.Encoding.UTF8.GetBytes(output, 0, output.Length, buffer, offset);
            Buffer.BlockCopy(this.textBytes, 0, buffer, offset + written, this.textBytes.Length);
            return written + this.textBytes.Length;
        }


        /// <summary>
        /// Decodes a BEncodedString from the supplied StreamReader
        /// </summary>
        /// <param name="reader">The StreamReader containing the BEncodedString</param>
        internal override void DecodeInternal(BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            int letterCount;
            StringBuilder sb = new StringBuilder(20);

            try
            {
                while ((reader.PeekChar() != -1) && (reader.PeekChar() != ':'))     // read in how many characters
                    sb.Append((char)reader.ReadChar());                                 // the string is

                if (reader.ReadChar() != ':')                                           // remove the ':'
                    throw new BEncodingException("Invalid data found. Aborting");

                letterCount = int.Parse(sb.ToString());
                sb.Remove(0, sb.Length);

                this.textBytes = new byte[letterCount];

                if (reader.Read(textBytes, 0, letterCount) != letterCount)
                    throw new BEncodingException("Couldn't decode string");
            }
            catch (BEncodingException ex)
            {
                throw new BEncodingException("Couldn't decode string", ex);
            }
            catch (Exception ex)
            {
                throw new BEncodingException("Couldn't decode string", ex);
            }
        }
        #endregion


        #region Helper Methods
        public override int LengthInBytes()
        {
            string output = this.textBytes.Length.ToString() + ":";
            return (output.Length + this.textBytes.Length);
        }

        public int CompareTo(BEncodedString other)
        {
            if (other == null)
                return 1;

            int difference=0;
            int length = this.textBytes.Length > other.textBytes.Length ? other.textBytes.Length : this.textBytes.Length;

            for (int i = 0; i < length; i++)
                if ((difference = this.textBytes[i].CompareTo(other.textBytes[i])) != 0)
                    return difference;

            if (this.textBytes.Length == other.textBytes.Length)
                return 0;

            return this.textBytes.Length > other.textBytes.Length ? 1 : -1;
        }

        #endregion


        #region Overridden Methods

        public override bool Equals(object obj)
        {
            BEncodedString other = obj as BEncodedString;
            if (obj is string)
                other = new BEncodedString((string)obj);

            if (other == null)
                return false;

            if (this.textBytes.Length != other.textBytes.Length)
                return false;

            for (int i = 0; i < this.textBytes.Length; i++)
                if (this.textBytes[i] != other.textBytes[i])
                    return false;

            return true;
        }

        public override int GetHashCode()
        {
            int hash = 0;
            for (int i = 0; i < this.textBytes.Length; i++)
                hash += this.textBytes[i];

            return hash;
        }

        public override string ToString()
        {
            return System.Text.Encoding.UTF8.GetString(textBytes);
        }

        #endregion
    }
}