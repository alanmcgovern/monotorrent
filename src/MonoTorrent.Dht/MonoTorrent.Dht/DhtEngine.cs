//
// DhtEngine.cs
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
using System.Net;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

using MonoTorrent;
using MonoTorrent.Common;
using MonoTorrent.Client;
using MonoTorrent.BEncoding;
using System.IO;
using MonoTorrent.Dht.Listeners;

namespace MonoTorrent.Dht
{
	public class DhtEngine
	{
        public event EventHandler StateChanged;

        State state = State.NotReady;
        MessageLoop messageLoop;
        RoutingTable table = new RoutingTable();
        int timeout;

        internal MessageLoop MessageLoop
        {
            get { return messageLoop; }
        }

        internal RoutingTable RoutingTable
        {
            get { return table; }
        }

        public State State
        {
            get { return state; }
        }

        internal int TimeOut
        {
            get { return timeout; }
            set { timeout = value; }
        }

        public DhtEngine(IListener listener)
        {
            messageLoop = new MessageLoop(this, listener);
            timeout = 20 * 1000; // 20 second message timeout by default
        }

        public void Add(IEnumerable<Node> nodes)
        {
            if (nodes == null)
                throw new ArgumentNullException("nodes");

            foreach (Node n in nodes)
                Add(n);
        }

        public void Add(Node node)
        {
            if (node == null)
                throw new ArgumentNullException("node");
            
            table.Add(node);
        }

        public void Start()
        {
            RaiseStateChanged(State.Initialising);
        }

        private void RaiseStateChanged(State newState)
        {
            table.Initialise();
            state = newState;

            if (StateChanged != null)
                StateChanged(this, EventArgs.Empty);
        }
    }
}
