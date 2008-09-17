using MonoTorrent.Dht.Messages;
using System.Collections.Generic;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.Tasks
{
    internal class AnnounceTask : Task
    {
    	NodeId infoHash;
    	DhtEngine engine;
    	
    	public AnnounceTask(DhtEngine engine, byte[] infohash)
            : this(engine, new NodeId(infohash))
    	{
    		
    	}

        public AnnounceTask(DhtEngine engine, NodeId infohash)
        {
            this.engine = engine;
            this.infoHash = infohash;
        }
    	
        public override void Execute()
    	{
            engine.PeersFound += PeerFound;
            engine.RoutingTable.NodeAdded += NodeFound;
            
            IList<Node> nodes = engine.RoutingTable.GetClosest(infoHash);
            foreach(Node n in nodes)
            {
                GetPeers m = new GetPeers(engine.RoutingTable.LocalNode.Id, infoHash);
                engine.MessageLoop.EnqueueSend(m, n);
            }
    	}
        
        public void PeerFound(object sender, PeersFoundEventArgs e)
        {
            Node node = (Node)sender;
            AnnouncePeer apmsg = new AnnouncePeer(engine.RoutingTable.LocalNode.Id, infoHash, engine.Port, node.Token);
            engine.MessageLoop.EnqueueSend(apmsg, node);
            RaiseComplete(new TaskCompleteEventArgs(this));
        }
        
        public void NodeFound(object sender, NodeAddedEventArgs e)
        {
            GetPeers gpmsg = new GetPeers(engine.RoutingTable.LocalNode.Id, infoHash);
            engine.MessageLoop.EnqueueSend(gpmsg, e.Node);
        }

        protected override void RaiseComplete(TaskCompleteEventArgs e)
        {
            engine.PeersFound -= PeerFound;
            engine.RoutingTable.NodeAdded -= NodeFound;
            base.RaiseComplete(e);
        }
    }
}
