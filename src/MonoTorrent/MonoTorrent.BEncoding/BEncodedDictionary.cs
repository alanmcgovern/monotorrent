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


using System.Collections.Generic;
using System.IO;

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Class representing a BEncoded Dictionary
    /// </summary>
    public class BEncodedDictionary : BEncodedValue, IDictionary<BEncodedString, BEncodedValue>
    {
        #region Member Variables

        readonly SortedList<BEncodedString, BEncodedValue> dictionary;

        #endregion


        #region Constructors

        /// <summary>
        /// Create a new BEncodedDictionary
        /// </summary>
        public BEncodedDictionary ()
        {
            dictionary = new SortedList<BEncodedString, BEncodedValue> ();
        }

        #endregion


        #region Encode/Decode Methods

        /// <summary>
        /// Encodes the dictionary to a byte[]
        /// </summary>
        /// <param name="buffer">The buffer to encode the data to</param>
        /// <param name="offset">The offset to start writing the data to</param>
        /// <returns></returns>
        public override int Encode (byte[] buffer, int offset)
        {
            int written = 0;

            //Dictionaries start with 'd'
            buffer[offset] = (byte) 'd';
            written++;

            foreach (var keypair in dictionary) {
                written += keypair.Key.Encode (buffer, offset + written);
                written += keypair.Value.Encode (buffer, offset + written);
            }

            // Dictionaries end with 'e'
            buffer[offset + written] = (byte) 'e';
            written++;
            return written;
        }

        public static BEncodedDictionary DecodeTorrent (byte[] bytes)
        {
            return DecodeTorrent (new MemoryStream (bytes));
        }

        public static BEncodedDictionary DecodeTorrent (Stream s)
        {
            return DecodeTorrent (new RawReader (s));
        }


        /// <summary>
        /// Special decoding method for torrent files - allows dictionary attributes to be out of order for the
        /// overall torrent file, but imposes strict rules on the info dictionary.
        /// </summary>
        /// <returns></returns>
        public static BEncodedDictionary DecodeTorrent (RawReader reader)
        {
            return BEncodeDecoder.DecodeTorrent (reader);
        }

        #endregion


        #region Helper Methods

        /// <summary>
        /// Returns the size of the dictionary in bytes using UTF8 encoding
        /// </summary>
        /// <returns></returns>
        public override int LengthInBytes ()
        {
            int length = 2; // Account for the prefix/suffix

            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dictionary) {
                length += keypair.Key.LengthInBytes ();
                length += keypair.Value.LengthInBytes ();
            }

            return length;
        }

        #endregion


        #region Overridden Methods
        public override bool Equals (object obj)
        {
            if (!(obj is BEncodedDictionary other))
                return false;

            if (dictionary.Count != other.dictionary.Count)
                return false;

            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dictionary) {
                if (!other.TryGetValue (keypair.Key, out BEncodedValue val))
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
        {
            return System.Text.Encoding.UTF8.GetString (Encode ());
        }
        #endregion


        #region IDictionary and IList methods
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

        public BEncodedValue GetValueOrDefault (BEncodedString key)
        {
            return GetValueOrDefault (key, null);
        }

        public BEncodedValue GetValueOrDefault (BEncodedString key, BEncodedValue defaultValue)
        {
            return dictionary.TryGetValue (key, out BEncodedValue value) ? value : defaultValue;
        }

        //public int IndexOf(KeyValuePair<BEncodedString, IBEncodedValue> item)
        //{
        //    return this.dictionary.IndexOf(item);
        //}

        //public void Insert(int index, KeyValuePair<BEncodedString, IBEncodedValue> item)
        //{
        //    this.dictionary.Insert(index, item);
        //}

        public bool IsReadOnly => false;

        public bool Remove (BEncodedString key)
        {
            return dictionary.Remove (key);
        }

        public bool Remove (KeyValuePair<BEncodedString, BEncodedValue> item)
        {
            return dictionary.Remove (item.Key);
        }

        public bool TryGetValue (BEncodedString key, out BEncodedValue value)
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
        #endregion
    }
}