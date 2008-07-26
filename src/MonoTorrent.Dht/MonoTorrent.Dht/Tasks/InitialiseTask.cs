using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    class InitialiseTask : Task
    {
        static readonly int NodesToFind = 50;
        static readonly TimeSpan MaxTime = TimeSpan.FromSeconds(30);
        private int nodesFound;
        private DateTime startTime;
        private bool registered;
        private DhtEngine engine;

        public InitialiseTask(DhtEngine engine)
        {
            this.engine = engine;
        }

        public override void Execute(DhtEngine engine)
        {
            if (Active)
                return;

            Active = true;
            startTime = DateTime.Now;
            nodesFound = 0;
            engine.RoutingTable.NodeAdded += NodeAdded;

            Node utorrent = new Node(NodeId.Create(), new IPEndPoint(Dns.GetHostEntry("router.bittorrent.com").AddressList[0], 6881));
            Node node2 = new Node(NodeId.Create(), new IPEndPoint(Dns.GetHostEntry("router.utorrent.com").AddressList[0], 6881));
            engine.Add(utorrent);
            engine.Add(node2);
        }

        public override void Cancel()
        {
            if (Active)
                engine.RoutingTable.NodeAdded -= NodeAdded;

            base.Cancel();
        }

        protected override void RaiseComplete(TaskCompleteEventArgs e)
        {
            if (Active)
                engine.RoutingTable.NodeAdded -= NodeAdded;

            base.RaiseComplete(e);
        }

        private void NodeAdded(object o, NodeAddedEventArgs e)
        {
            // If we reached our target amount of nodes or we've run out of time, complete the task
            // Otherwise keep firing off FindNode requests to find nodes close to our own.
            if ((DateTime.Now - startTime) > MaxTime || nodesFound >= NodesToFind)
            {
                RaiseComplete(new TaskCompleteEventArgs(nodesFound != 0));
            }
            else
            {
                // When initialising, we try to find nodes close to our own ID
                engine.MessageLoop.EnqueueSend(new FindNode(engine.RoutingTable.LocalNode.Id, engine.RoutingTable.LocalNode.Id), e.Node);
            }
        }
    }
}
