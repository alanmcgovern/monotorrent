using MonoTorrent.Dht.Messages;
using System;
using System.Net;
using System.Collections.Generic;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.Tasks
{
    class InitialiseTask : Task
    {
        int activeRequests = 0;
        byte[] initialNodes;
        SortedList<NodeId, NodeId> nodes = new SortedList<NodeId, NodeId>();
        DhtEngine engine;
            
        public InitialiseTask(DhtEngine engine, byte[] initialNodes)
        {
            this.engine = engine;
            this.initialNodes = initialNodes;
        }
        
        public override void Execute()
        {
            if (Active)
                return;

            Active = true;

            // If we were given a list of nodes to load at the start, use them
            if (initialNodes != null)
            {
                BEncodedList list = (BEncodedList)BEncodedValue.Decode(initialNodes);
                foreach (BEncodedString s in list)
                    engine.Add(Node.FromCompactNode(s.TextBytes, 0));
            }
            else
            {
                Node utorrent = new Node(NodeId.Create(), new System.Net.IPEndPoint(Dns.GetHostEntry("router.bittorrent.com").AddressList[0], 6881));
                SendFindNode(new Node[] { utorrent });
            }
        }

        private void FindNodeComplete(object sender, TaskCompleteEventArgs e)
        {
            e.Task.Completed -= FindNodeComplete;
            activeRequests--;

            SendQueryEventArgs args = (SendQueryEventArgs)e;
            if (!args.TimedOut)
            {
                FindNodeResponse response = (FindNodeResponse)args.Response;
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
            if (nodes != null && engine.RoutingTable.CountNodes() < 10)
            {
                new InitialiseTask(engine, null);
            }
            else
            {
                engine.RaiseStateChanged(State.Ready);
            }

            Active = false;
            base.RaiseComplete(e);
        }

        private void SendFindNode(IEnumerable<Node> newNodes)
        {
            foreach (Node node in Node.CloserNodes(engine.LocalId, nodes, newNodes, Bucket.MaxCapacity))
            {
                activeRequests++;
                FindNode request = new FindNode(engine.LocalId, engine.LocalId);
                SendQueryTask task = new SendQueryTask(engine, request, node);
                task.Completed += FindNodeComplete;
                task.Execute();
            }
        }
    }
}