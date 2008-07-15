using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace MonoTorrent.Client
{
    internal class RangeComparer : IComparer<AddressRange>
    {
        public int Compare(AddressRange source, AddressRange target)
        {
            if (target.Start >= source.Start && target.End <= source.End)
                return 0;
            if (source.Start > target.Start)
                return 1;
            return -1;
        }
    }

    public struct AddressRange
    {
        public int Start;
        public int End;

        public AddressRange(int start, int end)
        {
            Start = start;
            End = end;
        }

        public AddressRange(IPAddress start, IPAddress end)
        {
            Start = (int)(IPAddress.NetworkToHostOrder(start.Address) >> 32);
            End = (int)(IPAddress.NetworkToHostOrder(end.Address) >> 32);
        }
    }

    public class BanList
    {
        RangeComparer comparer = new RangeComparer();
        List<AddressRange> addresses = new List<AddressRange>();

        public void Add(AddressRange address)
        {
            addresses.Add(address);
            addresses.Sort(comparer);
        }

        public void AddRange(IEnumerable<AddressRange> range)
        {
            addresses.AddRange(range);
            addresses.Sort(comparer);
        }

        public bool IsBanned(IPAddress address)
        {
            AddressRange range = new AddressRange(address, address);
            int ret = addresses.BinarySearch(range, new RangeComparer());
            return ret >= 0;
        }

        private void Remove(AddressRange range)
        {
            int index = addresses.BinarySearch(range, comparer);
            if (index < 0)
                return;

            if (addresses[index].Start == addresses[index].End && addresses[index].Start == range.Start)
            {
                addresses.RemoveAt(index);
                return;
            }

            if (addresses[index].Start == range.Start)
            {
                addresses[index] = new AddressRange(addresses[index].Start + 1, addresses[index].End);
            }
            else if (addresses[index].End == range.Start)
            {
                addresses[index] = new AddressRange(addresses[index].Start, addresses[index].End - 1);
            }
            else
            {
                // Split the existing range into two new ranges, the old max and min are the same
                // but the 'range' is removed from the middle.
                AddressRange lower = new AddressRange(addresses[index].Start, range.Start - 1);
                AddressRange upper = new AddressRange(range.Start + 1, addresses[index].End);

                addresses.RemoveAt(index);
                addresses.Insert(index, lower);
                addresses.Insert(index + 1, upper);
            }
        }

        public void Remove(IPAddress address)
        {
            AddressRange range = new AddressRange(address, address);
            Remove(range);
        }

        public void Remove(IEnumerable<AddressRange> range)
        {
            foreach (AddressRange address in range)
                Remove(address);
        }
    }
}
