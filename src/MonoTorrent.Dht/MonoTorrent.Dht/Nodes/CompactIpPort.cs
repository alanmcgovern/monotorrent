//
// Bucket.cs
//
// Authors:
//   Jérémie Laval <jeremie.laval@gmail.com>
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2008 Jérémie Laval, Alan McGovern
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
using System.Net;
using MonoTorrent.BEncoding;
using System.Collections;
using MonoTorrent.Dht.Messages;
using System.Collections.Generic;

namespace MonoTorrent.Dht
{
    public struct CompactIpPort
    {
        private IPEndPoint endpoint;
        public IPEndPoint EndPoint
        {
            get { return endpoint; }
        }

        public CompactIpPort(IPAddress ip, int port)
        {
            this.endpoint = new IPEndPoint(ip, port);
        }

        internal BEncodedString Encode()
        {
            byte[] buffer = new byte[6];
            Encode(buffer, 0);
            return buffer;
        }

        internal void Encode(byte[] buffer, int offset)
        {
            Message.Write(buffer, offset, endpoint.Address.GetAddressBytes());
            Message.Write(buffer, offset + 4, (ushort)endpoint.Port);
        }

        public static BEncodedString Encode(IList<CompactIpPort> peers)
        {
            byte[] buffer = new byte[peers.Count * 6];
            for (int i = 0; i < peers.Count; i++)
                peers[i].Encode(buffer, i * 6);
            
            return new BEncodedString(buffer);
        }

        public static BEncodedString Encode(IList<Node> nodes)
        {
            byte[] buffer = new byte[nodes.Count * 6];
            for (int i = 0; i < nodes.Count; i++)
                nodes[i].ContactInfo.Encode(buffer, i * 6);

            return new BEncodedString(buffer);
        }
    }
}
