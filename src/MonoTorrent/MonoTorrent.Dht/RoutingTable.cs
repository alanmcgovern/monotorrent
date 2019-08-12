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
using System.Collections.Generic;
using System.Net;

namespace MonoTorrent.Dht
{
    class RoutingTable
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

        public bool NeedsBootstrap => CountNodes () < 10;

        public RoutingTable()
            : this(new Node(NodeId.Create(), new System.Net.IPEndPoint(IPAddress.Any, 0)))
        {

        }

        public RoutingTable(Node localNode)
        {
            if (localNode == null)
                throw new ArgumentNullException("localNode");

            this.localNode = localNode;
            localNode.Seen();
            Add(new Bucket());
        }

        public bool Add(Node node)
        {
            return Add(node, true);
        }

        private bool Add(Node node, bool raiseNodeAdded)
        {
            if (node == null)
                throw new ArgumentNullException("node");

            Bucket bucket = buckets.Find(delegate(Bucket b) { return b.CanContain(node); });
            if (bucket.Nodes.Contains(node))
                return false;

            bool added = bucket.Add(node);
            if (!added && bucket.CanContain(LocalNode))
                if (Split(bucket))
                    return Add(node, raiseNodeAdded);

            return added;
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

        private void Remove(Bucket bucket)
        {
            buckets.Remove(bucket);
        }

        private bool Split(Bucket bucket)
        {
            if (bucket.Capacity < Bucket.MaxCapacity)
                return false;//to avoid infinite loop when add same node
            
            NodeId median = NodeId.Median (bucket.Min, bucket.Max);
            Bucket left = new Bucket(bucket.Min, median);
            Bucket right = new Bucket(median, bucket.Max);

            Remove(bucket);
            Add(left);
            Add(right);

            foreach (Node n in bucket.Nodes)
                Add(n, false);

            if (bucket.Replacement != null)
                Add(bucket.Replacement, false);

            return true;
        }

        public int CountNodes()
        {
            int r = 0;
            foreach (Bucket b in buckets)
                r += b.Nodes.Count;
            return r;            
        }

        
        public ICollection<Node> GetClosest(NodeId target)
        {
            var closestNodes = new ClosestNodesCollection (target);

            // Buckets have a capacity of 8 and are split in two whenever they are
            // full. As such we should always be able to find the 8 closest nodes
            // by adding the nodes of the matching bucket, the bucket above, and the
            // bucket below.
            var firstBucketIndex = buckets.FindIndex (t => t.CanContain (target));
            foreach (var node in buckets[firstBucketIndex].Nodes)
                closestNodes.Add (node);

            // Try the bucket before this one
            if (firstBucketIndex > 0)
                foreach (var node in buckets [firstBucketIndex - 1].Nodes)
                    closestNodes.Add (node);

            // Try the bucket after this one
            if (firstBucketIndex < (buckets.Count - 1))
                foreach (var node in buckets [firstBucketIndex + 1].Nodes)
                    closestNodes.Add (node);

            return closestNodes;
        }

        internal void Clear()
        {
            buckets.Clear();
            Add(new Bucket());
        }
    }
}
