using MonoTorrent.Dht.Messages;
using System;
using System.Net;
using System.Collections.Generic;
using MonoTorrent.BEncoding;
using System.Threading.Tasks;
using System.Linq;

namespace MonoTorrent.Dht.Tasks
{
    class InitialiseTask
    {
        readonly List<Node> initialNodes;
        readonly DhtEngine engine;

        public InitialiseTask(DhtEngine engine)
            : this (engine, Enumerable.Empty<Node> ())
        {

        }

        public InitialiseTask(DhtEngine engine, IEnumerable<Node> nodes)
        {
            this.engine = engine;
            initialNodes = new List<Node>(nodes);
        }

        public async Task ExecuteAsync()
        {
            // If we were given a list of nodes to load at the start, use them
            if (initialNodes.Count > 0)
            {
                foreach (Node node in initialNodes)
                    engine.Add(node);
                await SendFindNode(initialNodes);
            }
            else
            {
                Node utorrent = new Node(NodeId.Create(), new IPEndPoint(Dns.GetHostEntry("router.bittorrent.com").AddressList[0], 6881));
                await SendFindNode(new Node[] { utorrent });
            }
        }

        async Task SendFindNode(IEnumerable<Node> newNodes)
        {
            var activeRequests = new List<Task<SendQueryEventArgs>> ();
            var nodes = new ClosestNodesCollection (engine.LocalId);

            foreach (var node in newNodes) {
                var request = new FindNode(engine.LocalId, engine.LocalId);
                activeRequests.Add (engine.SendQueryAsync (request, node));
                nodes.Add (node);
            }

            while (activeRequests.Count > 0) {
                var completed = await Task.WhenAny (activeRequests);
                activeRequests.Remove (completed);

                var args = await completed;
                if (args.Response != null) {
                    var response = (FindNodeResponse)args.Response;
                    foreach (var node in Node.FromCompactNode (response.Nodes)) {
                        if (nodes.Add (node)) {
                            var request = new FindNode(engine.LocalId, engine.LocalId);
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
