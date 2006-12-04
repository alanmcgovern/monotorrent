//
// BEncode.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//   Gregor Burger burger.gregor@gmail.com
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
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace MonoTorrent.Common
{
    /// <summary>
    /// Class for decoding any BEncoded material
    /// </summary>
    public static class BEncode
    {
        /// <summary>
        /// Interface for all BEncoded values
        /// </summary>
        /// <param name="data">The byte array containing the BEncoded data</param>
        /// <returns></returns>
        public static IBEncodedValue Decode(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
                return (BEncode.Decode(stream, Encoding.UTF8));
        }


        /// <summary>
        /// Decode BEncoded data in the given byte array
        /// </summary>
        /// <param name="data">The byte array containing the BEncoded data</param>
        /// <param name="encoding">The character encoding to use</param>
        /// <returns>BEncodedValue containing the data that was in the stream</returns>
        public static IBEncodedValue Decode(byte[] data, Encoding encoding)
        {
            using (MemoryStream stream = new MemoryStream(data))
                return (BEncode.Decode(stream, encoding));
        }


        /// <summary>
        /// Decode BEncoded data in the given byte array
        /// </summary>
        /// <param name="buffer">The byte array containing the BEncoded data</param>
        /// <param name="offset">The offset at which the data starts at</param>
        /// <param name="length">The number of bytes to be decoded</param>
        /// <returns>BEncodedValue containing the data that was in the byte[]</returns>
        public static IBEncodedValue Decode(byte[] buffer, int offset, int length)
        {
            using (MemoryStream stream = new MemoryStream(buffer, offset, length))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                return Decode(reader);
        }


        /// <summary>
        /// Decode BEncoded data in the given byte array
        /// </summary>
        /// <param name="buffer">The byte array containing the BEncoded data</param>
        /// <param name="offset">The offset at which the data starts at</param>
        /// <param name="length">The number of bytes to be decoded</param>
        /// <param name="e">The encoding of the data</param>
        /// <returns>BEncodedValue containing the data that was in the byte[]</returns>
        public static IBEncodedValue Decode(byte[] buffer, int offset, int length, Encoding e)
        {
            using (MemoryStream stream = new MemoryStream(buffer, offset, length))
            using (BinaryReader reader = new BinaryReader(stream, e))
                return Decode(reader);
        }


        /// <summary>
        /// Decode BEncoded data in the given stream 
        /// </summary>
        /// <param name="stream">The stream containing the BEncoded data</param>
        /// <returns>BEncodedValue containing the data that was in the stream</returns>
        public static IBEncodedValue Decode(Stream stream)
        {
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                return Decode(reader);
        }


        /// <summary>
        /// Decode BEncoded data in the given stream 
        /// </summary>
        /// <param name="stream">The stream containing the BEncoded data</param>
        /// <param name="encoding">The character encoding to use</param>
        /// <returns>BEncodedValue containing the data that was in the stream</returns>
        public static IBEncodedValue Decode(Stream stream, Encoding encoding)
        {
            using (BinaryReader reader = new BinaryReader(stream, encoding))
                return Decode(reader);
        }


        /// <summary>
        /// Decode BEncoded data in the given BinaryReader
        /// </summary>
        /// <param name="reader">The BinaryReader containing the BEncoded data</param>
        /// <returns>BEncodedValue containing the data that was in the stream</returns>
        public static IBEncodedValue Decode(BinaryReader reader)
        {
            IBEncodedValue data;
            switch ((char)reader.PeekChar())
            {
                case ('i'):                         // Integer
                    data = new BEncodedNumber();
                    break;

                case ('d'):                         // Dictionary
                    data = new BEncodedDictionary();
                    break;

                case ('l'):                         // List
                    data = new BEncodedList();
                    break;

                case ('1'):             // String
                case ('2'):
                case ('3'):
                case ('4'):
                case ('5'):
                case ('6'):
                case ('7'):
                case ('8'):
                case ('9'):
                case ('0'):
                    data = new BEncodedString();
                    break;

                default:
                    throw new BEncodingException("Could not find what value to decode");
            }

            data.Decode(reader);
            return data;
        }
    }
}
