using MonoTorrent.Dht.Messages;
using System.Collections.Generic;
using MonoTorrent.BEncoding;
using System;

namespace MonoTorrent.Dht.Tasks
{
    internal class AnnounceTask : Task
    {
    	NodeId infoHash;
    	DhtEngine engine;
        SortedList<NodeId, NodeId> closestNodes;

    	public AnnounceTask(DhtEngine engine, byte[] infohash)
            : this(engine, new NodeId(infohash))
    	{
    		
    	}

        public AnnounceTask(DhtEngine engine, NodeId infohash)
        {
            this.engine = engine;
            this.infoHash = infohash;
            this.closestNodes = new SortedList<NodeId, NodeId>(Bucket.MaxCapacity);
            DhtEngine.MainLoop.QueueTimeout(TimeSpan.FromMinutes(1), delegate {
                RaiseComplete(new TaskCompleteEventArgs(this));
                return false;
            });
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
            if (!Active)
                return;

            GetPeers m = new GetPeers(engine.RoutingTable.LocalNode.Id, infoHash);
            SendQueryTask task = new SendQueryTask(engine, m, n);
            task.Completed += GetPeersCompleted;
            task.Execute();
        }

        private void GetPeersCompleted(object o, TaskCompleteEventArgs e)
        {
            SendQueryEventArgs args = (SendQueryEventArgs)e;
            e.Task.Completed -= GetPeersCompleted;

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
                foreach (Node n in Node.FromCompactNode(response.Nodes))
                {
                    // If we attempt to add a node to the engine, if the
                    // bucket is already full it will be silently dropped.
                    // Therefore we should always just send a getpeers message
                    // without bothering to verify the node is still alive
                    engine.Add(n);

                    // Only bother pinging the node if it's closer
                    // than everything else we've tried.
                    NodeId distance = n.Id.Xor(infoHash);
                    if (closestNodes.Count < Bucket.MaxCapacity)
                    {
                        closestNodes.Add(distance, n.Id);
                        SendGetPeers(n);
                    }
                    else if(distance < closestNodes.Keys[closestNodes.Count - 1])
                    {
                        closestNodes.RemoveAt(closestNodes.Count - 1);
                        closestNodes.Add(distance, n.Id);
                        SendGetPeers(n);
                    }
                }
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
