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
using System.Numerics;

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Class representing a BEncoded number
    /// </summary>
    public class BEncodedNumber : BEncodedValue, IComparable<BEncodedNumber>
    {
        /// <summary>
        /// The value of the BEncodedNumber
        /// </summary>
        public long Number { get; set; }

        public BEncodedNumber ()
            : this (0)
        {
        }

        /// <summary>
        /// Create a new BEncoded number with the given value
        /// </summary>
        /// <param name="value">The value of the BEncodedNumber</param>
        public BEncodedNumber (long value)
        {
            Number = value;
        }

        public static implicit operator BEncodedNumber (long value)
            => new BEncodedNumber (value);

        /// <summary>
        /// Encodes this number to the supplied byte[] starting at the supplied offset
        /// </summary>
        /// <param name="buffer">The buffer to write the data to</param>
        /// <returns></returns>
        public override int Encode (Span<byte> buffer)
        {
#if NETSTANDARD2_0 || NET472

            // exclude the 'i' and 'e'
            var totalCharacters = LengthInBytes () - 2;
            int written = 0;
            buffer[written++] = (byte) 'i';

            long number = Number;
            if (number < 0) {
                buffer[written++] = (byte) '-';
                totalCharacters--;
            }

            for (int i = 0; i < totalCharacters; i++) {
                buffer[(written + totalCharacters) - i - 1] = (byte) (Math.Abs (number % 10) + '0');
                number /= 10;
            }

            written += totalCharacters;
            buffer[written++] = (byte) 'e';
            return written;
#else
            // int64.MinValue has at most 20 characters
            Span<char> bytes = stackalloc char[20];
            if (!Number.TryFormat (bytes, out int written))
                throw new ArgumentException ("Could not format the BEncodedNumber");
            buffer[0] = (byte) 'i';
            for (int i = 0; i < written; i ++)
                buffer[1 + i] = (byte) bytes[i];
            buffer[1 + written] = (byte) 'e';

            return 2 + written;
#endif
        }

        /// <summary>
        /// Returns the length of the encoded string in bytes
        /// </summary>
        /// <returns></returns>
        public override int LengthInBytes ()
        {
            // Add 2 for the 'i' and 'e'.
            // Special case '0' as we can't Log10 it.
            // For everything else we add 1 to cover the first digit, and then CeilLog10 for the number of 10s we need.
            switch (Number) {
                case 0:
                    return 2 + 1;
                case long.MinValue: // -9223372036854775808
                    return 2 + 20;
                default:
                    return 2 + 1 + (Number < 0 ? 1 : 0) + BitOps.CeilLog10 (Math.Abs (Number));
            }
        }

        public int CompareTo (object? other)
        {
            if (other is BEncodedNumber || other is long || other is int)
                return CompareTo ((BEncodedNumber) other);

            return 1;
        }

        public int CompareTo (BEncodedNumber? other)
            => other == null ? 1 : Number.CompareTo (other.Number);

        public int CompareTo (long other)
            => Number.CompareTo (other);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals (object? obj)
            => obj is BEncodedNumber obj2 ? Number == obj2.Number : false;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode ()
            => Number.GetHashCode ();

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString ()
            => Number.ToString ();
    }
}
