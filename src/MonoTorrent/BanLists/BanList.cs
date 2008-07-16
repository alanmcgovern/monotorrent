using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace MonoTorrent.Client
{
    public struct AddressRange
    {
        public uint Start;
        public uint End;

        public AddressRange(int start, int end)
        {
            Start = (uint)start;
            End = (uint)end;
        }

        public AddressRange(IPAddress start, IPAddress end)
        {
            Start = (uint)(IPAddress.NetworkToHostOrder(start.Address) >> 32);
            End = (uint)(IPAddress.NetworkToHostOrder(end.Address) >> 32);
        }
    }

    public class BanList
    {
        Hyena.Collections.RangeCollection addresses = new Hyena.Collections.RangeCollection();

        public void Add(AddressRange address)
        {
            for (uint i = address.End; i > address.Start; i--)
                addresses.Add(i);
            addresses.Add(address.Start);
        }

        public void AddRange(IEnumerable<AddressRange> range)
        {
            foreach (AddressRange address in range)
                Add(address);
        }

        public bool IsBanned(IPAddress address)
        {
            AddressRange range = new AddressRange(address, address);
            return addresses.Contains(range.Start);
        }

        private void Remove(AddressRange range)
        {
            for (uint i = range.Start; i <= range.End; i++)
                addresses.Remove(i);
        }

        public void Remove(IPAddress address)
        {
            Remove(new AddressRange(address, address));
        }

        public void Remove(IEnumerable<AddressRange> range)
        {
            foreach (AddressRange address in range)
                Remove(address);
        }
    }
}
