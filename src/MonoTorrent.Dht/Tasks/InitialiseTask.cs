#if !DISABLE_DHT
using MonoTorrent.Dht.Messages;
using System;
using System.Net;
using System.Collections.Generic;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.Tasks
{
    internal class InitialiseTask : Task
    {
        private int activeRequests = 0;
        private List<Node> initialNodes;
        private SortedList<NodeId, NodeId> nodes = new SortedList<NodeId, NodeId>();
        private DhtEngine engine;

        public InitialiseTask(DhtEngine engine)
        {
            Initialise(engine, null);
        }

        public InitialiseTask(DhtEngine engine, byte[] initialNodes)
        {
            Initialise(engine, initialNodes == null ? null : Node.FromCompactNode(initialNodes));
        }

        public InitialiseTask(DhtEngine engine, IEnumerable<Node> nodes)
        {
            Initialise(engine, nodes);
        }

        private void Initialise(DhtEngine engine, IEnumerable<Node> nodes)
        {
            this.engine = engine;
            initialNodes = new List<Node>();
            if (nodes != null)
                initialNodes.AddRange(nodes);
        }

        public override void Execute()
        {
            if (Active)
                return;

            Active = true;

            // If we were given a list of nodes to load at the start, use them
            if (initialNodes.Count > 0)
            {
                foreach (var node in initialNodes)
                    engine.Add(node);
                SendFindNode(initialNodes);
            }
            else
            {
                try
                {
                    var utorrent = new Node(NodeId.Create(),
                        new IPEndPoint(Dns.GetHostEntry("router.bittorrent.com").AddressList[0], 6881));
                    SendFindNode(new Node[] {utorrent});
                }
                catch
                {
                    RaiseComplete(new TaskCompleteEventArgs(this));
                }
            }
        }

        private void FindNodeComplete(object sender, TaskCompleteEventArgs e)
        {
            e.Task.Completed -= FindNodeComplete;
            activeRequests--;

            var args = (SendQueryEventArgs) e;
            if (!args.TimedOut)
            {
                var response = (FindNodeResponse) args.Response;
                SendFindNode(Node.FromCompactNode(response.Nodes));
            }

            if (activeRequests == 0)
                RaiseComplete(new TaskCompleteEventArgs(this));
        }

        protected override void RaiseComplete(TaskCompleteEventArgs e)
        {
            if (!Active)
                return;

            // If we were given a list of initial nodes and they were all dead,
            // initialise again except use the utorrent router.
            if (initialNodes.Count > 0 && engine.RoutingTable.CountNodes() < 10)
            {
                new InitialiseTask(engine).Execute();
            }
            else
            {
                engine.RaiseStateChanged(DhtState.Ready);
            }

            Active = false;
            base.RaiseComplete(e);
        }

        private void SendFindNode(IEnumerable<Node> newNodes)
        {
            foreach (var node in Node.CloserNodes(engine.LocalId, nodes, newNodes, Bucket.MaxCapacity))
            {
                activeRequests++;
                var request = new FindNode(engine.LocalId, engine.LocalId);
                var task = new SendQueryTask(engine, request, node);
                task.Completed += FindNodeComplete;
                task.Execute();
            }
        }
    }
}

#endif