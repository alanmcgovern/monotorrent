//
// BEncodedDictionary.cs
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
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Class representing a BEncoded Dictionary
    /// </summary>
    public class BEncodedDictionary : BEncodedValue, IDictionary<BEncodedString, BEncodedValue>
    {
        /// <summary>
        /// Special decoding method for torrent files. This mode will ensure the correct infohash is generated
        /// for torrents which contain dictionaries with misordered keys.
        /// </summary>
        /// <returns></returns>
        public static (BEncodedDictionary torrent, RawInfoHashes infoHashes) DecodeTorrent (ReadOnlySpan<byte> buffer)
            => BEncodeDecoder.DecodeTorrent (ref buffer);

        /// <summary>
        /// Special decoding method for torrent files. This mode will ensure the correct infohash is generated
        /// for torrents which contain dictionaries with misordered keys.
        /// </summary>
        /// <returns></returns>
        public static (BEncodedDictionary torrent, RawInfoHashes infohashes) DecodeTorrent (Stream stream)
            => BEncodeDecoder.DecodeTorrent (stream);

        readonly SortedList<BEncodedString, BEncodedValue> dictionary;

        /// <summary>
        /// Create a new BEncodedDictionary
        /// </summary>
        public BEncodedDictionary ()
        {
            dictionary = new SortedList<BEncodedString, BEncodedValue> ();
        }

        /// <summary>
        /// Encodes the dictionary to a byte[]
        /// </summary>
        /// <param name="buffer">The buffer to encode the data to</param>
        /// <returns></returns>
        public override int Encode (Span<byte> buffer)
        {
            //Dictionaries start with 'd'
            buffer[0] = (byte) 'd';
            int written = 1;

            for (int i = 0; i < dictionary.Keys.Count; i++) {
                written += dictionary.Keys[i].Encode (buffer.Slice (written));
                written += dictionary.Values[i].Encode (buffer.Slice (written));
            }

            // Dictionaries end with 'e'
            buffer[written ++] = (byte) 'e';
            return written;
        }

        /// <summary>
        /// Returns the size of the dictionary in bytes using UTF8 encoding
        /// </summary>
        /// <returns></returns>
        public override int LengthInBytes ()
        {
            int length = 2; // Account for the prefix/suffix

            for (int i = 0; i < dictionary.Keys.Count; i++) {
                length += dictionary.Keys[i].LengthInBytes ();
                length += dictionary.Values[i].LengthInBytes ();
            }

            return length;
        }

        public override bool Equals (object? obj)
        {
            if (!(obj is BEncodedDictionary other))
                return false;

            if (dictionary.Count != other.dictionary.Count)
                return false;

            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dictionary) {
                if (!other.TryGetValue (keypair.Key, out BEncodedValue? val))
                    return false;

                if (!keypair.Value.Equals (val))
                    return false;
            }

            return true;
        }

        public override int GetHashCode ()
        {
            int result = 0;
            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dictionary) {
                result ^= keypair.Key.GetHashCode ();
                result ^= keypair.Value.GetHashCode ();
            }

            return result;
        }

        public override string ToString ()
            => $"BEncodedDictionary [{Count} items]";

        public void Add (BEncodedString key, BEncodedValue value)
        {
            dictionary.Add (key, value);
        }

        public void Add (KeyValuePair<BEncodedString, BEncodedValue> item)
        {
            dictionary.Add (item.Key, item.Value);
        }
        public void Clear ()
        {
            dictionary.Clear ();
        }

        public bool Contains (KeyValuePair<BEncodedString, BEncodedValue> item)
        {
            if (!dictionary.ContainsKey (item.Key))
                return false;

            return dictionary[item.Key].Equals (item.Value);
        }

        public bool ContainsKey (BEncodedString key)
        {
            return dictionary.ContainsKey (key);
        }

        public void CopyTo (KeyValuePair<BEncodedString, BEncodedValue>[] array, int arrayIndex)
        {
            foreach (var item in dictionary)
                array[arrayIndex++] = new KeyValuePair<BEncodedString, BEncodedValue> (item.Key, item.Value);
        }

        public int Count => dictionary.Count;

        public BEncodedValue? GetValueOrDefault (BEncodedString key)
        {
            return GetValueOrDefault (key, null);
        }

        public BEncodedValue? GetValueOrDefault (BEncodedString key, BEncodedValue? defaultValue)
        {
            return dictionary.TryGetValue (key, out BEncodedValue? value) ? value : defaultValue;
        }

        public bool IsReadOnly => false;

        public bool Remove (BEncodedString key)
        {
            return dictionary.Remove (key);
        }

        public bool Remove (KeyValuePair<BEncodedString, BEncodedValue> item)
        {
            return dictionary.Remove (item.Key);
        }

#pragma warning disable 8767
        public bool TryGetValue (BEncodedString key, [MaybeNullWhen (false)] out BEncodedValue value)
#pragma warning restore 8767
        {
            return dictionary.TryGetValue (key, out value);
        }

        public BEncodedValue this[BEncodedString key] {
            get => dictionary[key];
            set => dictionary[key] = value;
        }

        public ICollection<BEncodedString> Keys => dictionary.Keys;

        public ICollection<BEncodedValue> Values => dictionary.Values;

        public IEnumerator<KeyValuePair<BEncodedString, BEncodedValue>> GetEnumerator ()
        {
            return dictionary.GetEnumerator ();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
        {
            return dictionary.GetEnumerator ();
        }
    }
}
