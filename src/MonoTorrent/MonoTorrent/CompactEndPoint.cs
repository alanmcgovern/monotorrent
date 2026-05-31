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

        static CompactEndPoint()
        {
            var socketAddress = new IPEndPoint (IPAddress.Parse ("127.0.1.2"), 16).Serialize ();
            var c = new CompactEndPoint (socketAddress);

            Span<byte> dest = stackalloc byte[18];
            Span<byte> expected = stackalloc byte[] { 127, 0, 1, 2, 0, 16 };
            if (!c.TryWriteBytes (dest, out int written) || written != expected.Length || !expected.SequenceEqual (dest.Slice (0, 6)))
                throw new PlatformNotSupportedException ("SocketAddress stores ipv4 data in an unsupported format");

            socketAddress = new IPEndPoint (IPAddress.Parse ("0102:0304:0506:0708:090A:0B0C:0D0E:0F10"), 17).Serialize ();
            c = new CompactEndPoint (socketAddress);

            Span<byte> expectedv6 = stackalloc byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 0, 17 };
            if (!c.TryWriteBytes (dest, out written) || written!= expectedv6.Length || !expectedv6.SequenceEqual (dest))
                throw new PlatformNotSupportedException ("SocketAddress stores ipv6 data in an unsupported format");
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
                    address.Buffer.Span.Slice (4, 4).CopyTo (tmp.Slice (0, 4));
                    tmp[18] = 0;
                    break;
                case System.Net.Sockets.AddressFamily.InterNetworkV6:
                    address.Buffer.Span.Slice (8, 16).CopyTo (tmp.Slice (0, 16));
                    tmp[18] = 1;
                    break;
                default:
                    throw new NotSupportedException ();
            }
            address.Buffer.Span.Slice (2, 2).CopyTo (tmp.Slice (16, 2));
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
            Address.CopyTo (destination.Buffer.Span.Slice (4));
            BinaryPrimitives.WriteUInt16BigEndian (destination.Buffer.Span.Slice (2), (ushort) Port);
            return true;
        }

        public override string ToString ()
            => new IPAddress (Address).ToString () + ":" + Port;
    }
}
