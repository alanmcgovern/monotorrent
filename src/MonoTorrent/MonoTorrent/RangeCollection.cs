//
// RangeCollection.cs
//
// Author:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2009 Alan McGovern.
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

namespace MonoTorrent.Client
{
    struct RangeComparer : IComparer<AddressRange>
    {
        public int Compare (AddressRange x, AddressRange y)
        {
            return x.Start.CompareTo (y.Start);
        }
    }

    class RangeCollection
    {
        public int Count => Ranges.Count;

        internal List<AddressRange> Ranges { get; } = new List<AddressRange> ();

        public void Add (AddressRange item)
        {
            int index;
            if (Ranges.Count == 0 || item.Start > Ranges[Ranges.Count - 1].Start) {
                index = Ranges.Count;
            } else {
                index = Ranges.BinarySearch (item, new RangeComparer ());
                if (index < 0)
                    index = ~index;
            }
            bool mergedLeft = MergeLeft (item, index);
            bool mergedRight = MergeRight (item, index);

            if (mergedLeft || mergedRight) {
                if (index > 0)
                    index--;

                while ((index + 1) < Ranges.Count) {
                    if (Ranges[index].End > Ranges[index + 1].Start || Ranges[index].End + 1 == Ranges[index + 1].Start) {
                        Ranges[index] = new AddressRange (Ranges[index].Start, Math.Max (Ranges[index].End, Ranges[index + 1].End));
                        Ranges.RemoveAt (index + 1);
                    } else
                        break;
                }
            } else {
                Ranges.Insert (index, item);
            }
        }

        public void AddRange (IEnumerable<AddressRange> ranges)
        {
            var list = new List<AddressRange> (ranges);
            list.Sort ((x, y) => x.Start.CompareTo (y.Start));

            foreach (AddressRange r in list)
                Add (new AddressRange (r.Start, r.End));
        }

        bool MergeLeft (AddressRange range, int position)
        {
            if (position > 0)
                position--;
            if (Ranges.Count > position && position >= 0) {
                AddressRange leftRange = Ranges[position];
                if (leftRange.Contains (range.Start)) {
                    Ranges[position] = new AddressRange (leftRange.Start, Math.Max (leftRange.End, range.End));
                    return true;
                } else if (leftRange.End + 1 == range.Start) {
                    Ranges[position] = new AddressRange (leftRange.Start, range.End);
                    return true;
                } else if (leftRange.Start - 1 == range.End) {
                    Ranges[position] = new AddressRange (range.Start, leftRange.End);
                    return true;
                }
            }
            return false;
        }

        bool MergeRight (AddressRange range, int position)
        {
            if (position == Ranges.Count)
                position--;
            if (position >= 0 && position < Ranges.Count) {
                AddressRange rightRange = Ranges[position];
                if (rightRange.Contains (range.End)) {
                    Ranges[position] = new AddressRange (Math.Min (range.Start, rightRange.Start), rightRange.End);
                    return true;
                } else if (range.Contains (rightRange)) {
                    Ranges[position] = range;
                    return true;
                } else if (rightRange.Contains (range.Start)) {
                    Ranges[position] = new AddressRange (rightRange.Start, Math.Max (range.End, rightRange.End));
                    return true;
                }
            }
            return false;
        }

        internal bool Contains (AddressRange range)
        {
            int index = Ranges.BinarySearch (range, new RangeComparer ());

            // The start of this range is smaller than the start of any range in the list
            if (index == -1)
                return false;

            // An element in the collection has the same 'Start' as 'range'
            if (index >= 0)
                return range.End <= Ranges[index].End;

            index = ~index;
            AddressRange r = Ranges[index - 1];
            return r.Contains (range);
        }

        internal void Remove (AddressRange item)
        {
            if (Ranges.Count == 0)
                return;

            for (int i = item.Start; i <= item.End; i++) {
                var addressRange = new AddressRange (i, i);
                int index = Ranges.BinarySearch (addressRange, new RangeComparer ());
                if (index < 0) {
                    index = Math.Max ((~index) - 1, 0);

                    AddressRange range = Ranges[index];
                    if (addressRange.Start < range.Start || addressRange.Start > range.End)
                        continue;

                    if (addressRange.Start == range.Start) {
                        Ranges[index] = new AddressRange (range.Start + 1, range.End);
                    } else if (addressRange.End == range.End) {
                        Ranges[index] = new AddressRange (range.Start, range.End - 1);
                    } else {
                        Ranges[index] = new AddressRange (range.Start, addressRange.Start - 1);
                        Ranges.Insert (index + 1, new AddressRange (addressRange.Start + 1, range.End));
                    }
                } else {
                    AddressRange range = Ranges[index];
                    if (range.Contains (addressRange)) {
                        if (range.Start == range.End)
                            Ranges.RemoveAt (index);
                        else
                            Ranges[index] = new AddressRange (range.Start + 1, range.End);
                    }
                }
            }
        }

        internal void Validate ()
        {
            for (int i = 1; i < Ranges.Count; i++) {
                AddressRange left = Ranges[i - 1];
                AddressRange right = Ranges[i];
                if (left.Start > left.End)
                    throw new Exception ();
                if (left.End >= right.Start)
                    throw new Exception ();
            }
        }
    }
}
