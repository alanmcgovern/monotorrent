using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.BEncoding
{
    static class BEncodeDecoder
    {
        static readonly BEncodedString InfoKey = new BEncodedString ("info");

        internal static BEncodedValue Decode (RawReader reader)
            => Decode (reader, reader.ReadByte ());

        internal static BEncodedDictionary DecodeTorrent (RawReader reader)
        {
            var torrent = new BEncodedDictionary ();
            if (reader.ReadByte () != 'd')
                throw new BEncodingException ("Invalid data found. Aborting"); // Remove the leading 'd'

            int read;
            while ((read = reader.ReadByte ()) != -1 && read != 'e') {
                BEncodedValue value;
                var key = (BEncodedString) Decode (reader, read);         // keys have to be BEncoded strings

                if ((read = reader.ReadByte ()) == 'd') {
                    value = DecodeDictionary (reader, InfoKey.Equals (key));
                } else
                    value = Decode (reader, read);                     // the value is a BEncoded value

                torrent.Add (key, value);
            }

            if (read != 'e')                                    // remove the trailing 'e'
                throw new BEncodingException ("Invalid data found. Aborting");

            return torrent;
        }

        static BEncodedValue Decode (RawReader reader, int read)
        {

            BEncodedValue data;
            switch (read) {
                case ('i'):                         // Integer
                    data = DecodeNumber (reader);
                    break;

                case ('d'):                         // Dictionary
                    data = DecodeDictionary (reader);
                    break;

                case ('l'):                         // List
                    data = DecodeList (reader);
                    break;

                case ('1'):                         // String
                case ('2'):
                case ('3'):
                case ('4'):
                case ('5'):
                case ('6'):
                case ('7'):
                case ('8'):
                case ('9'):
                case ('0'):
                    data = DecodeString (reader, read - '0');
                    break;

                default:
                    throw new BEncodingException ("Could not find what value to decode");
            }

            return data;
        }

        static BEncodedDictionary DecodeDictionary (RawReader reader)
            => DecodeDictionary (reader, reader.StrictDecoding);

        static BEncodedDictionary DecodeDictionary (RawReader reader, bool strictDecoding)
        {
            int read;
            BEncodedString oldkey = null;
            var dictionary = new BEncodedDictionary ();
            while ((read = reader.ReadByte ()) != -1 && read != 'e') {
                BEncodedString key = DecodeString (reader, read - '0');         // keys have to be BEncoded strings

                if (oldkey != null && oldkey.CompareTo (key) > 0)
                    if (strictDecoding)
                        throw new BEncodingException (
                            $"Illegal BEncodedDictionary. The attributes are not ordered correctly. Old key: {oldkey}, New key: {key}");

                oldkey = key;
                BEncodedValue value = Decode (reader);                     // the value is a BEncoded value
                dictionary.Add (key, value);
            }

            if (read != 'e')                                    // remove the trailing 'e'
                throw new BEncodingException ("Invalid data found. Aborting");
            return dictionary;
        }

        static BEncodedList DecodeList (RawReader reader)
        {
            var list = new BEncodedList ();
            int read;
            while ((read = reader.ReadByte ()) != -1 && read != 'e')
                list.Add (Decode (reader, read));

            if (read != 'e')                            // Remove the trailing 'e'
                throw new BEncodingException ("Invalid data found. Aborting");

            return list;
        }

        static BEncodedNumber DecodeNumber (RawReader reader)
        {
            int sign = 1;
            long result = 0;
            int val = reader.ReadByte ();
            if (val == '-') {
                sign = -1;
                val = reader.ReadByte ();
            }

            do {
                if (val < '0' || val > '9')
                    throw new BEncodingException ("Invalid number found.");
                result = result * 10 + (val - '0');
            } while ((val = reader.ReadByte ()) != 'e' && val != -1);

            if (val == -1)        //remove the trailing 'e'
                throw new BEncodingException ("Invalid data found. Aborting.");

            result *= sign;
            return result;
        }

        static BEncodedString DecodeString (RawReader reader, int length)
        {
            int read;
            while ((read = reader.ReadByte ()) != -1 && read != ':') {
                if (read < '0' || read > '9')
                    throw new BEncodingException ($"Invalid BEncodedString. Length was '{length}' instead of a number");
                length = length * 10 + (read - '0');
            }

            if (read != ':')
                throw new BEncodingException ("Invalid data found. Aborting");

            var bytes = new byte[length];
            if (reader.Read (bytes, 0, length) != length)
                throw new BEncodingException ("Couldn't decode string");
            return new BEncodedString (bytes);
        }
    }
}
