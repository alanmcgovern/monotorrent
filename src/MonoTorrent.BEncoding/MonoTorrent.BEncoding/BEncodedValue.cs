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
using System.Collections.Generic;
using System.IO;

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Base class for all BEncoded values.
    /// </summary>
    public abstract class BEncodedValue
    {
        const bool DefaultStrictDecoding = false;

        /// <summary>
        /// Encodes the BEncodedValue into a byte array
        /// </summary>
        /// <returns>Byte array containing the BEncoded Data</returns>
        public byte[] Encode ()
        {
            byte[] buffer = new byte[LengthInBytes ()];
            if (Encode (buffer) != buffer.Length)
                throw new BEncodingException ("Error encoding the data");

            return buffer;
        }

        /// <summary>
        /// Encodes the BEncodedValue into the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to encode the information to</param>
        /// <returns></returns>
        public abstract int Encode (Span<byte> buffer);

        public static T Clone<T> (T value)
            where T : BEncodedValue
            => (T) Decode (value.Encode ());

        /// <summary>
        /// Decode BEncoded data in the given stream 
        /// </summary>
        /// <param name="buffer">The byte array containing the BEncoded data</param>
        /// <returns></returns>
        public static BEncodedValue Decode (ReadOnlySpan<byte> buffer)
            => BEncodeDecoder.Decode (ref buffer, DefaultStrictDecoding);

        /// <summary>
        /// Decode BEncoded data in the given stream 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="strictDecoding"></param>
        /// <returns></returns>
        public static BEncodedValue Decode (ReadOnlySpan<byte> buffer, bool strictDecoding)
            => BEncodeDecoder.Decode (ref buffer, strictDecoding);

        /// <summary>
        /// Decode BEncoded data in the given stream 
        /// </summary>
        /// <param name="stream">The stream containing the BEncoded data</param>
        /// <returns>BEncodedValue containing the data that was in the stream</returns>
        public static BEncodedValue Decode (Stream stream)
            => BEncodeDecoder.Decode (stream, DefaultStrictDecoding);

        /// <summary>
        /// Decode BEncoded data in the given stream 
        /// </summary>
        /// <param name="stream">The stream containing the BEncoded data</param>
        /// <param name="strictDecoding"></param>
        /// <returns></returns>
        public static BEncodedValue Decode (Stream stream, bool strictDecoding)
            => BEncodeDecoder.Decode (stream, strictDecoding);

        /// <summary>
        /// Decode BEncoded data in the given byte array
        /// </summary>
        /// <param name="buffer">The byte array containing the BEncoded data</param>
        /// <returns></returns>
        public static T Decode<T> (ReadOnlySpan<byte> buffer) where T : BEncodedValue
           => (T) BEncodeDecoder.Decode (ref buffer, DefaultStrictDecoding);

        /// <summary>
        /// Decode BEncoded data in the given byte array
        /// </summary>
        /// <param name="buffer">The byte array containing the BEncoded data</param>
        /// <param name="strictDecoding"></param>
        /// <returns></returns>
        public static T Decode<T> (ReadOnlySpan<byte> buffer, bool strictDecoding) where T : BEncodedValue
            => (T) BEncodeDecoder.Decode (ref buffer, strictDecoding);

        /// <summary>
        /// Decode BEncoded data in the given stream 
        /// </summary>
        /// <param name="stream">The stream containing the BEncoded data</param>
        /// <returns>BEncodedValue containing the data that was in the stream</returns>
        public static T Decode<T> (Stream stream) where T : BEncodedValue
             => (T) BEncodeDecoder.Decode (stream, DefaultStrictDecoding);

        /// <summary>
        /// Decode BEncoded data in the given stream 
        /// </summary>
        /// <param name="stream">The stream containing the BEncoded data</param>
        /// <param name="strictDecoding"></param>
        /// <returns>BEncodedValue containing the data that was in the stream</returns>
        public static T Decode<T> (Stream stream, bool strictDecoding) where T : BEncodedValue
             => (T) BEncodeDecoder.Decode (stream, strictDecoding);

        /// <summary>
        /// Returns the length of the BEncodedValue in bytes.
        /// </summary>
        /// <returns></returns>
        public abstract int LengthInBytes ();

        // Helper method for testing. Decodes the value using each parser.
        internal static IEnumerable<T> DecodingVariants<T>(byte[] data)
            where T : BEncodedValue
        {
            yield return Decode<T> (data);
            yield return Decode<T> (new MemoryStream (data));
        }
    }
}
