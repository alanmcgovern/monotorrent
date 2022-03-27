//
// BEncodeDecoder.Span.cs
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
using System.Security.Cryptography;

namespace MonoTorrent.BEncoding
{
    partial class BEncodeDecoder
    {
        public static BEncodedValue Decode (ref ReadOnlySpan<byte> buffer, bool strictDecoding)
        {
            if (buffer.Length == 0)
                throw new BEncodingException ("Invalid BEncodedValue. The buffer was incomplete");

            switch ((char) buffer[0]) {
                case 'i':
                    buffer = buffer.Slice (1);
                    return DecodeNumber (ref buffer);

                case 'd':
                    buffer = buffer.Slice (1);
                    return DecodeDictionary (ref buffer, strictDecoding);

                case 'l':
                    buffer = buffer.Slice (1);
                    return DecodeList (ref buffer, strictDecoding);

                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '0':
                    return DecodeString (ref buffer);

                default:
                    throw new BEncodingException ("Could not find what value to decode");
            }
        }

        public static (BEncodedDictionary torrent, RawInfoHashes infoHashes) DecodeTorrent (ref ReadOnlySpan<byte> buffer)
        {
            if (buffer[0] != 'd')
                throw new BEncodingException ($"The root value was not a BEncodedDictionary");

            buffer = buffer.Slice (1);
            BEncodedString? oldkey = null;
            ReadOnlySpan<byte> infoBuffer = default;
            Memory<byte> infoHashSHA1 = default;
            Memory<byte> infoHashSHA256 = default;
            var dictionary = new BEncodedDictionary ();
            while (buffer.Length > 0) {
                if (buffer[0] == 'e') {
                    buffer = buffer.Slice (1);
                    return (dictionary, new RawInfoHashes (infoHashSHA1, infoHashSHA256));
                }

                var key = DecodeString (ref buffer);
                if (oldkey != null && oldkey.CompareTo (key) > 0)
                        throw new BEncodingException ($"Illegal BEncodedDictionary. The attributes are not ordered correctly. Old key: {oldkey}, New key: {key}");

                if (InfoKey.Equals (key))
                    infoBuffer = buffer;

                oldkey = key;
                var value = Decode (ref buffer, false);
                dictionary.Add (key, value);

                if (InfoKey.Equals (key)) {
                    using var hasher = SHA1.Create ();
                    using var hasherV2 = SHA256.Create ();
                    infoHashSHA1 = new byte[hasher.HashSize / 8];
                    infoHashSHA256 = new byte[hasherV2.HashSize / 8];
                    if (!hasher.TryComputeHash (infoBuffer.Slice (0, infoBuffer.Length - buffer.Length), infoHashSHA1.Span, out int written) || written != infoHashSHA1.Length)
                        throw new BEncodingException ("Could not compute infohash for torrent.");
                    if (!hasherV2.TryComputeHash (infoBuffer.Slice (0, infoBuffer.Length - buffer.Length), infoHashSHA256.Span, out written) || written != infoHashSHA256.Length)
                        throw new BEncodingException ("Could not compute v2 infohash for torrent.");
                }
            }
            throw new BEncodingException ("Invalid data found. Aborting");
        }

        static BEncodedDictionary DecodeDictionary (ref ReadOnlySpan<byte> buffer, bool strictDecoding)
        {
            BEncodedString? oldkey = null;
            var dictionary = new BEncodedDictionary ();
            while (buffer.Length > 0) {
                if (buffer[0] == 'e') {
                    buffer = buffer.Slice (1);
                    return dictionary;
                }

                var key = DecodeString (ref buffer);

                if (oldkey != null && oldkey.CompareTo (key) > 0)
                    if (strictDecoding)
                        throw new BEncodingException (
                            $"Illegal BEncodedDictionary. The attributes are not ordered correctly. Old key: {oldkey}, New key: {key}");

                oldkey = key;
                var value = Decode (ref buffer, strictDecoding);
                dictionary.Add (key, value);
            }
            throw new BEncodingException ("Invalid data found. Aborting");
        }

        static BEncodedList DecodeList (ref ReadOnlySpan<byte> buffer, bool strictDecoding)
        {
            var list = new BEncodedList ();
            while (buffer.Length > 0) {
                if (buffer[0] == 'e') {
                    buffer = buffer.Slice (1);
                    return list;
                }
                list.Add (Decode (ref buffer, strictDecoding));
            }
            throw new BEncodingException ("Invalid data found. Aborting");
        }

        static BEncodedNumber DecodeNumber (ref ReadOnlySpan<byte> buffer)
        {
            int sign = 1;
            if(buffer [0] == '-') {
                sign = -1;
                buffer = buffer.Slice (1);
            }

            long result = 0;
            for (int i = 0; i < buffer.Length; i++) {
                if (buffer[i] == 'e') {
                    if (i == 0)
                        throw new BEncodingException ("BEncodedNumber did not contain any digits between the 'i' and 'e'");
                    buffer = buffer.Slice (i + 1);
                    return result * sign;
                }
                if (buffer[i] < '0' || buffer[i] > '9')
                    throw new BEncodingException ("Invalid number found.");
                result = result * 10 + (buffer[i] - '0');
            }

            throw new BEncodingException ("Invalid number found.");
        }

        static BEncodedString DecodeString (ref ReadOnlySpan<byte> buffer)
        {
            int length = 0;
            for (int i = 0; i < buffer.Length; i++) {
                if (buffer[i] == (byte) ':') {
                    // Consume the ':' character
                    i++;

                    // Ensure we have enough bytes left
                    if (buffer.Length < (i + length))
                        throw new BEncodingException ($"Invalid BEncodedString. The buffer does not contain at least {length} bytes.");

                    // Copy the data out!
                    var bytes = new byte[length];
                    buffer.Slice (i, length).CopyTo (bytes);
                    buffer = buffer.Slice (i + length);
                    return BEncodedString.FromMemory (bytes);
                }

                if (buffer[i] < (byte) '0' || buffer[i] > (byte) '9')
                    throw new BEncodingException ($"Invalid BEncodedString. Length was '{length}' instead of a number");
                length = length * 10 + (buffer[i] - '0');
            }

            throw new BEncodingException ($"Invalid BEncodedString. The ':' separater was not found.");
        }
    }
}
