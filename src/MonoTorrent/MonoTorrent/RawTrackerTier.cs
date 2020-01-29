//
// RawTrackerTier.cs
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


using System.Collections;
using System.Collections.Generic;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    public class RawTrackerTier : IList<string>
    {
        public string this[int index] {
            get => ((BEncodedString) Tier[index]).Text;
            set => Tier[index] = new BEncodedString (value);
        }

        internal BEncodedList Tier {
            get; set;
        }

        public RawTrackerTier ()
            : this (new BEncodedList ())
        {
        }

        public RawTrackerTier (BEncodedList tier)
        {
            Tier = tier;
        }

        public RawTrackerTier (IEnumerable<string> announces)
            : this ()
        {
            foreach (string v in announces)
                Add (v);
        }

        public int IndexOf (string item)
        {
            return Tier.IndexOf ((BEncodedString) item);
        }

        public void Insert (int index, string item)
        {
            Tier.Insert (index, (BEncodedString) item);
        }

        public void RemoveAt (int index)
        {
            Tier.RemoveAt (index);
        }

        public void Add (string item)
        {
            Tier.Add ((BEncodedString) item);
        }

        public void Clear ()
        {
            Tier.Clear ();
        }

        public bool Contains (string item)
        {
            return Tier.Contains ((BEncodedString) item);
        }

        public void CopyTo (string[] array, int arrayIndex)
        {
            foreach (string s in this)
                array[arrayIndex++] = s;
        }

        public bool Remove (string item)
        {
            return Tier.Remove ((BEncodedString) item);
        }

        public int Count => Tier.Count;

        public bool IsReadOnly => Tier.IsReadOnly;

        public IEnumerator<string> GetEnumerator ()
        {
            foreach (BEncodedString v in Tier)
                yield return v.Text;
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }
    }
}
