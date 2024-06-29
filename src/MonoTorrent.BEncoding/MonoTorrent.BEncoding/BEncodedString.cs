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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Class representing a BEncoded string
    /// </summary>
    public class BEncodedString : BEncodedValue, IComparable<BEncodedString>, IEquatable<BEncodedString>
    {
        public static readonly BEncodedString Empty = new BEncodedString (ReadOnlyMemory<byte>.Empty);

        public static bool IsNullOrEmpty ([NotNullWhen (false)] BEncodedString? value)
            => value is null || value.Span.IsEmpty;

        public static BEncodedString FromMemory (ReadOnlyMemory<byte> buffer)
            => buffer.Length == 0 ? Empty : new BEncodedString (buffer);

        public static BEncodedString UrlDecode (string urlEncodedValue)
        {
            if (urlEncodedValue == null)
                throw new ArgumentNullException (urlEncodedValue);
            if (urlEncodedValue.Length == 0)
                return Empty;
            return new BEncodedString (new ReadOnlyMemory<byte> (HttpUtility.UrlDecodeToBytes (urlEncodedValue, Encoding.UTF8)));
        }

        [return: NotNullIfNotNull ("value")]
        public static implicit operator BEncodedString? (string? value)
            => value is null ? null : (value.Length == 0 ? Empty : new BEncodedString (value));

        [return: NotNullIfNotNull ("value")]
        public static implicit operator BEncodedString? (char[]? value)
            => value is null ? null : (value.Length == 0 ? Empty : new BEncodedString (value));

        [return: NotNullIfNotNull ("value")]
        public static implicit operator BEncodedString? (byte[]? value)
            => value is null ? null : (value.Length == 0 ? Empty : new BEncodedString (value));

        readonly ReadOnlyMemory<byte> TextBytes;

        /// <summary>
        /// The value of the BEncodedString interpreted as a UTF-8 string. If the underlying bytes
        /// cannot be represented in UTF-8 then the invalid byte sequence is silently discarded.
        /// </summary>
#if NETSTANDARD2_0 || NET472
        public unsafe string Text {
            get {
                var span = Span;
                if (span.Length == 0)
                    return "";
                fixed (byte* spanPtr = span)
                    return Encoding.UTF8.GetString (spanPtr, span.Length);
            }
        }
#else
        public string Text
            => Encoding.UTF8.GetString (Span);
#endif

        public ReadOnlySpan<byte> Span => TextBytes.Span;

        /// <summary>
        /// Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value"></param>
        public BEncodedString (char[] value)
        {
            if (value is null)
                throw new ArgumentNullException (nameof (value));
            TextBytes =  value.Length == 0 ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte> (Encoding.UTF8.GetBytes (value));
        }

        /// <summary>
        /// Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value">Initial value for the string</param>
        public BEncodedString (string value)
        {
            if (value is null)
                throw new ArgumentNullException (nameof (value));
            TextBytes = value.Length == 0 ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte> (Encoding.UTF8.GetBytes (value));
        }

        /// <summary>
        /// Create a new BEncodedString using UTF8 encoding
        /// </summary>
        /// <param name="value"></param>
        public BEncodedString (byte[] value)
        {
            if (value is null)
                throw new ArgumentNullException (nameof (value));
            TextBytes = value.Length == 0 ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte> ((byte[]) value.Clone ());
        }

        BEncodedString (ReadOnlyMemory<byte> value)
            => TextBytes = value;

        /// <summary>
        /// Returns a readonly reference to the underlying data.
        /// </summary>
        /// <returns></returns>
        public ReadOnlyMemory<byte> AsMemory ()
            => TextBytes;

        /// <summary>
        /// Encodes the BEncodedString to a byte[] using the supplied Encoding
        /// </summary>
        /// <param name="buffer">The buffer to encode the string to</param>
        /// <returns>The number of bytes encoded</returns>
        public override int Encode (Span<byte> buffer)
        {
            var written = WriteLengthAsAscii (buffer, TextBytes.Length);
            buffer[written++] = (byte) ':';
            Span.CopyTo (buffer.Slice (written));
            return written + Span.Length;
        }

#if NETSTANDARD2_0 || NET472
        static int WriteLengthAsAscii (Span<byte> buffer, int asciiLength)
        {
            if (asciiLength == 0) {
                buffer[0] = (byte) '0';
                return 1;
            }

            // int32.MinValue can have at most 11 characters
            Span<byte> printedchars = stackalloc byte[11];
            int counter = printedchars.Length;
            while (asciiLength > 0) {
                printedchars[--counter] = (byte) ('0' + asciiLength % 10);
                asciiLength /= 10;
            }
            printedchars.Slice (counter).CopyTo (buffer);
            return printedchars.Length - counter;
        }
#else
        static int WriteLengthAsAscii (Span<byte> buffer, int asciiLength)
        {
            Span<char> asciiChars = stackalloc char[16];
            if (!asciiLength.TryFormat (asciiChars, out int written))
                throw new InvalidOperationException ("Could not write the length of the BEncodedString");

            for (int i = 0; i < written; i++)
                buffer[i] = (byte) asciiChars[i];

            return written;
        }
#endif

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

        public int CompareTo (object? other)
            => CompareTo (other as BEncodedString);

        public int CompareTo (BEncodedString? other)
            => other is null ? 1 : Span.SequenceCompareTo (other.Span);

        public override bool Equals (object? obj)
        {
            if (obj is BEncodedString other)
                return Equals (other);
            if (obj is string str)
                return Equals (new BEncodedString (str));
            return false;
        }

        public bool Equals (BEncodedString? other)
            => !(other is null) && Span.SequenceEqual (other.Span);

        public override int GetHashCode ()
        {
            if (Span.Length >= 4)
                return MemoryMarshal.Read<int> (Span);
            if (Span.Length > 0)
                return Span[0];
            return 0;
        }

        [Obsolete("This wraps HttpUtility.UrlEncode which improperly encodes ' ' as '+' instead of '%20' when cencoding for a query param")]
        public string UrlEncode ()
            => HttpUtility.UrlEncode (Span.ToArray ());

        public string ToHex ()
            => BitConverter.ToString (Span.ToArray ());

        public override string ToString ()
            => Text;
    }
}
