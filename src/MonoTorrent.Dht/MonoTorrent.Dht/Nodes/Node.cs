//
// Node.cs
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
using System.Collections;
using System.Collections.Generic;

using Mono.Math;
using MonoTorrent.BEncoding;
using System.Net;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht
{
    public class Node : IComparable<Node>
    {
        public const int MaxFailures = 4;

        IPEndPoint endpoint;
        NodeId id;
        int failedCount;
        DateTime lastSeen;
        BEncodedString token;

        public IPEndPoint EndPoint
        {
            get { return endpoint; }
        }

        public int FailedCount
        {
            get { return failedCount; }
            internal set { failedCount = value; }
        }

        public NodeId Id
        {
            get { return id; }
        }

        public DateTime LastSeen
        {
            get { return lastSeen; }
            internal set { lastSeen = value; }
        }

        // FIXME: State should be set properly as per specification.
        // i.e. it needs to take into account the 'LastSeen' property.
        // and must take into account when a node does not send us messages etc
        public NodeState State
        {
            get
            {
                if (failedCount >= MaxFailures)
                    return NodeState.Bad;

                else if (lastSeen == DateTime.MinValue)
                    return NodeState.Unknown;

                return (DateTime.UtcNow - lastSeen).TotalMinutes < 15 ? NodeState.Good : NodeState.Questionable;
            }
        }

        public BEncodedString Token
        {
            get { return token; }
            set { token = value; }
        }

        public Node(NodeId id, IPEndPoint endpoint)
        {
            this.endpoint = endpoint;
            this.id = id;
        }

        internal void Seen()
        {
            failedCount = 0;
            lastSeen = DateTime.UtcNow;
        }

        internal BEncodedString CompactPort()
        {
            byte[] buffer = new byte[6];
            CompactPort(buffer, 0);
            return buffer;
        }

        internal void CompactPort(byte[] buffer, int offset)
        {
            Message.Write(buffer, offset, endpoint.Address.GetAddressBytes());
            Message.Write(buffer, offset + 4, (ushort)endpoint.Port);
        }

        internal static BEncodedString CompactPort(IList<Node> peers)
        {
            byte[] buffer = new byte[peers.Count * 6];
            for (int i = 0; i < peers.Count; i++)
                peers[i].CompactPort(buffer, i * 6);

            return new BEncodedString(buffer);
        }

        internal BEncodedString CompactNode()
        {
            byte[] buffer = new byte[26];
            CompactNode(buffer, 0);
            return buffer;
        }

        private void CompactNode(byte[] buffer, int offset)
        {
            Message.Write(buffer, offset, id.Bytes);
            CompactPort(buffer, offset + 20);
        }

        internal static BEncodedString CompactNode(IList<Node> nodes)
        {
            byte[] buffer = new byte[nodes.Count * 26];
            for (int i = 0; i < nodes.Count; i++)
                nodes[i].CompactNode(buffer, i * 26);

            return new BEncodedString(buffer);
        }

        internal static Node FromCompactNode(byte[] buffer, int offset)
        {
            byte[] id = new byte[20];
            Buffer.BlockCopy(buffer, offset, id, 0, 20);
            IPAddress address = new IPAddress(BitConverter.ToInt32(buffer, offset + 20));
            int port = (int)(ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(buffer, offset + 24));
            return new Node(new NodeId(id), new IPEndPoint(address, port));
        }

        //To order by last seen in bucket
        public int CompareTo(Node other)
        {
            if (other == null)
                return 1;
            
            return lastSeen.CompareTo(other.lastSeen);
        }
    }
}