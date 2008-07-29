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
using System.Net;
using MonoTorrent.BEncoding;


namespace MonoTorrent.Dht
{
    public class RoutingTable
    {
        public event EventHandler<NodeAddedEventArgs> NodeAdded;

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

        public void Add(DhtEngine engine, Node node)
        {
            if (node == null)
                throw new ArgumentNullException("node");

            Bucket bucket = buckets.Find(delegate(Bucket b) { return b.CanContain(node); });

            bool added = bucket.Add(node);
            if (!added && bucket.CanContain(LocalNode))
            {
                if (Split(engine, bucket))
                {
                    Add(engine, node);
                    return;
                }
            }
            if (!added)
            {
                new ReplacementTask(engine, node, bucket).Execute();
            }
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

        private bool Split(DhtEngine engine, Bucket bucket)
        {
            if (bucket.Max - bucket.Min < Bucket.MaxCapacity)
                return false;//to avoid infinit loop when add same node
            
            NodeId median = (bucket.Min + bucket.Max) / 2;
            Bucket left = new Bucket(bucket.Min, median);
            Bucket right = new Bucket(median, bucket.Max);

            Remove(bucket);
            Add(left);
            Add(right);

            foreach (Node n in bucket.Nodes)
                Add(engine, n);
            return true;
        }

        public int CountNodes()
        {
            int r = 0;
            foreach (Bucket b in buckets)
                r += b.Nodes.Count;
            return r;            
        }

        
        public IList<Node> GetClosest(NodeId Target)
        {
            SortedList<NodeId,Node> sortedNodes = new SortedList<NodeId,Node>(Bucket.MaxCapacity);
						
            foreach (Bucket b in this.buckets)
            {
                foreach (Node n in b.Nodes)
                {
                    NodeId distance = n.Id.Xor(Target);
                    if (sortedNodes.Count == Bucket.MaxCapacity)
                    {
                        if (distance > sortedNodes.Keys[sortedNodes.Count-1])//maxdistance
                            continue;
                        //remove last (with the maximum distance)
                        sortedNodes.RemoveAt(sortedNodes.Count-1);						
                    }
                    sortedNodes.Add(distance, n);
                }
            }
            return sortedNodes.Values;
        }
    }
}
