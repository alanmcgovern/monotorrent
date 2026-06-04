using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MonoTorrent
{
    public readonly struct CompactEndPoint : IEquatable<CompactEndPoint>
    {
        const int InlineArraySize = 19;

        static int IPv4PortOffset;
        static int IPv4AddressOffset;

        static int IPv6PortOffset;
        static int IPv6AddressOffset;

        static CompactEndPoint ()
        {
            TestIPv4 ();
            TestIPv6 ();

            static void TestIPv4 ()
            {
                short port = 5678;
                var portBytes = BitConverter.GetBytes (IPAddress.HostToNetworkOrder (port));
                var expectedEndPoint = new IPEndPoint (IPAddress.Parse ("127.0.1.2"), port);
                ReadOnlySpan<byte> expectedCompact = stackalloc byte[] { 127, 0, 1, 2, portBytes[0], portBytes [1] };
                var expectedSocketAddress = expectedEndPoint.Serialize ();

                var compactEndPoint = CompactEndPoint.FromCompact (expectedCompact, AddressFamily.InterNetwork);
                if (compactEndPoint.Port != 5678)
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv4 data in an unsupported format");

                Span<byte> actualCompact = stackalloc byte[6];
                if (!compactEndPoint.TryWriteBytes (actualCompact, out int written) || written != expectedCompact.Length || !expectedCompact.SequenceEqual (actualCompact))
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv4 data in an unsupported format");

                IPv4AddressOffset = MemoryExtensions.IndexOf (expectedSocketAddress.Buffer.Span, expectedCompact.Slice (0, 4));
                if (IPv4AddressOffset == -1)
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv4 data in an unsupported format");

                IPv4PortOffset = MemoryExtensions.IndexOf (expectedSocketAddress.Buffer.Span, expectedCompact.Slice (4, 2));
                if (IPv4PortOffset == -1)
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv4 data in an unsupported format");

                var actualSocketAddress = new SocketAddress (AddressFamily.InterNetwork);
                if (!compactEndPoint.TryWriteBytes (actualSocketAddress))
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv4 data in an unsupported format");
                if (!actualSocketAddress.Equals (expectedSocketAddress))
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv4 data in an unsupported format");
            }

            static void TestIPv6 ()
            {
                short port = 5678;
                var portBytes = BitConverter.GetBytes (IPAddress.HostToNetworkOrder (port));

                var expectedEndPoint = new IPEndPoint (IPAddress.Parse ("0102:0304:0506:0708:090A:0B0C:0D0E:0F10"), port);
                ReadOnlySpan<byte> expectedCompact = stackalloc byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, portBytes[0], portBytes[1] };
                var expectedSocketAddress = expectedEndPoint.Serialize ();

                var compactEndpoint = CompactEndPoint.FromCompact (expectedCompact, AddressFamily.InterNetworkV6);
                if (compactEndpoint.Port != port)
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv6 data in an unsupported format");

                Span<byte> actualCompact = stackalloc byte[18];
                if (!compactEndpoint.TryWriteBytes (actualCompact, out var written) || written != expectedCompact.Length || !expectedCompact.SequenceEqual (actualCompact))
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv6 data in an unsupported format");

                IPv6AddressOffset = MemoryExtensions.IndexOf (expectedSocketAddress.Buffer.Span, expectedCompact.Slice (0, 16));
                if (IPv6AddressOffset == -1)
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv6 data in an unsupported format");

                IPv6PortOffset = MemoryExtensions.IndexOf (expectedSocketAddress.Buffer.Span, expectedCompact.Slice (16, 2));
                if (IPv6PortOffset == -1)
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv6 data in an unsupported format");

                var actualSocketAddress = new SocketAddress (AddressFamily.InterNetworkV6);
                if (!compactEndpoint.TryWriteBytes (actualSocketAddress))
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv6 data in an unsupported format");
                if (!actualSocketAddress.Equals (expectedSocketAddress))
                    throw new PlatformNotSupportedException ("SocketAddress stores ipv6 data in an unsupported format");
            }
        }

        // Large enough for 16 byte ipv6 address + 2 byte port + 1 byte discriminator
        [InlineArray (InlineArraySize)]
        struct Storage
        {
            internal byte _element;
        }
        readonly Storage _data;

        /// <summary>
        /// Reads a 4 or 16 byte address from the buffer, followed by a 2 byte port./>
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="family">Should be InterNetwork or InterNetworkV6</param>
        /// <returns></returns>
        public static CompactEndPoint FromCompact(ReadOnlySpan<byte> buffer, AddressFamily family)
        {
            CompactEndPoint result = new CompactEndPoint ();
            if (family == AddressFamily.InterNetwork) {
                buffer.Slice (0, 4).CopyTo (result.Span);
                buffer.Slice (4, 2).CopyTo (result.Span.Slice (16, 2));
                result.Span[18] = 0;
            } else if (family == AddressFamily.InterNetworkV6) {
                buffer.Slice (0, 16).CopyTo (result.Span);
                buffer.Slice (16, 2).CopyTo (result.Span.Slice (16, 2));
                result.Span[18] = 1;
            } else {
                throw new ArgumentException ("AddressFamily should be InterNetwork or InterNetworkV6", nameof (family));
            }
            return result;
        }

        public ReadOnlySpan<byte> Address => Span.Slice (0, Family == AddressFamily.InterNetwork ? 4 : 16);

        public AddressFamily Family => _data[18] == 0 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6;

        public ushort Port => BinaryPrimitives.ReadUInt16BigEndian (Span.Slice (16, 2));

        Span<byte> Span
            => MemoryMarshal.CreateSpan (ref Unsafe.AsRef (in _data._element), InlineArraySize);

        public CompactEndPoint (SocketAddress address)
        {
            // Format is 2 byte address family, 2 byte port, $N byte ipaddress
            Span<byte> tmp = _data;
            switch (address.Family) {
                case System.Net.Sockets.AddressFamily.InterNetwork:
                    address.Buffer.Span.Slice (IPv4AddressOffset, 4).CopyTo (tmp.Slice (0, 4));
                    address.Buffer.Span.Slice (IPv4PortOffset, 2).CopyTo (tmp.Slice (16, 2));
                    tmp[18] = 0;
                    break;
                case System.Net.Sockets.AddressFamily.InterNetworkV6:
                    address.Buffer.Span.Slice (IPv6AddressOffset, 16).CopyTo (tmp.Slice (0, 16));
                    address.Buffer.Span.Slice (IPv6PortOffset, 2).CopyTo (tmp.Slice (16, 2));
                    tmp[18] = 1;
                    break;
                default:
                    throw new NotSupportedException ();
            }
        }

        public CompactEndPoint (IPAddress address, int port)
        {
            Span<byte> tmp = _data;
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                tmp[18] = 0;
            else if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                tmp[18] = 1;
            else
                throw new NotSupportedException ();

            if (!address.TryWriteBytes (tmp, out int written))
                throw new ArgumentException ("Couldn't convert IPAddress to a byte buffer");
            BinaryPrimitives.WriteUInt16BigEndian (tmp.Slice (16, 2), (ushort) port);
        }

        public override bool Equals ([NotNullWhen (true)] object? obj)
            => obj is CompactEndPoint c && Equals (c);

        public bool Equals (CompactEndPoint other)
            => Span.SequenceEqual (other.Span);

        public override int GetHashCode ()
        {
            var hc = new HashCode ();
            hc.AddBytes (Span);
            return hc.ToHashCode ();
        }

        /// <summary>
        /// Writes the underlying bytes as the 4 or 16 bytes of the IPAddress in network order followed by the 2 byte port in network order.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="bytesWritten"></param>
        /// <returns></returns>
        public bool TryWriteBytes (Span<byte> destination, out int bytesWritten)
        {
            if (destination.Length < Address.Length + 2) {
                bytesWritten = 0;
                return false;
            }

            Address.CopyTo (destination);
            BinaryPrimitives.WriteUInt16BigEndian (destination.Slice (Address.Length), Port);
            bytesWritten = Address.Length + 2;
            return true;
        }

        public bool TryWriteBytes (SocketAddress destination)
        {
            if (Family != destination.Family)
                return false;
            switch (Family) {
                case AddressFamily.InterNetwork:
                    Address.CopyTo (destination.Buffer.Span.Slice (IPv4AddressOffset));
                    Span.Slice (16, 2).CopyTo (destination.Buffer.Span.Slice (IPv4PortOffset));
                    break;
                case AddressFamily.InterNetworkV6:
                    Address.CopyTo (destination.Buffer.Span.Slice (Family == AddressFamily.InterNetwork ? IPv4AddressOffset : IPv6AddressOffset));
                    Span.Slice (16, 2).CopyTo (destination.Buffer.Span.Slice (IPv6PortOffset));
                    break;
                default:
                    throw new NotSupportedException ("Only AddressFamily.InterNetwork and AddressFamily.InterNetworkv6 are supported");
            }
            
            return true;
        }

        public override string ToString ()
            => new IPAddress (Address).ToString () + ":" + Port;
    }
}
