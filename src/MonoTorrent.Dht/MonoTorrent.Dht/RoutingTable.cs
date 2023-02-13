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
        internal List<Bucket> Buckets { get; private set; }

        public NodeId LocalNodeId { get; }

        public bool NeedsBootstrap => CountNodes () < 10;

        public RoutingTable ()
            : this (NodeId.Create ())
        {

        }

        public RoutingTable (NodeId localNodeId)
        {
            Buckets = new List<Bucket> ();
            LocalNodeId = localNodeId;
            Add (new Bucket ());
        }

        public bool Add (Node node)
        {
            return Add (node, true);
        }

        bool Add (Node node, bool raiseNodeAdded)
        {
            if (node == null)
                throw new ArgumentNullException (nameof (node));

            Bucket bucket = Buckets.Find (b => b.CanContain (node))!;
            if (bucket.Nodes.Contains (node))
                return false;

            bool added = bucket.Add (node);
            if (!added && bucket.CanContain (LocalNodeId))
                if (Split (bucket))
                    return Add (node, raiseNodeAdded);

            return added;
        }

        void Add (Bucket bucket)
        {
            var newBuckets = new List<Bucket> (Buckets);
            newBuckets.Add (bucket);
            newBuckets.Sort ();
            Buckets = newBuckets;
        }

        internal Node? FindNode (NodeId id)
        {
            foreach (Bucket b in Buckets)
                foreach (Node n in b.Nodes)
                    if (n.Id.Equals (id))
                        return n;

            return null;
        }

        void Remove (Bucket bucket)
        {
            var newBuckets = new List<Bucket> (Buckets);
            newBuckets.Remove (bucket);
            Buckets = newBuckets;
        }

        bool Split (Bucket bucket)
        {
            if (!bucket.CanSplit)
                return false;//to avoid infinite loop when add same node

            var median = NodeId.Median (bucket.Min, bucket.Max);
            var left = new Bucket (bucket.Min, median);
            var right = new Bucket (median, bucket.Max);

            Remove (bucket);
            Add (left);
            Add (right);

            foreach (Node n in bucket.Nodes)
                Add (n, false);

            if (bucket.Replacement != null)
                Add (bucket.Replacement, false);

            return true;
        }

        public int CountNodes ()
        {
            int r = 0;
            foreach (Bucket b in Buckets)
                r += b.Nodes.Count;
            return r;
        }


        public ICollection<Node> GetClosest (NodeId target)
        {
            var closestNodes = new ClosestNodesCollection (target);

            // Buckets have a capacity of 8 and are split in two whenever they are
            // full. As such we should always be able to find the 8 closest nodes
            // by adding the nodes of the matching bucket, the bucket above, and the
            // bucket below.
            int firstBucketIndex = Buckets.FindIndex (t => t.CanContain (target));
            foreach (Node node in Buckets[firstBucketIndex].Nodes)
                closestNodes.Add (node);

            // Try the bucket before this one
            if (firstBucketIndex > 0)
                foreach (Node node in Buckets[firstBucketIndex - 1].Nodes)
                    closestNodes.Add (node);

            // Try the bucket after this one
            if (firstBucketIndex < (Buckets.Count - 1))
                foreach (Node node in Buckets[firstBucketIndex + 1].Nodes)
                    closestNodes.Add (node);

            return closestNodes;
        }

        internal void Clear ()
        {
            Buckets = new List<Bucket> {
                new Bucket ()
            };
        }
    }
}
