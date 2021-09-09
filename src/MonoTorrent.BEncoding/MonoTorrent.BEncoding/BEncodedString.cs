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
using System.ComponentModel;
using System.Text;
using System.Web;

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Class representing a BEncoded string
    /// </summary>
    public class BEncodedString : BEncodedValue, IComparable<BEncodedString>
    {
        public static readonly BEncodedString Empty = new BEncodedString (Array.Empty<byte> ());

        public static bool IsNullOrEmpty (BEncodedString value)
        {
            return (value?.TextBytes.Length ?? 0) == 0;
        }

        public static BEncodedString UrlDecode (string urlEncodedValue)
        {
            if (urlEncodedValue == null)
                return null;
            if (urlEncodedValue.Length == 0)
                return Empty;
            return new BEncodedString (HttpUtility.UrlDecodeToBytes (urlEncodedValue, Encoding.UTF8));
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use 'UrlDecode' instead'")]
        public static BEncodedString FromUrlEncodedString (string urlEncodedValue)
        => UrlDecode (urlEncodedValue);


        /// <summary>
        /// The value of the BEncodedString interpreted as a UTF-8 string. If the underlying bytes
        /// cannot be represented in UTF-8 then the invalid byte sequence is silently discarded.
        /// </summary>
        public string Text => Encoding.UTF8.GetString (TextBytes);

        /// <summary>
        /// The underlying byte[] associated with this BEncodedString
        /// </summary>
        public byte[] TextBytes { get; private set; }

        #region Constructors
        /// <summary>
        /// Create a new BEncodedString using UTF8 encoding
        /// </summary>
        public BEncodedString ()
            : this (Array.Empty<byte> ())
        {
        }

        /// <summary>
        /// Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value"></param>
        public BEncodedString (char[] value)
            : this (Encoding.UTF8.GetBytes (value))
        {
        }

        /// <summary>
        /// Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value">Initial value for the string</param>
        public BEncodedString (string value)
            : this (Encoding.UTF8.GetBytes (value))
        {
        }


        /// <summary>
        /// Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value"></param>
        public BEncodedString (byte[] value)
        {
            TextBytes = value;
        }

        public static implicit operator BEncodedString (string value)
        {
            if (value == null)
                return null;
            if (value.Length == 0)
                return Empty;
            return new BEncodedString (value);
        }

        public static implicit operator BEncodedString (char[] value)
        {
            return value == null ? null : new BEncodedString (value);
        }

        public static implicit operator BEncodedString (byte[] value)
        {
            return value == null ? null : new BEncodedString (value);
        }

        #endregion


        #region Encode/Decode Methods


        /// <summary>
        /// Encodes the BEncodedString to a byte[] using the supplied Encoding
        /// </summary>
        /// <param name="buffer">The buffer to encode the string to</param>
        /// <param name="offset">The offset at which to save the data to</param>
        /// <returns>The number of bytes encoded</returns>
        public override int Encode (byte[] buffer, int offset)
        {
            int written = offset;
            written += WriteLengthAsAscii (buffer, written, TextBytes.Length);
            buffer[written++] = (byte) ':';
            Buffer.BlockCopy (TextBytes, 0, buffer, written, TextBytes.Length);
            return written + TextBytes.Length - offset;
        }

        int WriteLengthAsAscii (byte[] buffer, int offset, int asciiLength)
        {
            if (asciiLength > 100000) {
                var data = Encoding.ASCII.GetBytes (TextBytes.Length.ToString ());
                Buffer.BlockCopy (data, 0, buffer, offset, data.Length);
                return data.Length;
            }
            bool hasWritten = false;
            int written = offset;
            for (int remainder = 100000; remainder > 1; remainder /= 10) {
                if (asciiLength < remainder && !hasWritten)
                    continue;
                byte resultChar = (byte) ('0' + asciiLength / remainder);
                buffer[written++] = resultChar;
                asciiLength %= remainder;
                hasWritten = true;
            }
            buffer[written++] = (byte) ('0' + asciiLength);
            return written - offset;
        }

        #endregion


        #region Helper Methods

        public override int LengthInBytes ()
        {
            // The length is equal to the length-prefix + ':' + length of data
            // If the string is of length 0 we need to account for that too.
            int prefix = TextBytes.Length == 0 ? 2 : 1; // Account for ':'

            // Count the number of characters needed for the length prefix
            for (int i = TextBytes.Length; i != 0; i /= 10)
                prefix += 1;

            return prefix + TextBytes.Length;
        }

        public int CompareTo (object other)
        {
            return CompareTo (other as BEncodedString);
        }


        public int CompareTo (BEncodedString other)
        {
            if (other == null)
                return 1;

            int difference;
            int length = TextBytes.Length > other.TextBytes.Length ? other.TextBytes.Length : TextBytes.Length;

            for (int i = 0; i < length; i++)
                if ((difference = TextBytes[i].CompareTo (other.TextBytes[i])) != 0)
                    return difference;

            if (TextBytes.Length == other.TextBytes.Length)
                return 0;

            return TextBytes.Length > other.TextBytes.Length ? 1 : -1;
        }

        #endregion


        #region Overridden Methods

        public override bool Equals (object obj)
        {
            if (obj == null)
                return false;

            BEncodedString other;
            if (obj is string str)
                other = new BEncodedString (str);
            else if (obj is BEncodedString bString)
                other = bString;
            else
                return false;

            var first = TextBytes;
            var second = other.TextBytes;
            if (first.Length != second.Length)
                return false;

            for (int i = 0; i < first.Length; i++)
                if (first[i] != second[i])
                    return false;
            return true;
        }

        public override int GetHashCode ()
        {
            int hash = 0;
            for (int i = 0; i < TextBytes.Length; i++)
                hash += TextBytes[i];

            return hash;
        }

        public string UrlEncode ()
        {
            return HttpUtility.UrlEncode (TextBytes);
        }

        public string ToHex ()
        {
            return BitConverter.ToString (TextBytes);
        }

        public override string ToString ()
        {
            return Encoding.UTF8.GetString (TextBytes);
        }

        #endregion
    }
}
