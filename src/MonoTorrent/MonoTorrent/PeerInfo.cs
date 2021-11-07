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
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace MonoTorrent
{
    public class PeerInfo
    {
        public byte[] PeerId { get; }

        public Uri Uri { get; }

        public PeerInfo (Uri uri, byte[] peerId)
            => (Uri, PeerId) = (uri ?? throw new ArgumentNullException (nameof (peerId)), peerId ?? Array.Empty<byte> ());

        public byte[] CompactPeer ()
            => CompactPeer (Uri);

        public void CompactPeer (byte[] data, int offset)
            => CompactPeer (Uri, data, offset);

        public static byte[] CompactPeer (Uri uri)
        {
            byte[] data = new byte[6];
            CompactPeer (uri, data, 0);
            return data;
        }

        public static void CompactPeer (Uri uri, byte[] data, int offset)
        {
            Buffer.BlockCopy (IPAddress.Parse (uri.Host).GetAddressBytes (), 0, data, offset, 4);

            var port = (ushort) IPAddress.HostToNetworkOrder ((short) uri.Port);
            data[offset + 4] = (byte) (port >> 0);
            data[offset + 5] = (byte) (port >> 8);
        }

        public static IList<PeerInfo> FromCompact (byte[] data)
            => FromCompact (data, 0);

        public static IList<PeerInfo> FromCompact (byte[] data, int offset)
        {
            // "Compact Response" peers are encoded in network byte order. 
            // IP's are the first four bytes
            // Ports are the following 2 bytes
            byte[] byteOrderedData = data;
            int i = offset;
            ushort port;
            var sb = new StringBuilder (27);
            var list = new List<PeerInfo> ((byteOrderedData.Length / 6) + 1);
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

                port = (ushort) IPAddress.NetworkToHostOrder (BitConverter.ToInt16 (byteOrderedData, i));
                i += 2;
                sb.Append (':');
                sb.Append (port);

                var uri = new Uri (sb.ToString ());
                list.Add (new PeerInfo (uri, Array.Empty<byte> ()));
            }

            return list;
        }
    }
}
