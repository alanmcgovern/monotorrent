//
// PeerInfo.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    public sealed class PeerInfo : IEquatable<PeerInfo>
    {
        public bool MaybeSeeder { get; }
        public BEncodedString PeerId { get; }

        public Uri ConnectionUri { get; }

        IPEndPoint? EndPoint { get; }

        public PeerInfo (Uri connectionUri)
            : this (connectionUri, BEncodedString.Empty)
        {
        }

        public PeerInfo (Uri connectionUri, BEncodedString peerId)
            : this (connectionUri, peerId, false)
        {
        }

        public PeerInfo (Uri connectionUri, BEncodedString peerId, bool maybeSeeder)
            : this (connectionUri, peerId, maybeSeeder, IPAddress.TryParse (connectionUri.Host, out var ip) ? new IPEndPoint (ip, connectionUri.Port) : null)
        {

        }

        internal PeerInfo (Uri connectionUri, BEncodedString peerId, bool maybeSeeder, IPEndPoint? endPoint)
            => (ConnectionUri, PeerId, MaybeSeeder, EndPoint) = (connectionUri ?? throw new ArgumentNullException (nameof (connectionUri)), peerId ?? throw new ArgumentNullException (nameof (BEncodedString)), maybeSeeder, endPoint);

        public override bool Equals (object? obj)
            => Equals (obj as PeerInfo);

        public bool Equals (PeerInfo? other)
            => ConnectionUri.Equals (other?.ConnectionUri);

        public override int GetHashCode ()
            => ConnectionUri.GetHashCode ();

        public byte[] CompactPeer ()
        {
            var buffer = new byte[2 + (EndPoint!.AddressFamily == AddressFamily.InterNetworkV6 ? 16 : 4)];
            if (!TryWriteCompactPeer (ConnectionUri, buffer, out int written) || written != buffer.Length)
                throw new InvalidOperationException ();
            return buffer;
        }

        public bool TryWriteCompactPeer (Span<byte> buffer, out int written)
            => TryWriteCompactPeer (ConnectionUri, buffer, out written);

        bool TryWriteCompactPeer (Uri uri, Span<byte> buffer, out int written)
        {
            if(EndPoint == null) {
                written = 0;
                return false;
            }

            if (!EndPoint.Address.TryWriteBytes (buffer, out written))
                return false;

            BinaryPrimitives.WriteUInt16BigEndian (buffer.Slice (written, 2), (ushort) uri.Port);
            written += 2;
            return true;
        }

        public static IList<PeerInfo> FromCompact (ReadOnlySpan<byte> buffer, AddressFamily addressFamily)
        {
            var list = new List<PeerInfo> ((buffer.Length / 6) + 1);
            FromCompact (buffer, addressFamily, list);
            return list;
        }

        public static IList<PeerInfo> FromCompact (IEnumerable<byte[]> data, AddressFamily addressFamily)
        {
            var list = new List<PeerInfo> ();
            foreach (var buffer in data)
                FromCompact (buffer, addressFamily, list);
            return list;
        }

        static void FromCompact (ReadOnlySpan<byte> buffer, AddressFamily addressFamily, List<PeerInfo> results)
        {
            (var sizeOfIP, var prefix) = addressFamily switch {
                AddressFamily.InterNetwork => (4, "ipv4://"),
                AddressFamily.InterNetworkV6 => (16, "ipv6://"),
                _ => throw new NotSupportedException ()
            };

            var stride = sizeOfIP + 2;

            // Round it off into a multipl eof 'stride' bytes.
            buffer = buffer.Slice (0, (buffer.Length / stride) * stride);

            while (buffer.Length > 0) {
                var ipBuffer = buffer.Slice (0, sizeOfIP);
                var portBuffer = buffer.Slice (sizeOfIP, 2);
#if NETSTANDARD2_0 || NET472
                var ip = new IPAddress (ipBuffer.ToArray ());
#else
                var ip = new IPAddress (ipBuffer);
#endif
                var port = BinaryPrimitives.ReadUInt16BigEndian (portBuffer);

                var endPoint = new IPEndPoint (ip, port);
                results.Add (new PeerInfo (new Uri (prefix + endPoint.ToString ()), BEncodedString.Empty, false, endPoint));
                buffer = buffer.Slice (stride);
            }
        }
    }
}
