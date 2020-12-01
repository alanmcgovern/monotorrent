//
// ClosestNodesCollection.cs
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
using System.Collections;
using System.Collections.Generic;

namespace MonoTorrent.Dht
{
    sealed class ClosestNodesCollection : ICollection<Node>
    {
        sealed class DistanceComparer : IComparer<ValueTuple<NodeId, Node>>
        {
            public static readonly DistanceComparer Instance = new DistanceComparer ();

            public int Compare (ValueTuple<NodeId, Node> x, ValueTuple<NodeId, Node> y)
            {
                int result = x.Item1.CompareTo (y.Item1);
                if (result == 0)
                    result = x.Item2.Id.CompareTo (y.Item2.Id);
                return result;
            }
        }

        public int Capacity => Nodes.Capacity;
        public int Count => Nodes.Count;
        public bool IsReadOnly => false;
        public NodeId Target { get; }

        List<ValueTuple<NodeId, Node>> Nodes { get; }

        public ClosestNodesCollection (NodeId target)
            : this (target, Bucket.MaxCapacity)
        {

        }

        public ClosestNodesCollection (NodeId target, int capacity)
        {
            Nodes = new List<ValueTuple<NodeId, Node>> (capacity);
            Target = target;
        }

        public bool Add (Node item)
        {
            var kvp = ValueTuple.Create (item.Id ^ Target, item);

            // The item is already here!
            int insertionIndex = IndexOf (kvp);
            if (insertionIndex >= 0)
                return false;

            // Our list is at max capacity and this node is further away than all others.
            if (~insertionIndex == Capacity)
                return false;

            // If we're going to insert a new node and we're at capacity, remove the
            // furthest node.
            if (Nodes.Count == Nodes.Capacity)
                Nodes.RemoveAt (Nodes.Capacity - 1);

            Nodes.Insert (~insertionIndex, kvp);
            return true;
        }

        void ICollection<Node>.Add (Node item)
        {
            Add (item);
        }

        public void Clear ()
        {
            Nodes.Clear ();
        }

        public bool Contains (Node item)
        {
            return IndexOf (item) >= 0;
        }

        void ICollection<Node>.CopyTo (Node[] array, int arrayIndex)
        {
            for (int i = 0; i < Count; i++)
                array[arrayIndex++] = Nodes[i].Item2;
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }

        public IEnumerator<Node> GetEnumerator ()
        {
            foreach ((NodeId, Node) v in Nodes)
                yield return v.Item2;
        }

        public int IndexOf (Node item)
        {
            return IndexOf (ValueTuple.Create (item.Id ^ Target, item));
        }

        int IndexOf (ValueTuple<NodeId, Node> item)
        {
            return Nodes.BinarySearch (item, DistanceComparer.Instance);
        }

        public bool Remove (Node item)
        {
            int index = IndexOf (item);
            if (index >= 0)
                Nodes.RemoveAt (index);
            return index >= 0;
        }
    }
}
