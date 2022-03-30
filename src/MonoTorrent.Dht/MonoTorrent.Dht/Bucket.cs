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
using System.Collections;
using System.Collections.Generic;

namespace MonoTorrent.Dht
{
    /// <summary>
    /// This class holds a maximum amount of 8 Nodes and is itself a child of a RoutingTable
    /// </summary>
    class Bucket : IComparable<Bucket>, IEquatable<Bucket>, IEnumerable<Node>
    {
        public const int MaxCapacity = 8;

        // The item at position 0 will be the one we have not seen in the longest time.
        // The last item in the list is one we have seen the most recently.
        static readonly Comparison<Node> LastSeenComparer = (l, r) => r.LastSeen.CompareTo (l.LastSeen);

        internal bool CanSplit { get; }
        public TimeSpan LastChanged => LastChangedTimer.Elapsed + LastChangedDelta;
        TimeSpan LastChangedDelta { get; set; }
        ValueStopwatch LastChangedTimer;
        public NodeId Max { get; }
        public NodeId Min { get; }
        public List<Node> Nodes { get; }
        internal Node? Replacement { get; set; }

        public Bucket ()
            : this (NodeId.Minimum, NodeId.Maximum)
        {

        }

        public Bucket (NodeId min, NodeId max)
        {
            Min = min ?? throw new ArgumentNullException (nameof (min));
            Max = max ?? throw new ArgumentNullException (nameof (max));

            CanSplit = (Max - Min) > MaxCapacity;
            LastChangedDelta = TimeSpan.FromDays (1);
            LastChangedTimer = new ValueStopwatch ();
            Nodes = new List<Node> (MaxCapacity);
        }

        public bool Add (Node node)
        {
            // if the current bucket is not full we directly add the Node
            if (Nodes.Count < MaxCapacity) {
                Nodes.Add (node);
                Changed ();
                return true;
            }
            //test replace

            for (int i = Nodes.Count - 1; i >= 0; i--) {
                if (Nodes[i].State != NodeState.Bad)
                    continue;

                Nodes.RemoveAt (i);
                Nodes.Add (node);
                Changed ();
                return true;
            }
            return false;
        }

        public bool CanContain (Node node)
        {
            if (node == null)
                throw new ArgumentNullException (nameof (node));
            return CanContain (node.Id);
        }

        public bool CanContain (NodeId id)
        {
            if (id == null)
                throw new ArgumentNullException (nameof (id));

            return Min <= id && Max > id;
        }

        internal void Changed ()
        {
            Changed (TimeSpan.Zero);
        }

        internal void Changed (TimeSpan delta)
        {
            LastChangedDelta = delta;
            LastChangedTimer.Restart ();
        }

        public int CompareTo (Bucket? other)
        {
            return other == null ? 1 : Min.CompareTo (other.Min);
        }

        public override bool Equals (object? obj)
        {
            return Equals (obj as Bucket);
        }

        public bool Equals (Bucket? other)
        {
            return Min.Equals (other?.Min) && Max.Equals (other?.Max);
        }

        public IEnumerator<Node> GetEnumerator ()
        {
            return Nodes.GetEnumerator ();
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }

        public override int GetHashCode ()
        {
            return Min.GetHashCode () ^ Max.GetHashCode ();
        }

        internal void SortBySeen ()
        {
            Nodes.Sort (LastSeenComparer);
        }

        public override string ToString ()
        {
            return string.Format ("Count: {2} Min: {0}  Max: {1}", Min, Max, Nodes.Count);
        }
    }
}
