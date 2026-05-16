//
// BEncodeDecoder.Stream.cs
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
using System.Buffers;
using System.IO;
using System.Security.Cryptography;

namespace MonoTorrent.BEncoding
{
    static partial class BEncodeDecoder
    {
        static readonly BEncodedString InfoKey = new BEncodedString ("info");

        internal static BEncodedValue Decode (Stream reader, bool strictDecoding)
        {
            using (var pool = new BEncodedStringPool (MemoryPool<byte>.Shared.Rent (128)))
                return Decode (reader, pool, strictDecoding, reader.ReadByte ());
        }

        internal static (BEncodedDictionary torrent, RawInfoHashes infohashes) DecodeTorrent (Stream reader)
        {
            using (var pool = new BEncodedStringPool (MemoryPool<byte>.Shared.Rent (128)))
                return DecodeTorrent (reader, pool);
        }

        static BEncodedValue Decode (Stream reader, BEncodedStringPool pool, bool strictDecoding)
            => Decode (reader, pool, strictDecoding, reader.ReadByte ());

        static (BEncodedDictionary torrent, RawInfoHashes infohashes) DecodeTorrent (Stream reader, BEncodedStringPool pool)
        {
            var torrent = new BEncodedDictionary ();
            if (reader.ReadByte () != 'd')
                throw new BEncodingException ("Invalid data found. Aborting"); // Remove the leading 'd'

            int read;
            byte[]? infohashSHA1 = null;
            byte[]? infohashSHA256 = null;
            while ((read = reader.ReadByte ()) != -1) {
                if (read == 'e')
                    return (torrent, new RawInfoHashes (infohashSHA1, infohashSHA256));

                if (read < '0' || read > '9')
                    throw new BEncodingException ("Invalid key length");

                BEncodedValue value;
                var key = (BEncodedString) Decode (reader, pool, false, read);         // keys have to be BEncoded strings

                if ((read = reader.ReadByte ()) == 'd') {
                    if (InfoKey.Equals (key)) {
                        using var sha1Reader = new HashingReader (reader, (byte) 'd', IncrementalHash.CreateHash (HashAlgorithmName.SHA1));
                        using var sha256Reader = new HashingReader (sha1Reader, (byte) 'd', IncrementalHash.CreateHash (HashAlgorithmName.SHA256));
                        value = DecodeDictionary (sha256Reader, pool, false);
                        infohashSHA1 = sha1Reader.TransformFinalBlock ();
                        infohashSHA256 = sha256Reader.TransformFinalBlock ();
                    } else {
                        value = DecodeDictionary (reader, pool, false);
                    }
                } else {
                    value = Decode (reader, pool, false, read);
                }
                torrent.Add (key, value);
            }

            throw new BEncodingException ("Invalid data found. Aborting");
        }

        static BEncodedValue Decode (Stream reader, BEncodedStringPool pool, bool strictDecoding, int read)
        {
            switch (read) {
                case 'i':
                    return DecodeNumber (reader, pool);

                case 'd':
                    return DecodeDictionary (reader, pool, strictDecoding);

                case 'l':
                    return DecodeList (reader, pool, strictDecoding);

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
                    return DecodeString (reader, pool, read - '0');

                default:
                    throw new BEncodingException ("Could not find what value to decode");
            }
        }

        static BEncodedDictionary DecodeDictionary (Stream reader, BEncodedStringPool pool, bool strictDecoding)
        {
            int read;
            BEncodedString? oldkey = null;
            var dictionary = new BEncodedDictionary ();
            while ((read = reader.ReadByte ()) != -1) {
                if (read == 'e')
                    return dictionary;

                if (read < '0' || read > '9')
                    throw new BEncodingException ("Invalid key length");

                var key = DecodeString (reader, pool, read - '0');         // keys have to be BEncoded strings

                if (oldkey != null && oldkey.CompareTo (key) > 0)
                    if (strictDecoding)
                        throw new BEncodingException (
                            $"Illegal BEncodedDictionary. The attributes are not ordered correctly. Old key: {oldkey}, New key: {key}");

                oldkey = key;
                var value = Decode (reader, pool, strictDecoding);                     // the value is a BEncoded value
                dictionary.Add (key, value);
            }

            throw new BEncodingException ("Invalid data found. Aborting");
        }

        static BEncodedList DecodeList (Stream reader, BEncodedStringPool pool, bool strictDecoding)
        {
            var list = new BEncodedList ();
            int read;
            while ((read = reader.ReadByte ()) != -1) {
                if (read == 'e')
                    return list;
                list.Add (Decode (reader, pool, strictDecoding, read));
            }

            throw new BEncodingException ("Invalid data found. Aborting");
        }

        static BEncodedNumber DecodeNumber (Stream reader, BEncodedStringPool pool)
        {
            int sign = 1;
            long result = 0;
            int val = reader.ReadByte ();
            if (val == '-') {
                sign = -1;
                val = reader.ReadByte ();
            }

            var readValue = false;
            do {
                if (val == 'e') {
                    if (!readValue)
                        throw new BEncodingException ("BEncodedNumber did not contain any digits between the 'i' and 'e'");
                    return result * sign;
                }
                if (val < '0' || val > '9')
                    throw new BEncodingException ("Invalid number found.");
                if ((sign == -1 || readValue) && result == 0 && val == '0')
                    throw new BEncodingException ("Invalid number found. The invalid number is negative zero or negative leading zero.");
                result = result * 10 + (val - '0');
                readValue = true;
            } while ((val = reader.ReadByte ()) != -1);

            throw new BEncodingException ("Invalid data found. Aborting.");
        }

        static BEncodedString DecodeString (Stream reader, BEncodedStringPool pool, int length)
        {
            int read;
            while ((read = reader.ReadByte ()) != -1) {
                if (read == ':')
                    return pool.GetInternedOrCreateNew (reader, length);

                if (read < '0' || read > '9')
                    throw new BEncodingException ($"Invalid BEncodedString. Length was '{(char)read}' instead of a number");
                if (length == 0)
                    throw new BEncodingException ($"Invalid BEncodedString. The length was prefixed with '0'.");

                length = length * 10 + (read - '0');
                if (length < 0)
                    throw new BEncodingException ($"Invalid BEncodedString. Length overflowed the size of an int64");
            }

            throw new BEncodingException ("Invalid data found. Aborting");
        }
    }
}
