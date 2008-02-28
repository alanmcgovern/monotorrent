using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace MonoTorrent.Tracker
{
    public interface IPeerComparer : IEqualityComparer<object>
    {
        object GetKey(AnnounceParameters parameters);
    }

    public class IPAddressComparer : IPeerComparer
    {
        public new bool Equals(object left, object right)
        {
            if (left == null)
                return right == null;

            if (right == null)
                return false;

            IPAddress l = (IPAddress)left;
            IPAddress r = (IPAddress)right;
            return l.Equals(r);
        }

        public int GetHashCode(object obj)
        {
            if (obj == null)
                return 0;

            return obj.GetHashCode();
        }

        public object GetKey(AnnounceParameters parameters)
        {
            return parameters.ClientAddress.Address;
        }
    }
}
