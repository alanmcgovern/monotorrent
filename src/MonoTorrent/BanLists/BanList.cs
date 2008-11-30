//
// BanList.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
            Start = (IPAddress.NetworkToHostOrder(BitConverter.ToInt32(start.GetAddressBytes(), 0)));
            End = (IPAddress.NetworkToHostOrder(BitConverter.ToInt32(end.GetAddressBytes(), 0)));
        }
    }

    public class BanList
    {
        Hyena.Collections.RangeCollection addresses = new Hyena.Collections.RangeCollection();

        public void Add(IPAddress address)
        {
            Check.Address(address);
            Add(new AddressRange(address, address));
        }

        public void Add(AddressRange addressRange)
        {
            for (int i = addressRange.End; i > addressRange.Start; i--)
                addresses.Add(i);
            addresses.Add(addressRange.Start);
        }

        public void AddRange(IEnumerable<AddressRange> addressRanges)
        {
            Check.AddressRanges(addressRanges);
            foreach (AddressRange address in addressRanges)
                Add(address);
        }

        public bool IsBanned(IPAddress address)
        {
            Check.Address(address);
            AddressRange range = new AddressRange(address, address);
            return addresses.Contains(range.Start);
        }

        private void Remove(AddressRange addressRange)
        {
            for (int i = addressRange.Start; i <= addressRange.End; i++)
                addresses.Remove(i);
        }

        public void Remove(IPAddress address)
        {
            Check.Address(address);
            Remove(new AddressRange(address, address));
        }

        public void Remove(IEnumerable<AddressRange> addressRanges)
        {
            Check.AddressRanges(addressRanges);
            foreach (AddressRange address in addressRanges)
                Remove(address);
        }
    }
}
