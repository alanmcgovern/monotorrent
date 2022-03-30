//
// BEncodedList.cs
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

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Class representing a BEncoded list
    /// </summary>
    public class BEncodedList : BEncodedValue, IList<BEncodedValue>
    {
        readonly List<BEncodedValue> list;

        /// <summary>
        /// Create a new BEncoded List with default capacity
        /// </summary>
        public BEncodedList ()
            : this (new List<BEncodedValue> ())
        {
        }

        /// <summary>
        /// Create a new BEncoded List with the supplied capacity
        /// </summary>
        /// <param name="capacity">The initial capacity</param>
        public BEncodedList (int capacity)
            : this (new List<BEncodedValue> (capacity))
        {

        }

        public BEncodedList (IEnumerable<BEncodedValue> list)
        {
            if (list == null)
                throw new ArgumentNullException (nameof (list));

            this.list = new List<BEncodedValue> (list);
        }

        BEncodedList (List<BEncodedValue> value)
        {
            list = value;
        }

        /// <summary>
        /// Encodes the list to a byte[]
        /// </summary>
        /// <param name="buffer">The buffer to encode the list to</param>
        /// <returns></returns>
        public override int Encode (Span<byte> buffer)
        {
            buffer[0] = (byte) 'l';
            int written = 1;
            for (int i = 0; i < list.Count; i++)
                written += list[i].Encode (buffer.Slice (written));
            buffer[written++] = (byte) 'e';
            return written;
        }

        /// <summary>
        /// Returns the size of the list in bytes
        /// </summary>
        /// <returns></returns>
        public override int LengthInBytes ()
        {
            int length = 2; // account for the prefix/suffix

            for (int i = 0; i < list.Count; i++)
                length += list[i].LengthInBytes ();

            return length;
        }

        public override bool Equals (object? obj)
        {
            if (!(obj is BEncodedList other))
                return false;

            for (int i = 0; i < list.Count; i++)
                if (!list[i].Equals (other.list[i]))
                    return false;

            return true;
        }

        public override int GetHashCode ()
        {
            int result = 0;
            for (int i = 0; i < list.Count; i++)
                result ^= list[i].GetHashCode ();
            return result;
        }

        public override string ToString ()
            => $"BEncodedList [{list.Count} items]";

        public void Add (BEncodedValue item)
        {
            list.Add (item);
        }

        public void AddRange (IEnumerable<BEncodedValue> collection)
        {
            list.AddRange (collection);
        }

        public void Clear ()
        {
            list.Clear ();
        }

        public bool Contains (BEncodedValue item)
        {
            return list.Contains (item);
        }

        public void CopyTo (BEncodedValue[] array, int arrayIndex)
        {
            list.CopyTo (array, arrayIndex);
        }

        public int Count => list.Count;

        public int IndexOf (BEncodedValue item)
        {
            return list.IndexOf (item);
        }

        public void Insert (int index, BEncodedValue item)
        {
            list.Insert (index, item);
        }

        public bool IsReadOnly => false;

        public bool Remove (BEncodedValue item)
        {
            return list.Remove (item);
        }

        public void RemoveAt (int index)
        {
            list.RemoveAt (index);
        }

        public BEncodedValue this[int index] {
            get => list[index];
            set => list[index] = value;
        }

        public IEnumerator<BEncodedValue> GetEnumerator ()
        {
            return list.GetEnumerator ();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }
    }
}
