using MonoTorrent.Dht.Messages;
using System;
using System.Net;

namespace MonoTorrent.Dht
{
    internal class InitialiseTask : Task
    {
        static readonly int NodesToFind = 50;
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private int nodesFound;
        private DhtEngine engine;
        private DateTime startTime;

        public InitialiseTask(DhtEngine engine)
        {
            this.engine = engine;
        }
        
        public override void Execute ()
        {
            if (Active)
                return;

            nodesFound = 0;
            startTime = DateTime.UtcNow;
            engine.RoutingTable.NodeAdded += NodeAdded;
            DhtEngine.MainLoop.QueueTimeout(Timeout, delegate {
                if (Active)
                    RaiseComplete(new TaskCompleteEventArgs(this));
                return false;
            });
            Node utorrent = new Node(NodeId.Create(), new System.Net.IPEndPoint(Dns.GetHostEntry("router.bittorrent.com").AddressList[0], 6881));
            Node node2 = new Node(NodeId.Create(), new IPEndPoint(Dns.GetHostEntry("router.utorrent.com").AddressList[0], 6881));
            engine.Add(utorrent);
            engine.Add(node2);
        }

        private void NodeAdded(object o, NodeAddedEventArgs e)
        {
            nodesFound++;
            // If we reached our target amount of nodes or we've run out of time, complete the task
            // Otherwise keep firing off FindNode requests to find nodes close to our own.
            if ((DateTime.UtcNow - startTime) > Timeout || nodesFound >= NodesToFind)
            {
                RaiseComplete(new TaskCompleteEventArgs(this));
            }
            else
            {
                engine.MessageLoop.EnqueueSend(new FindNode(engine.RoutingTable.LocalNode.Id, engine.RoutingTable.LocalNode.Id), e.Node);
            }
        }

        protected override void RaiseComplete(TaskCompleteEventArgs e)
        {
            if (Active)
                engine.RoutingTable.NodeAdded -= NodeAdded;
            base.RaiseComplete(e);
        }
    }
}