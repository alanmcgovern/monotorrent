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
        List<Node> nodes;
    	
    	public AnnounceTask(DhtEngine engine, byte[] infohash)
            : this(engine, new NodeId(infohash))
    	{
    		
    	}

        public AnnounceTask(DhtEngine engine, NodeId infohash)
        {
            this.engine = engine;
            this.infoHash = infohash;
            this.nodes = new List<Node>(24);
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
            engine.RoutingTable.NodeAdded += NodeFound;

            foreach (Node n in engine.RoutingTable.GetClosest(infoHash))
                SendGetPeers(n);
        }

        private void SendGetPeers(Node n)
        {
            nodes.Add(n);
            GetPeers m = new GetPeers(engine.RoutingTable.LocalNode.Id, infoHash);
            SendQueryTask task = new SendQueryTask(engine, m, n);
            task.Completed += GetPeersCompleted;
            task.Execute();
        }

        private void GetPeersCompleted(object o, TaskCompleteEventArgs e)
        {
            SendQueryEventArgs args = (SendQueryEventArgs)e;
            e.Task.Completed -= GetPeersCompleted;
            
            GetPeersResponse response = (GetPeersResponse)args.Response;
            Node node = nodes.Find(delegate(Node n) { return n.Id == response.Id; });
            nodes.Remove(node);

            if (response.Values != null)
            {
                // We have actual peers!
                engine.RaisePeersFound(node, infoHash, MonoTorrent.Client.Peer.Decode(response.Values));
            }
            else if (response.Nodes != null)
            {
                // We got a list of nodes which are closer
                foreach (Node n in Node.FromCompactNode(response.Nodes))
                    engine.Add(n);
            }
        }
        
        public void NodeFound(object sender, NodeAddedEventArgs e)
        {
            nodes.Add(e.Node);
            SendGetPeers(e.Node);
        }

        protected override void RaiseComplete(TaskCompleteEventArgs e)
        {
            if (!Active)
                return;

            Active = false;
            engine.RoutingTable.NodeAdded -= NodeFound;
            base.RaiseComplete(e);
        }
    }
}
