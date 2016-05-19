#if !DISABLE_DHT
//
// Bucket.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//   Jérémie Laval <jeremie.laval@gmail.com>
//
// Copyright (C) 2008 Alan McGovern, Jérémie Laval
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

namespace MonoTorrent.Dht
{
    /// <summary>
    ///     This class holds a maximum amount of 8 Nodes and is itself a child of a RoutingTable
    /// </summary>
    internal class Bucket : IComparable<Bucket>, IEquatable<Bucket>
    {
        public const int MaxCapacity = 8;
        private static readonly NodeId Minimum = new NodeId(new byte[20]);

        private static readonly NodeId Maximum =
            new NodeId(new byte[]
            {255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255});

        public Bucket()
            : this(Minimum, Maximum)
        {
        }

        public Bucket(NodeId min, NodeId max)
        {
            Min = min;
            Max = max;
        }

        public DateTime LastChanged { get; set; } = DateTime.UtcNow;

        public NodeId Max { get; }

        public NodeId Min { get; }

        public List<Node> Nodes { get; } = new List<Node>(MaxCapacity);

        internal Node Replacement { get; set; }

        public int CompareTo(Bucket other)
        {
            return Min.CompareTo(other.Min);
        }

        public bool Equals(Bucket other)
        {
            if (other == null)
                return false;

            return Min.Equals(other.Min) && Max.Equals(other.Max);
        }

        public bool Add(Node node)
        {
            // if the current bucket is not full we directly add the Node
            if (Nodes.Count < MaxCapacity)
            {
                Nodes.Add(node);
                LastChanged = DateTime.UtcNow;
                return true;
            }
            //test replace

            for (var i = Nodes.Count - 1; i >= 0; i--)
            {
                if (Nodes[i].State != NodeState.Bad)
                    continue;

                Nodes.RemoveAt(i);
                Nodes.Add(node);
                LastChanged = DateTime.Now;
                return true;
            }
            return false;
        }

        public bool CanContain(Node node)
        {
            if (node == null)
                throw new ArgumentNullException("node");
            return CanContain(node.Id);
        }

        public bool CanContain(NodeId id)
        {
            if (id == null)
                throw new ArgumentNullException("id");

            return Min <= id && Max > id;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Bucket);
        }

        public override int GetHashCode()
        {
            return Min.GetHashCode() ^ Max.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("Count: {2} Min: {0}  Max: {1}", Min, Max, Nodes.Count);
        }

        internal void SortBySeen()
        {
            Nodes.Sort();
        }
    }
}

#endif