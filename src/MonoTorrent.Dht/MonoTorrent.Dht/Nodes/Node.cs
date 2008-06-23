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
using System.Collections;
using System.Collections.Generic;

using Mono.Math;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht
{
    public class Node
    {
        public const int MaxFailures = 4;
        CompactIpPort contactInfo;
        NodeId id;
        int failedCount;
        DateTime lastSeen;
        BEncodedString token;

        public CompactIpPort ContactInfo
        {
            get { return contactInfo; }
            set { contactInfo = value; }
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
        }

        // FIXME: State should be set properly as per specification.
        // i.e. it needs to take into account the 'LastSeen' property.
        // and must take into account when a node does not send us messages etc
        public NodeState State
        {
            get
            {
                if (failedCount > MaxFailures)
                    return NodeState.Bad;

                else if (lastSeen == DateTime.MinValue)
                    return NodeState.Unknown;

                return (DateTime.Now - lastSeen).TotalMinutes < 15 ? NodeState.Good : NodeState.Questionable;
            }
        }

        public BEncodedString Token
        {
            get { return token; }
            set { token = value; }
        }

        public Node(NodeId id)
        {
            this.id = id;
        }

        internal void Seen()
        {
            failedCount = 0;
            lastSeen = DateTime.Now;
        }
    }
}
