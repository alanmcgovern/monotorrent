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

using Mono.Math;

namespace MonoTorrent.Dht
{
	/// <summary>
	/// This class holds a maximum amount of 8 Nodes and is itself a child of a RoutingTable
	/// </summary>
	internal class Bucket : IComparable<Bucket>
	{
		public const int MaxCapacity = 8;

        DateTime lastChanged = DateTime.Now;
        NodeId max;
        NodeId min;
		List<Node> nodes = new List<Node>(MaxCapacity);
        Node replacement;

        public DateTime LastChanged
        {
            get { return lastChanged; }
            set { lastChanged = value; }
        }

        public NodeId Max
        {
            get { return max; }
        }

        public NodeId Min
        {
            get { return min; }
        }

        public List<Node> Nodes
        {
            get { return nodes; }
        }

        internal Node Replacement
        {
            get { return replacement; }
            set { replacement = value; }
        }

        //TODO avoid calcul and made it with hardcoded const NodeIdDefaultMaxBuffer = 
        public Bucket()
            : this(new NodeId(0), new NodeId(2).Pow(160))
        {

        }

        public Bucket(NodeId min, NodeId max)
		{
			this.min = min;
			this.max = max;
		}
		
        public bool Add(Node node)
        {
            // if the current bucket is not full we directly add the Node
            if (nodes.Count < MaxCapacity)
            {
                nodes.Add(node);
                node.Bucket = this;
                lastChanged = DateTime.Now;
                return true;
            }
            //test replace
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                if (nodes[i].State != NodeState.Bad)
                    continue;

                nodes.RemoveAt(i);
                nodes.Add(node);
                node.Bucket = this;
                lastChanged = DateTime.Now;
                return true;
            }
            //queue for replace fresh node
            replacement = node;
            node.Bucket = this;
            return false;
		}

        public void Replace (Node oldNode)
        {
            nodes.Remove(oldNode);
            nodes.Add(replacement);
            lastChanged = DateTime.Now;
            this.replacement = null;
        }
        
        internal void PingForReplace(DhtEngine engine)
        {
            Nodes.Sort();//max to min last seen
            foreach (Node n in Nodes)
            {
                if (!n.CurrentlyPinging && (n.State == NodeState.Unknown || n.State == NodeState.Questionable))
                {
                    n.CurrentlyPinging = true;
                    engine.MessageLoop.EnqueueSend(new Messages.Ping(engine.RoutingTable.LocalNode.Id), n);
                    return;//ping only the first questionnable of bucket
                }
            }
        }
        
        public int CompareTo(Bucket other)
        {
            return min.CompareTo(other.min);
        }

        public override string ToString()
        {
            return string.Format("Count: {2} Min: {0}  Max: {1}", min, max, this.nodes.Count);
        }
    }
}
