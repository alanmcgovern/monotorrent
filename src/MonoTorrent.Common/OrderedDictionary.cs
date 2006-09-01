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
using System.Collections.Generic;
using System.Text;

/* OrderedDictionary Revision 0.1.1
 * by Alan McGovern.
 * 
 * This is a dictionary that returns the contents in the same order they were inserted in.
*/

namespace MonoTorrent.Common
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class OrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IList<KeyValuePair<TKey, TValue>>
    {
        #region Member Variables
        private List<TKey> keys;
        private List<TValue> values;
        #endregion


        #region Constructors
        /// <summary>
        /// 
        /// </summary>
        public OrderedDictionary()
            : this(8)
        {
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="capacity"></param>
        public OrderedDictionary(int capacity)
        {
            this.keys = new List<TKey>(capacity);
            this.values = new List<TValue>(capacity);
        }
        #endregion


        #region IDictionary and IList methods
        public void Add(TKey key, TValue value)
        {
            this.keys.Add(key);
            this.values.Add(value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            if (this.keys.Contains(item.Key))
                throw new Exception("Key already there");

            this.Add(item.Key, item.Value);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            int keyIndex = this.keys.IndexOf(item.Key);
            int valueIndex = this.values.IndexOf(item.Value);

            if (keyIndex < 0 || valueIndex < 0)
                return false;

            return keyIndex == valueIndex;
        }

        public bool ContainsKey(TKey key)
        {
            return this.keys.Contains(key);
        }

        public void Clear()
        {
            this.keys.Clear();
            this.values.Clear();
        }

        public int Count
        {
            get { return this.keys.Count; }
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public int IndexOf(KeyValuePair<TKey, TValue> item)
        {
            return this.keys.IndexOf(item.Key);
        }

        public void Insert(int index, KeyValuePair<TKey, TValue> item)
        {
            this.keys.Insert(index, item.Key);
            this.values.Insert(index, item.Value);
        }

        public bool IsReadOnly
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        public bool Remove(TKey key)
        {
            return this.keys.Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            int keyIndex = this.keys.IndexOf(item.Key);
            int valueIndex = this.values.IndexOf(item.Value);

            if (keyIndex < 0 || valueIndex < 0 || keyIndex != valueIndex)
                return false;

            this.keys.RemoveAt(keyIndex);
            this.values.RemoveAt(valueIndex);

            return true;
        }

        public void RemoveAt(int index)
        {
            this.keys.RemoveAt(index);
            this.values.RemoveAt(0);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default(TValue);

            int keyIndex = this.keys.IndexOf(key);

            if (keyIndex < 0)
                return false;

            value = this.values[keyIndex];
            return true;
        }

        public ICollection<TKey> Keys
        {
            get { return this.keys; }
        }

        public ICollection<TValue> Values
        {
            get { return this.values; }
        }

        public TValue this[TKey key]
        {
            get
            {
                int keyIndex = this.keys.IndexOf(key);

                if (keyIndex < 0)
                    throw new KeyNotFoundException();

                return this.values[keyIndex];
            }
            set
            {
                int keyIndex = this.keys.IndexOf(key);
                if (keyIndex < 0)
                    throw new KeyNotFoundException();

                this.values[keyIndex] = value;
            }
        }

        public KeyValuePair<TKey, TValue> this[int index]
        {
            get { return new KeyValuePair<TKey, TValue>(this.keys[index], this.values[index]); }
            set
            {
                this.keys[index] = value.Key;
                this.values[index] = value.Value;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (int i = 0; i < this.keys.Count; i++)
                yield return new KeyValuePair<TKey, TValue>(this.keys[i], this.values[i]);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
        #endregion


        #region Overridden Methods
        public override bool Equals(object obj)
        {
            OrderedDictionary<TKey, TValue> dict = obj as OrderedDictionary<TKey, TValue>;
            if (obj == null)
                return false;

            if (this.keys.Count != dict.keys.Count)     // Wrong file size
                return false;

            for (int i = 0; i < this.values.Count; i++)
                if (!this.keys[i].Equals(dict.keys[i]) || !this.values[i].Equals(dict.values[i]))
                    return false;

            return true;
        }

        public override int GetHashCode()
        {
#warning Can someone verify if this is ok?
            long result = 0;
            result += this.keys.GetHashCode();
            result += this.values.GetHashCode();
            return result.GetHashCode();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(128);
            for (int i = 0; i < this.keys.Count; i++)
            {
                sb.Append(this.keys[i].ToString());
                sb.Append(this.values[i].ToString());
            }

            return sb.ToString();
        }
        #endregion
    }
}