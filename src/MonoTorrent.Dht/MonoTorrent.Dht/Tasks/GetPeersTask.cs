using MonoTorrent.Dht.Messages;
using System.Collections.Generic;
using MonoTorrent.BEncoding;
using System;

namespace MonoTorrent.Dht.Tasks
{
    internal class GetPeersTask : Task
    {
    	NodeId infoHash;
    	DhtEngine engine;
        int activeQueries;
        SortedList<NodeId, NodeId> closestNodes;

    	public GetPeersTask(DhtEngine engine, byte[] infohash)
            : this(engine, new NodeId(infohash))
    	{
    		
    	}

        public GetPeersTask(DhtEngine engine, NodeId infohash)
        {
            this.engine = engine;
            this.infoHash = infohash;
            this.closestNodes = new SortedList<NodeId, NodeId>(Bucket.MaxCapacity);
        }

        public override void Execute()
        {
            if (Active)
                return;

            Active = true;
            foreach (Node n in engine.RoutingTable.GetClosest(infoHash))
            {
                closestNodes.Add(n.Id.Xor(infoHash), n.Id);
                SendGetPeers(n);
            }
        }

        private void SendGetPeers(Node n)
        {
            activeQueries++;
            GetPeers m = new GetPeers(engine.RoutingTable.LocalNode.Id, infoHash);
            SendQueryTask task = new SendQueryTask(engine, m, n);
            task.Completed += GetPeersCompleted;
            task.Execute();
        }

        private void GetPeersCompleted(object o, TaskCompleteEventArgs e)
        {
            try
            {
                activeQueries--;
                e.Task.Completed -= GetPeersCompleted;

                SendQueryEventArgs args = (SendQueryEventArgs)e;
                if (args.TimedOut)
                    return;

                GetPeersResponse response = (GetPeersResponse)args.Response;
                if (response.Values != null)
                {
                    // We have actual peers!
                    engine.RaisePeersFound(infoHash, MonoTorrent.Client.Peer.Decode(response.Values));
                }
                else if (response.Nodes != null)
                {
                    if (!Active)
                        return;

                    // We got a list of nodes which are closer
                    IEnumerable<Node> newNodes = Node.FromCompactNode(response.Nodes);
                    foreach (Node n in Node.CloserNodes(infoHash, closestNodes, newNodes, Bucket.MaxCapacity))
                        SendGetPeers(n);
                }
            }
            finally
            {
                if (activeQueries == 0)
                    RaiseComplete(new TaskCompleteEventArgs(this));
            }
        }

        protected override void RaiseComplete(TaskCompleteEventArgs e)
        {
            if (!Active)
                return;

            Active = false;
            base.RaiseComplete(e);
        }
    }
}
