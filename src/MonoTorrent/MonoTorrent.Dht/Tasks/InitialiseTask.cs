using MonoTorrent.Dht.Messages;
using System;
using System.Net;
using System.Collections.Generic;
using MonoTorrent.BEncoding;
using System.Threading.Tasks;

namespace MonoTorrent.Dht.Tasks
{
    class InitialiseTask
    {
        int activeRequests = 0;
        List<Node> initialNodes;
        SortedList<NodeId, NodeId> nodes = new SortedList<NodeId, NodeId>();
        DhtEngine engine;

        public InitialiseTask(DhtEngine engine)
        {
            Initialise(engine, null);
        }

        public InitialiseTask(DhtEngine engine, byte[] initialNodes)
        {
            Initialise(engine, initialNodes == null ? null :  Node.FromCompactNode(initialNodes));
        }

        public InitialiseTask(DhtEngine engine, IEnumerable<Node> nodes)
        {
            Initialise(engine, nodes);
        }

        void Initialise(DhtEngine engine, IEnumerable<Node> nodes)
        {
            this.engine = engine;
            this.initialNodes = new List<Node>();
            if (nodes != null)
                initialNodes.AddRange(nodes);
        }

        public async void Execute()
        {
            try {
                await ExecuteAsync ();
            } catch {

            }
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
                Node utorrent = new Node(NodeId.Create(), new System.Net.IPEndPoint(Dns.GetHostEntry("router.bittorrent.com").AddressList[0], 6881));
                await SendFindNode(new Node[] { utorrent });
            }
        }

        private async Task SendFindNode(IEnumerable<Node> newNodes)
        {
            foreach (Node node in Node.CloserNodes(engine.LocalId, nodes, newNodes, Bucket.MaxCapacity))
            {
                FindNode request = new FindNode(engine.LocalId, engine.LocalId);
                activeRequests++;
                var args = await engine.SendQueryAsync (request, node);
                activeRequests--;

                if (!args.TimedOut) {
                    FindNodeResponse response = (FindNodeResponse)args.Response;
                    await SendFindNode (Node.FromCompactNode (response.Nodes));
                }

                if (activeRequests == 0) {
                    if (initialNodes.Count > 0 && engine.RoutingTable.CountNodes () < 10)
                        await new InitialiseTask (engine).ExecuteAsync ();
                }
            }
        }
    }
}
