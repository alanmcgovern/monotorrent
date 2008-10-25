using MonoTorrent.Dht.Messages;
using System;
using System.Net;
using System.Collections.Generic;

namespace MonoTorrent.Dht.Tasks
{
    class InitialiseTask : Task
    {
        int activeRequests = 0;
        SortedList<NodeId, NodeId> nodes = new SortedList<NodeId, NodeId>();
        DhtEngine engine;
            
        public InitialiseTask(DhtEngine engine)
        {
            this.engine = engine;
        }
        
        public override void Execute ()
        {
            if (Active)
                return;

            Active = true;

            Node utorrent = new Node(NodeId.Create(), new System.Net.IPEndPoint(Dns.GetHostEntry("router.bittorrent.com").AddressList[0], 6881));
            SendFindNode(new Node[] { utorrent });
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

            engine.RaiseStateChanged(State.Ready);

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