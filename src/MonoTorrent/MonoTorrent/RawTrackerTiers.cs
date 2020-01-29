//
// RawTrackerTiers.cs
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
    public class RawTrackerTiers : IList<RawTrackerTier>
    {
        BEncodedList Tiers {
            get; set;
        }

        public RawTrackerTiers ()
            : this (new BEncodedList ())
        {
        }

        public RawTrackerTiers (BEncodedList tiers)
        {
            Tiers = tiers;
        }

        public int IndexOf (RawTrackerTier item)
        {
            if (item != null) {
                for (int i = 0; i < Tiers.Count; i++)
                    if (item.Tier == Tiers[i])
                        return i;
            }
            return -1;
        }

        public void Insert (int index, RawTrackerTier item)
        {
            Tiers.Insert (index, item.Tier);
        }

        public void RemoveAt (int index)
        {
            Tiers.RemoveAt (index);
        }

        public RawTrackerTier this[int index] {
            get => new RawTrackerTier ((BEncodedList) Tiers[index]);
            set => Tiers[index] = value.Tier;
        }

        public void Add (RawTrackerTier item)
        {
            Tiers.Add (item.Tier);
        }

        public void AddRange (IEnumerable<RawTrackerTier> tiers)
        {
            foreach (RawTrackerTier v in tiers)
                Add (v);
        }

        public void Clear ()
        {
            Tiers.Clear ();
        }

        public bool Contains (RawTrackerTier item)
        {
            return IndexOf (item) != -1;
        }

        public void CopyTo (RawTrackerTier[] array, int arrayIndex)
        {
            foreach (RawTrackerTier v in this)
                array[arrayIndex++] = v;
        }

        public bool Remove (RawTrackerTier item)
        {
            int index = IndexOf (item);
            if (index != -1)
                RemoveAt (index);

            return index != -1;
        }

        public int Count => Tiers.Count;

        public bool IsReadOnly => Tiers.IsReadOnly;

        public IEnumerator<RawTrackerTier> GetEnumerator ()
        {
            foreach (BEncodedValue v in Tiers)
                yield return new RawTrackerTier ((BEncodedList) v);
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }
    }
}
