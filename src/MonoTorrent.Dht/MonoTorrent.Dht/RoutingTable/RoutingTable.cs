//
// RoutingTable.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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


namespace MonoTorrent.Dht
{
    public class RoutingTable
    {
        private readonly Node localNode;
        private List<Bucket> buckets = new List<Bucket>();

        internal List<Bucket> Buckets
        {
            get { return buckets; }
        }

        public Node LocalNode
        {
            get { return localNode; }
        }

        public RoutingTable()
            : this(new Node(NodeId.Create()))
        {

        }

        public RoutingTable(Node localNode)
        {
            this.localNode = localNode;
            localNode.Seen();
            Add(new Bucket());
            Add(localNode);
        }

        public void Add(Node node)
        {
            Bucket bucket = buckets.Find(delegate(Bucket b) { return node.Id >= b.Min && node.Id < b.Max; });

            bool added = bucket.Add(node);
            if (!added && bucket.Nodes.Contains(LocalNode))
                if (Split(bucket))
                    Add(node);
        }

        private void Add(Bucket bucket)
        {
            buckets.Add(bucket);
            buckets.Sort();
        }

        internal Node FindNode(NodeId id)
        {
            foreach (Bucket b in this.buckets)
                foreach (Node n in b.Nodes)
                    if (n.Id.Equals(id))
                        return n;

            return null;
        }

        internal void Initialise()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        private void Remove(Bucket bucket)
        {
            buckets.Remove(bucket);
        }

        private bool Split(Bucket bucket)
        {
            if (bucket.Max - bucket.Min < 6)
                return false;

            NodeId median = (bucket.Min + bucket.Max) / 2;
            Bucket left = new Bucket(bucket.Min, median);
            Bucket right = new Bucket(median, bucket.Max);

            Remove(bucket);
            Add(left);
            Add(right);

            foreach (Node n in bucket.Nodes)
                Add(n);

            Add(bucket.Replacement);
            return true;
        }

        internal List<Node> GetClosest(NodeId Target)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}