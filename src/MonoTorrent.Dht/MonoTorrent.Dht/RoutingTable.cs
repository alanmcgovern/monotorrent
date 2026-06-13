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

        public bool NeedsBootstrap => CountNodes () < 4;

        /// <summary>
        /// Used to ensure known bootstrap routers do not get added to the
        /// routing table.
        /// </summary>
        HashSet<CompactEndPoint> NodesToIgnore { get; }

        public RoutingTable ()
            : this (NodeId.Create ())
        {

        }

        public RoutingTable (NodeId localNodeId)
        {
            Buckets = new List<Bucket> ();
            LocalNodeId = localNodeId;
            NodesToIgnore = new HashSet<CompactEndPoint> ();
            Add (new Bucket (NodeId.Minimum, NodeId.Maximum, 32));
        }

        public bool Add (Node node)
        {
            return Add (node, true);
        }

        bool Add (Node node, bool raiseNodeAdded)
        {
            if (node == null)
                throw new ArgumentNullException (nameof (node));

            if (NodesToIgnore.Contains (node.EndPoint))
                return false;

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

        public void AddIgnoredEndpoint (CompactEndPoint endpoint)
        {
            NodesToIgnore.Add (endpoint);
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
            var left = new Bucket (bucket.Min, median, Math.Max (Bucket.MaxCapacity, bucket.Nodes.Capacity / 2));
            var right = new Bucket (median, bucket.Max, Math.Max (Bucket.MaxCapacity, bucket.Nodes.Capacity / 2));

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
            foreach (var bucket in Buckets)
                foreach (var node in bucket.Nodes)
                    closestNodes.Add (node);
            return closestNodes;
        }

        internal void Clear ()
        {
            Buckets = new List<Bucket> {
                new Bucket ()
            };
        }

        internal void PreSplitBuckets (int minBucketCount)
        {
            while (Buckets.Count < minBucketCount) {
                var mine = Buckets.Find (b => b.CanContain (LocalNodeId))!;
                Split (mine);
            }
        }
    }
}
