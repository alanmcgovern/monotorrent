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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Text;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    public sealed class PeerInfo : IEquatable<PeerInfo>
    {
        public bool MaybeSeeder { get; }
        public BEncodedString PeerId { get; }

        public Uri ConnectionUri { get; }

        public PeerInfo (Uri connectionUri)
            : this (connectionUri, BEncodedString.Empty)
        {
        }

        public PeerInfo (Uri connectionUri, BEncodedString peerId)
            : this (connectionUri, peerId, false)
        {
        }

        public PeerInfo (Uri connectionUri, BEncodedString peerId, bool maybeSeeder)
            => (ConnectionUri, PeerId, MaybeSeeder) = (connectionUri ?? throw new ArgumentNullException (nameof (connectionUri)), peerId ?? throw new ArgumentNullException (nameof (BEncodedString)), maybeSeeder);

        public override bool Equals (object? obj)
            => Equals (obj as PeerInfo);

        public bool Equals (PeerInfo? other)
            => ConnectionUri.Equals (other?.ConnectionUri);

        public override int GetHashCode ()
            => ConnectionUri.GetHashCode ();

        public byte[] CompactPeer ()
            => CompactPeer (ConnectionUri);

        public void CompactPeer (Span<byte> buffer)
            => CompactPeer (ConnectionUri, buffer);

        public static byte[] CompactPeer (Uri uri)
        {
            byte[] data = new byte[6];
            CompactPeer (uri, data);
            return data;
        }

        public static void CompactPeer (Uri uri, Span<byte> buffer)
        {
            foreach (char value in uri.Host.AsSpan ()) {
                if (value == '.') {
                    buffer = buffer.Slice (1);
                } else if (value >= '0' && value <= '9') {
                    buffer[0] = (byte) (buffer[0]  * 10 + (byte) (value - '0'));
                } else {
                    throw new NotSupportedException ("Invalid character in what should have been an ip address.");
                }
            }
            buffer = buffer.Slice (1);
            BinaryPrimitives.WriteUInt16BigEndian (buffer.Slice (0, 2), (ushort) uri.Port);
        }

        public static IList<PeerInfo> FromCompact (ReadOnlySpan<byte> buffer)
        {
            var sb = new StringBuilder (27);
            var list = new List<PeerInfo> ((buffer.Length / 6) + 1);
            FromCompact (buffer, sb, list);
            return list;
        }

        public static IList<PeerInfo> FromCompact (IEnumerable<byte[]> data)
        {
            var sb = new StringBuilder (27);
            var list = new List<PeerInfo> ();
            foreach (var buffer in data)
                FromCompact (buffer.AsSpan (), sb, list);
            return list;
        }

        static void FromCompact (ReadOnlySpan<byte> buffer, StringBuilder sb, List<PeerInfo> list)
        {
            // "Compact Response" peers are encoded in network byte order. 
            // IP's are the first four bytes
            // Ports are the following 2 bytes
            var byteOrderedData = buffer;
            int i = 0;
            ushort port;
            while ((i + 5) < byteOrderedData.Length) {
                sb.Remove (0, sb.Length);

                sb.Append ("ipv4://");
                sb.Append (byteOrderedData[i++]);
                sb.Append ('.');
                sb.Append (byteOrderedData[i++]);
                sb.Append ('.');
                sb.Append (byteOrderedData[i++]);
                sb.Append ('.');
                sb.Append (byteOrderedData[i++]);

                port = BinaryPrimitives.ReadUInt16BigEndian (byteOrderedData.Slice (i));
                i += 2;
                sb.Append (':');
                sb.Append (port);

                var uri = new Uri (sb.ToString ());
                list.Add (new PeerInfo (uri));
            }
        }
    }
}
