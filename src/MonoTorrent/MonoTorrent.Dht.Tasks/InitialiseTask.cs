//
// InitialiseTask.cs
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


using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    class InitialiseTask
    {
        readonly List<Node> initialNodes;
        readonly DhtEngine engine;

        public InitialiseTask (DhtEngine engine)
            : this (engine, Enumerable.Empty<Node> ())
        {

        }

        public InitialiseTask (DhtEngine engine, IEnumerable<Node> nodes)
        {
            this.engine = engine;
            initialNodes = new List<Node> (nodes);
        }

        public async Task ExecuteAsync ()
        {
            // If we were given a list of nodes to load at the start, use them
            if (initialNodes.Count > 0) {
                foreach (Node node in initialNodes)
                    engine.Add (node);
                await SendFindNode (initialNodes);
            } else {
                var utorrent = new Node (NodeId.Create (), new IPEndPoint (Dns.GetHostEntry ("router.bittorrent.com").AddressList[0], 6881));
                await SendFindNode (new[] { utorrent });
            }
        }

        async Task SendFindNode (IEnumerable<Node> newNodes)
        {
            var activeRequests = new List<Task<SendQueryEventArgs>> ();
            var nodes = new ClosestNodesCollection (engine.LocalId);

            foreach (Node node in newNodes) {
                var request = new FindNode (engine.LocalId, engine.LocalId);
                activeRequests.Add (engine.SendQueryAsync (request, node));
                nodes.Add (node);
            }

            while (activeRequests.Count > 0) {
                var completed = await Task.WhenAny (activeRequests);
                activeRequests.Remove (completed);

                SendQueryEventArgs args = await completed;
                if (args.Response != null) {
                    var response = (FindNodeResponse) args.Response;
                    foreach (Node node in Node.FromCompactNode (response.Nodes)) {
                        if (nodes.Add (node)) {
                            var request = new FindNode (engine.LocalId, engine.LocalId);
                            activeRequests.Add (engine.SendQueryAsync (request, node));
                        }
                    }
                }
            }

            if (initialNodes.Count > 0 && engine.RoutingTable.NeedsBootstrap)
                await new InitialiseTask (engine).ExecuteAsync ();
        }
    }
}
