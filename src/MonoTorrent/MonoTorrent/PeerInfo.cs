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

namespace MonoTorrent
{
    public class PeerInfo : IEquatable<PeerInfo>
    {
        public ReadOnlyMemory<byte> PeerId { get; }

        public Uri Uri { get; }

        public PeerInfo (Uri uri, ReadOnlyMemory<byte> peerId)
            => (Uri, PeerId) = (uri ?? throw new ArgumentNullException (nameof (peerId)), peerId);

        public override bool Equals (object? obj)
            => Equals (obj as PeerInfo);

        public bool Equals (PeerInfo? other)
            => !(other is null)
            && Uri.Equals (other.Uri)
            && PeerId.Span.SequenceEqual (other.PeerId.Span);

        public override int GetHashCode ()
            => Uri.GetHashCode ();

        public byte[] CompactPeer ()
            => CompactPeer (Uri);

        public void CompactPeer (Span<byte> buffer)
            => CompactPeer (Uri, buffer);

        public static byte[] CompactPeer (Uri uri)
        {
            byte[] data = new byte[6];
            CompactPeer (uri, data);
            return data;
        }

        public static void CompactPeer (Uri uri, Span<byte> buffer)
        {
            var bytes = IPAddress.Parse (uri.Host).GetAddressBytes ();
            bytes.AsSpan ().CopyTo (buffer);
            BinaryPrimitives.WriteUInt16BigEndian (buffer.Slice (bytes.Length), (ushort) uri.Port);
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
                list.Add (new PeerInfo (uri, Memory<byte>.Empty));
            }
        }
    }
}
