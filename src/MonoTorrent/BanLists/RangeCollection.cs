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
using System.Text;

namespace MonoTorrent.Client
{
    public struct RangeComparer : IComparer<AddressRange>
    {
        public int Compare(AddressRange x, AddressRange y)
        {
            return x.Start.CompareTo(y.Start);
        }
    }

    public class RangeCollection
    {
        List<AddressRange> ranges = new List<AddressRange>();

        public int Count
        {
            get { return ranges.Count; }
        }

        internal List<AddressRange> Ranges
        {
            get { return ranges; }
        }

        public void Add(AddressRange item)
        {
            int index;
            if (ranges.Count == 0 || item.Start > ranges[ranges.Count - 1].Start)
            {
                index = ranges.Count;
            }
            else
            {
                index = ranges.BinarySearch(item, new RangeComparer());
                if (index < 0)
                    index = ~index;
            }
            bool mergedLeft = MergeLeft(item, index);
            bool mergedRight = MergeRight(item, index);

            if (mergedLeft || mergedRight)
            {
                if (index > 0)
                    index--;

                while ((index +1) < ranges.Count)
                {
                    if (ranges[index].End > ranges[index + 1].Start || ranges[index].End + 1 == ranges[index + 1].Start)
                    {
                        ranges[index] = new AddressRange(ranges[index].Start, Math.Max(ranges[index].End, ranges[index + 1].End));
                        ranges.RemoveAt(index + 1);
                    }
                    else
                        break;
                }
            }
            else
            {
                ranges.Insert(index, item);
            }
        }

        public void AddRange(IEnumerable<MonoTorrent.Client.AddressRange> ranges)
        {
            List<AddressRange> list = new List<AddressRange>(ranges);
            list.Sort(delegate(AddressRange x, AddressRange y) { return x.Start.CompareTo(y.Start); });

            foreach (MonoTorrent.Client.AddressRange r in list)
                Add(new AddressRange(r.Start, r.End));
        }

        bool MergeLeft(AddressRange range, int position)
        {
            if (position > 0)
                position--;
            if (ranges.Count > position && position >= 0)
            {
                AddressRange leftRange = ranges[position];
                if (leftRange.Contains(range.Start))
                {
                    ranges[position] = new AddressRange(leftRange.Start, Math.Max(leftRange.End, range.End));
                    return true;
                }
                else if (leftRange.End + 1 == range.Start)
                {
                    ranges[position] = new AddressRange(leftRange.Start, range.End);
                    return true;
                }
                else if (leftRange.Start - 1 == range.End)
                {
                    ranges[position] = new AddressRange(range.Start, leftRange.End);
                    return true;
                }
            }
            return false;
        }

        bool MergeRight(AddressRange range, int position)
        {
            if (position == ranges.Count)
                position--;
            if (position >= 0 && position < ranges.Count)
            {
                AddressRange rightRange = ranges[position];
                if (rightRange.Contains(range.End))
                {
                    ranges[position] = new AddressRange(Math.Min(range.Start, rightRange.Start), rightRange.End);
                    return true;
                }
                else if (range.Contains(rightRange))
                {
                    ranges[position] = range;
                    return true;
                }
                else if (rightRange.Contains(range.Start))
                {
                    ranges[position] = new AddressRange(rightRange.Start, Math.Max(range.End, rightRange.End));
                    return true;
                }
            }
            return false;
        }

        internal bool Contains(AddressRange range)
        {
            int index = ranges.BinarySearch(range, new RangeComparer());
            
            // The start of this range is smaller than the start of any range in the list
            if (index == -1)
                return false;

            // An element in the collection has the same 'Start' as 'range' 
            if (index >= 0)
                return range.End <= ranges[index].End;

            index = ~index;
            AddressRange r = ranges[index - 1];
            return r.Contains(range);
        }

        internal void Remove(AddressRange item)
        {
            if (ranges.Count == 0)
                return;

            for (int i = item.Start; i <= item.End; i++)
            {
                AddressRange addressRange = new AddressRange(i, i);
                int index = ranges.BinarySearch(addressRange, new RangeComparer());
                if (index < 0)
                {
                    index = Math.Max((~index) - 1, 0);

                    AddressRange range = ranges[index];
                    if (addressRange.Start < range.Start || addressRange.Start > range.End)
                        continue;

                    if (addressRange.Start == range.Start)
                    {
                        ranges[index] = new AddressRange(range.Start + 1, range.End);
                    }
                    else if (addressRange.End == range.End)
                    {
                        ranges[index] = new AddressRange(range.Start, range.End - 1);
                    }
                    else
                    {
                        ranges[index] = new AddressRange(range.Start, addressRange.Start - 1);
                        ranges.Insert(index + 1, new AddressRange(addressRange.Start + 1, range.End));
                    }
                }
                else
                {
                    AddressRange range = ranges[index];
                    if (range.Contains(addressRange))
                    {
                        if (range.Start == range.End)
                            ranges.RemoveAt(index);
                        else
                            ranges[index] = new AddressRange(range.Start + 1, range.End);
                    }
                }
            }
        }

        internal void Validate()
        {
            for (int i = 1; i < ranges.Count; i++)
            {
                AddressRange left = ranges[i - 1];
                AddressRange right = ranges[i];
                if (left.Start > left.End)
                    throw new Exception();
                if (left.End >= right.Start)
                    throw new Exception();
            }
        }
    }
}
