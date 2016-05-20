using System;
using System.Collections.Generic;
using System.Net;

namespace MonoTorrent.Client
{
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
            Start = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(start.GetAddressBytes(), 0));
            End = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(end.GetAddressBytes(), 0));
        }

        public bool Contains(int value)
        {
            return value >= Start && value <= End;
        }

        public bool Contains(AddressRange range)
        {
            return range.Start >= Start && range.End <= End;
        }

        public override string ToString()
        {
            return string.Format("{0},{1}", Start, End);
        }
    }

    public class BanList
    {
        private readonly RangeCollection addresses = new RangeCollection();

        public void Add(IPAddress address)
        {
            Check.Address(address);
            Add(new AddressRange(address, address));
        }

        public void Add(AddressRange addressRange)
        {
            addresses.Add(addressRange);
        }

        public void AddRange(IEnumerable<AddressRange> addressRanges)
        {
            Check.AddressRanges(addressRanges);
            addresses.AddRange(addressRanges);
        }

        public bool IsBanned(IPAddress address)
        {
            Check.Address(address);
            return addresses.Contains(new AddressRange(address, address));
        }

        private void Remove(AddressRange addressRange)
        {
            addresses.Remove(addressRange);
        }

        public void Remove(IPAddress address)
        {
            Check.Address(address);
            Remove(new AddressRange(address, address));
        }

        public void Remove(IEnumerable<AddressRange> addressRanges)
        {
            Check.AddressRanges(addressRanges);
            foreach (var address in addressRanges)
                Remove(address);
        }
    }
}