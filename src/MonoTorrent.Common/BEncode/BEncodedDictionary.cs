//
// System.String.cs
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
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Common
{
    /// <summary>
    /// Class representing a BEncoded Dictionary
    /// </summary>
    public class BEncodedDictionary : IBEncodedValue, IDictionary<BEncodedString, IBEncodedValue>
    {
        #region Member Variables
        private SortedDictionary<BEncodedString, IBEncodedValue> dictionary;
        #endregion


        #region Constructors
        /// <summary>
        /// Create a new BEncodedDictionary
        /// </summary>
        public BEncodedDictionary()
            : this(new SortedDictionary<BEncodedString, IBEncodedValue>())
        {
        }

        public BEncodedDictionary(SortedDictionary<BEncodedString, IBEncodedValue> dictionary)
        {
            this.dictionary = dictionary;
        }
        #endregion


        #region Encode/Decode Methods
        /// <summary>
        /// Encodes the list to a byte[] using UTF8 Encoding
        /// </summary>
        /// <returns></returns>
        public byte[] Encode()
        {
            return this.Encode(new UTF8Encoding(false, false));
        }

        /// <summary>
        /// Encodes the list to a byte[] using the supplied encoding
        /// </summary>
        /// <returns></returns>
        public byte[] Encode(Encoding e)
        {
            byte[] buffer = new byte[this.LengthInBytes(e)];
            this.Encode(buffer, 0, e);
            return buffer;
        }

        /// <summary>
        /// Encodes the list to a byte[] using UTF8 Encoding
        /// </summary>
        /// <param name="buffer">The buffer to encode the data to</param>
        /// <param name="offset">The offset to start writing the data to</param>
        /// <returns></returns>
        public int Encode(byte[] buffer, int offset)
        {
            return this.Encode(buffer, offset, new UTF8Encoding(false, false));
        }

        /// <summary>
        /// Encodes the dictionary to a byte[] using the supplied encoding
        /// </summary>
        /// <param name="buffer">The buffer to encode the data to</param>
        /// <param name="offset">The offset to start writing the data to</param>
        /// <param name="e">The encoding to use</param>
        /// <returns></returns>
        public int Encode(byte[] buffer, int offset, Encoding e)
        {
            int written = 0;

            //Dictionaries start with 'd'
            written += e.GetBytes("d", 0, 1, buffer, offset);

            foreach (KeyValuePair<BEncodedString, IBEncodedValue> keypair in this)
            {
                written += keypair.Key.Encode(buffer, offset + written, e);
                written += keypair.Value.Encode(buffer, offset + written, e);
            }

            // Dictionaries end with 'e'
            written += e.GetBytes("e", 0, 1, buffer, offset + written);                 
            return written;
        }

        /// <summary>
        /// Decodes a BEncodedDictionary from the supplied StreamReader
        /// </summary>
        /// <param name="reader"></param>
        public void Decode(BinaryReader reader)
        {
            BEncodedString key = null;
            IBEncodedValue value = null;
            BEncodedString oldkey = null;

            try
            {
                if (reader.ReadByte() != 'd')
                    throw new BEncodingException("Invalid data found. Aborting"); // Remove the leading 'd'

                while ((reader.PeekChar() != -1) && ((char)reader.PeekChar() != 'e'))
                {
                    key = (BEncodedString)BEncode.Decode(reader); ;     // keys have to be BEncoded strings
                    if (oldkey != null)
                        if (string.Compare(oldkey.Text, key.Text) > 0)
                            throw new BEncodingException("Illegal BEncodedDictionary. The attributes are not ordered correctly");
                    oldkey = key;
                    
                    value = BEncode.Decode(reader);                     // the value is a BEncoded value
                    dictionary.Add(key.Text, value);
                }

                if (reader.ReadByte() != 'e')                                    // remove the trailing 'e'
                    throw new BEncodingException("Invalid data found. Aborting");
            }
            catch (BEncodingException ex)
            {
                throw new BEncodingException("Couldn't decode dictionary", ex);
            }
            catch(Exception ex)
            {
                throw new BEncodingException("Couldn't decode dictionary", ex);
            }
        }
        #endregion


        #region Helper Methods
        /// <summary>
        /// Returns the size of the dictionary in bytes using UTF8 encoding
        /// </summary>
        /// <returns></returns>
        public int LengthInBytes()
        {
            return this.LengthInBytes(new UTF8Encoding(false, false));
        }

        /// <summary>
        /// Returns the size of the list in bytes using the supplied encoding
        /// </summary>
        /// <param name="e">The encoding to use</param>
        /// <returns></returns>
        public int LengthInBytes(Encoding e)
        {
            int length = 0;
            length += e.GetByteCount("d");   // Dictionaries start with 'd'

            foreach (KeyValuePair<BEncodedString, IBEncodedValue> keypair in this.dictionary)
            {
                length += keypair.Key.LengthInBytes(e);
                length += keypair.Value.LengthInBytes(e);
            }
            length += e.GetByteCount("e");   // Dictionaries end with 'e'
            return length;
        }
        #endregion


        #region Overridden Methods
        public override bool Equals(object obj)
        {
            IBEncodedValue val;
            BEncodedDictionary dict = obj as BEncodedDictionary;
            if (dict == null)
                return false;

            if (this.dictionary.Count != dict.dictionary.Count)
                return false;

            foreach (KeyValuePair<BEncodedString, IBEncodedValue> keypair in this.dictionary)
            {
                if (!dict.TryGetValue(keypair.Key, out val))
                    return false;

                if (keypair.Value != val)
                    return false;
            }
            //for (int i = 0; i < this.dictionary.Count; i++)
            //    if (this.dictionary[i].Key != dict.dictionary[i].Key || this.dictionary[i].Value != dict.dictionary[i].Value)
            //        return false;

            return true;
        }

        public override int GetHashCode()
        {
            return this.dictionary.GetHashCode();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(32);

            foreach (KeyValuePair<BEncodedString, IBEncodedValue> keypair in dictionary)
            {
                sb.Append(keypair.Key.ToString());
                sb.Append(keypair.Value.ToString());
            }

            return sb.ToString();
        }
        #endregion


        #region IDictionary and IList methods
        public void Add(BEncodedString key, IBEncodedValue value)
        {
            this.dictionary.Add(key, value);
        }

        public void Add(KeyValuePair<BEncodedString, IBEncodedValue> keypair)
        {
            this.dictionary.Add(keypair.Key, keypair.Value);
        }
        public void Clear()
        {
            this.dictionary.Clear();
        }

        public bool Contains(KeyValuePair<BEncodedString, IBEncodedValue> item)
        {
            if (!this.dictionary.ContainsKey(item.Key))
                return false;

            return this.dictionary[item.Key].Equals(item.Value);
        }

        public bool ContainsKey(BEncodedString key)
        {
            return this.dictionary.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<BEncodedString, IBEncodedValue>[] array, int arrayIndex)
        {
            this.dictionary.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return this.dictionary.Count; }
        }

        //public int IndexOf(KeyValuePair<BEncodedString, IBEncodedValue> item)
        //{
        //    return this.dictionary.IndexOf(item);
        //}

        //public void Insert(int index, KeyValuePair<BEncodedString, IBEncodedValue> item)
        //{
        //    this.dictionary.Insert(index, item);
        //}

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(BEncodedString key)
        {
            return this.dictionary.Remove(key);
        }

        public bool Remove(KeyValuePair<BEncodedString, IBEncodedValue> item)
        {
            return this.dictionary.Remove(item.Key);
        }

        //public void RemoveAt(int index)
        //{
        //    this.dictionary.RemoveAt(index);
        //}

        public bool TryGetValue(BEncodedString key, out IBEncodedValue value)
        {
            return this.dictionary.TryGetValue(key, out value);
        }

        public IBEncodedValue this[BEncodedString key]
        {
            get { return this.dictionary[key]; }
            set { this.dictionary[key] = value; }
        }

        //public KeyValuePair<BEncodedString, IBEncodedValue> this[int index]
        //{
        //    get { return this.dictionary[index]; }
        //    set { this.dictionary[index] = value; }
        //}

        public ICollection<BEncodedString> Keys
        {
            get { return this.dictionary.Keys; }
        }

        public ICollection<IBEncodedValue> Values
        {
            get { return this.dictionary.Values; }
        }

        public IEnumerator<KeyValuePair<BEncodedString, IBEncodedValue>> GetEnumerator()
        {
            return this.dictionary.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.dictionary.GetEnumerator();
        }
        #endregion
    }
}