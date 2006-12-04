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

namespace MonoTorrent.Common
{
    /// <summary>
    /// Class representing a BEncoded string
    /// </summary>
    public class BEncodedString : IBEncodedValue, IComparable<BEncodedString>
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



        /// <summary>
        /// Create a new BEncodedString using the supplied encoding
        /// </summary>
        /// <param name="b">The byte array containing the string</param>
        /// <param name="offset">The offset into the array</param>
        /// <param name="length">Specifies how long the string should be</param>
        public BEncodedString(byte[] b, int offset, int length, Encoding e)
        {
            this.textBytes = new byte[length];
            Array.Copy(b, offset, this.textBytes, 0, length);
        }
        

        /// <summary>
        /// Create a new BEncodedString using UTF8 Encoding
        /// </summary>
        /// <param name="b">The byte array containing the string</param>
        /// <param name="offset">The offset into the array</param>
        /// <param name="length">Specifies how long the string should be</param>
        public BEncodedString(byte[] b, int offset, int length)
            : this(b, offset, length, new UTF8Encoding(false, false))
        {
            
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
        /// Creates a BEncoded string representation of this string
        /// </summary>
        /// <returns></returns>
        public byte[] Encode()
        {
            byte[] buffer = new byte[this.LengthInBytes()];
            this.Encode(buffer, 0);
            return buffer;
        }


        /// <summary>
        /// Encodes the BEncodedString to a byte[] using the supplied Encoding
        /// </summary>
        /// <param name="buffer">The buffer to encode the string to</param>
        /// <param name="offset">The offset at which to save the data to</param>
        /// <param name="e">The encoding to use</param>
        /// <returns>The number of bytes encoded</returns>
        public int Encode(byte[] buffer, int offset)
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
        public void Decode(BinaryReader reader)
        {
            int letterCount;
            StringBuilder sb = new StringBuilder(20);

            try
            {
                while ((reader.PeekChar() != -1) && (reader.PeekChar() != ':'))         // read in how many characters
                    sb.Append((char)reader.ReadChar());                                 // the string is

                if (reader.ReadChar() != ':')                                            // remove the ':'
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
        public int LengthInBytes()
        {
            string output = this.textBytes.Length.ToString() + ":";
            return (output.Length + this.textBytes.Length);
        }

        public int CompareTo(BEncodedString other)
        {
            return this.Text.CompareTo(other.Text);
        }

        public int CompareTo(string other)
        {
            return this.Text.CompareTo(other);
        }
        #endregion


        #region Overridden Methods
        public override bool Equals(object obj)
        {
            BEncodedString benString = obj as BEncodedString;
            if (benString == null)
                return false;

            return ToolBox.ByteMatch(this.textBytes, benString.textBytes);
        }

        public override int GetHashCode()
        {
            return this.textBytes.GetHashCode();
        }

        public override string ToString()
        {
            return System.Text.Encoding.UTF8.GetString(textBytes);
        }
        #endregion
    }
}