//
// IBEncodedValue.cs
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

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Base interface for all BEncoded values.
    /// </summary>
    public abstract class BEncodedValue
    {
        /// <summary>
        /// Encodes the BEncodedValue into a byte array
        /// </summary>
        /// <returns>Byte array containing the BEncoded Data</returns>
        public byte[] Encode ()
        {
            byte[] buffer = new byte[LengthInBytes ()];
            if (Encode (buffer, 0) != buffer.Length)
                throw new BEncodingException ("Error encoding the data");

            return buffer;
        }


        /// <summary>
        /// Encodes the BEncodedValue into the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to encode the information to</param>
        /// <param name="offset">The offset in the buffer to start writing the data</param>
        /// <returns></returns>
        public abstract int Encode (byte[] buffer, int offset);

        public static T Clone<T> (T value)
            where T : BEncodedValue
        {
            Check.Value (value);
            return (T) Decode (value.Encode ());
        }

        /// <summary>
        /// Interface for all BEncoded values
        /// </summary>
        /// <param name="data">The byte array containing the BEncoded data</param>
        /// <returns></returns>
        public static BEncodedValue Decode (byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException (nameof (data));

            using var stream = new RawReader (new MemoryStream (data));
            return (Decode (stream));
        }

        internal static BEncodedValue Decode (byte[] buffer, bool strictDecoding)
        {
            return Decode (buffer, 0, buffer.Length, strictDecoding);
        }

        /// <summary>
        /// Decode BEncoded data in the given byte array
        /// </summary>
        /// <param name="buffer">The byte array containing the BEncoded data</param>
        /// <param name="offset">The offset at which the data starts at</param>
        /// <param name="length">The number of bytes to be decoded</param>
        /// <returns>BEncodedValue containing the data that was in the byte[]</returns>
        public static BEncodedValue Decode (byte[] buffer, int offset, int length)
        {
            return Decode (buffer, offset, length, true);
        }

        public static BEncodedValue Decode (byte[] buffer, int offset, int length, bool strictDecoding)
        {
            if (buffer == null)
                throw new ArgumentNullException (nameof (buffer));

            if (offset < 0 || length < 0)
                throw new IndexOutOfRangeException ("Neither offset or length can be less than zero");

            if (offset > buffer.Length - length)
                throw new ArgumentOutOfRangeException (nameof (length));

            using var reader = new RawReader (new MemoryStream (buffer, offset, length), strictDecoding);
            return (Decode (reader));
        }


        /// <summary>
        /// Decode BEncoded data in the given stream 
        /// </summary>
        /// <param name="stream">The stream containing the BEncoded data</param>
        /// <returns>BEncodedValue containing the data that was in the stream</returns>
        public static BEncodedValue Decode (Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException (nameof (stream));

            return Decode (new RawReader (stream));
        }


        /// <summary>
        /// Decode BEncoded data in the given RawReader
        /// </summary>
        /// <param name="reader">The RawReader containing the BEncoded data</param>
        /// <returns>BEncodedValue containing the data that was in the stream</returns>
        public static BEncodedValue Decode (RawReader reader)
        {
            return BEncodeDecoder.Decode (reader);
        }


        /// <summary>
        /// Interface for all BEncoded values
        /// </summary>
        /// <param name="data">The byte array containing the BEncoded data</param>
        /// <returns></returns>
        public static T Decode<T> (byte[] data) where T : BEncodedValue
        {
            return (T) Decode (data);
        }


        /// <summary>
        /// Decode BEncoded data in the given byte array
        /// </summary>
        /// <param name="buffer">The byte array containing the BEncoded data</param>
        /// <param name="offset">The offset at which the data starts at</param>
        /// <param name="length">The number of bytes to be decoded</param>
        /// <returns>BEncodedValue containing the data that was in the byte[]</returns>
        public static T Decode<T> (byte[] buffer, int offset, int length) where T : BEncodedValue
        {
            return Decode<T> (buffer, offset, length, true);
        }

        public static T Decode<T> (byte[] buffer, int offset, int length, bool strictDecoding) where T : BEncodedValue
        {
            return (T) Decode (buffer, offset, length, strictDecoding);
        }


        /// <summary>
        /// Decode BEncoded data in the given stream 
        /// </summary>
        /// <param name="stream">The stream containing the BEncoded data</param>
        /// <returns>BEncodedValue containing the data that was in the stream</returns>
        public static T Decode<T> (Stream stream) where T : BEncodedValue
        {
            return (T) Decode (stream);
        }


        public static T Decode<T> (RawReader reader) where T : BEncodedValue
        {
            return (T) Decode (reader);
        }


        /// <summary>
        /// Returns the size of the byte[] needed to encode this BEncodedValue
        /// </summary>
        /// <returns></returns>
        public abstract int LengthInBytes ();
    }
}
