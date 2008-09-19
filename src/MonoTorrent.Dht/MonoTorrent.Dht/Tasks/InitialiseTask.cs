using MonoTorrent.Dht.Messages;
using System;
using System.Net;
using System.Collections.Generic;

namespace MonoTorrent.Dht.Tasks
{
    internal class InitialiseTask : Task
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

            // This shouldn't be needed
            //DhtEngine.MainLoop.QueueTimeout(TimeSpan.FromMinutes(1), delegate {
            //    if (Active)
            //        RaiseComplete(new TaskCompleteEventArgs(this));
            //    return false;
            //});

            Active = true;

            Node utorrent = new Node(NodeId.Create(), new System.Net.IPEndPoint(Dns.GetHostEntry("router.bittorrent.com").AddressList[0], 6881));
            SendFindNode(utorrent);
        }

        private void FindNodeComplete(object sender, TaskCompleteEventArgs e)
        {
            e.Task.Completed -= FindNodeComplete;
            activeRequests--;

            SendQueryEventArgs args = (SendQueryEventArgs)e;
            if (!args.TimedOut)
            {
                FindNodeResponse response = (FindNodeResponse)args.Response;
                foreach (Node n in Node.FromCompactNode(response.Nodes))
                    SendFindNode(n);
            }

            if (activeRequests == 0)
                RaiseComplete(new TaskCompleteEventArgs(this));
        }

        protected override void RaiseComplete(TaskCompleteEventArgs e)
        {
            if (!Active)
                return;

            Active = false;
            base.RaiseComplete(e);
        }

        private void SendFindNode(Node node)
        {
            NodeId distance = node.Id.Xor(engine.LocalId);
            if (nodes.Count < Bucket.MaxCapacity)
            {
                nodes.Add(distance, node.Id);
            }
            else if (distance < nodes.Keys[nodes.Count - 1] && !nodes.Values.Contains(node.Id))
            {
                nodes.RemoveAt(nodes.Count - 1);
                nodes.Add(distance, node.Id);
            }
            else
            {
                return;
            }

            activeRequests++;
            FindNode request = new FindNode(engine.LocalId, engine.LocalId);
            SendQueryTask task = new SendQueryTask(engine, request, node);
            task.Completed += FindNodeComplete;
        }
    }
}