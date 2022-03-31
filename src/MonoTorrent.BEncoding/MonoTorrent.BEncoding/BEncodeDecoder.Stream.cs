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
using System.IO;
using System.Security.Cryptography;

namespace MonoTorrent.BEncoding
{
    static partial class BEncodeDecoder
    {
        static readonly BEncodedString InfoKey = new BEncodedString ("info");

        public static BEncodedValue Decode (Stream reader, bool strictDecoding)
            => Decode (reader, strictDecoding, reader.ReadByte ());

        public static (BEncodedDictionary torrent, RawInfoHashes infohashes) DecodeTorrent (Stream reader)
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
                var key = (BEncodedString) Decode (reader, false, read);         // keys have to be BEncoded strings

                if ((read = reader.ReadByte ()) == 'd') {
                    if (InfoKey.Equals (key)) {
                        using var sha1Reader = new HashingReader (reader, (byte) 'd', SHA1.Create ());
                        using var sha256Reader = new HashingReader (sha1Reader, (byte) 'd', SHA256.Create ());
                        value = DecodeDictionary (sha256Reader, false);
                        infohashSHA1 = sha1Reader.TransformFinalBlock ();
                        infohashSHA256 = sha256Reader.TransformFinalBlock ();
                    } else {
                        value = DecodeDictionary (reader, false);
                    }
                } else {
                    value = Decode (reader, false, read);
                }
                torrent.Add (key, value);
            }

            throw new BEncodingException ("Invalid data found. Aborting");
        }

        static BEncodedValue Decode (Stream reader, bool strictDecoding, int read)
        {
            switch (read) {
                case 'i':
                    return DecodeNumber (reader);

                case 'd':
                    return DecodeDictionary (reader, strictDecoding);

                case 'l':
                    return DecodeList (reader, strictDecoding);

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
                    return DecodeString (reader, read - '0');

                default:
                    throw new BEncodingException ("Could not find what value to decode");
            }
        }

        static BEncodedDictionary DecodeDictionary (Stream reader, bool strictDecoding)
        {
            int read;
            BEncodedString? oldkey = null;
            var dictionary = new BEncodedDictionary ();
            while ((read = reader.ReadByte ()) != -1) {
                if (read == 'e')
                    return dictionary;

                if (read < '0' || read > '9')
                    throw new BEncodingException ("Invalid key length");

                var key = DecodeString (reader, read - '0');         // keys have to be BEncoded strings

                if (oldkey != null && oldkey.CompareTo (key) > 0)
                    if (strictDecoding)
                        throw new BEncodingException (
                            $"Illegal BEncodedDictionary. The attributes are not ordered correctly. Old key: {oldkey}, New key: {key}");

                oldkey = key;
                var value = Decode (reader, strictDecoding);                     // the value is a BEncoded value
                dictionary.Add (key, value);
            }

            throw new BEncodingException ("Invalid data found. Aborting");
        }

        static BEncodedList DecodeList (Stream reader, bool strictDecoding)
        {
            var list = new BEncodedList ();
            int read;
            while ((read = reader.ReadByte ()) != -1) {
                if (read == 'e')
                    return list;
                list.Add (Decode (reader, strictDecoding, read));
            }

            throw new BEncodingException ("Invalid data found. Aborting");
        }

        static BEncodedNumber DecodeNumber (Stream reader)
        {
            int sign = 1;
            long result = 0;
            int val = reader.ReadByte ();
            if (val == '-') {
                sign = -1;
                val = reader.ReadByte ();
            }

            if (val == 'e')
                throw new BEncodingException ("Invalid data found. Aborting.");

            do {
                if (val == 'e')
                    return result * sign;
                if (val < '0' || val > '9')
                    throw new BEncodingException ("Invalid number found.");
                result = result * 10 + (val - '0');
            } while ((val = reader.ReadByte ()) != -1);

            throw new BEncodingException ("Invalid data found. Aborting.");
        }

        static BEncodedString DecodeString (Stream reader, int length)
        {
            int read;
            while ((read = reader.ReadByte ()) != -1) {
                if (read == ':') {
                    var bytes = new byte[length];
                    if (reader.Read (bytes, 0, length) != length)
                        throw new BEncodingException ("Couldn't decode string");
                    return BEncodedString.FromMemory (bytes);
                }

                if (read < '0' || read > '9')
                    throw new BEncodingException ($"Invalid BEncodedString. Length was '{length}' instead of a number");
                length = length * 10 + (read - '0');
            }

            throw new BEncodingException ("Invalid data found. Aborting");
        }
    }
}
