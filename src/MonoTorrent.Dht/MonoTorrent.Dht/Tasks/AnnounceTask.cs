using MonoTorrent.Dht.Messages;
using System.Collections.Generic;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht
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
            
            IList<Node> nodes = engine.RoutingTable.GetClosest(infoHash);
            foreach(Node n in nodes)
            {
                GetPeers m = new GetPeers(engine.RoutingTable.LocalNode.Id, infoHash);
                engine.MessageLoop.EnqueueSend(m, n);
            }
    	}
        
        public void PeerFound(object sender, PeersFoundEventArgs e)
        {
            AnnouncePeer apmsg = new AnnouncePeer(engine.RoutingTable.LocalNode.Id, infoHash, engine.Port, ((Node)sender).Token);
            engine.MessageLoop.EnqueueSend(apmsg, (Node)sender);
            RaiseComplete(new TaskCompleteEventArgs(this));
        }
        
        public void NodeFound(object sender, NodeFoundEventArgs e)
        {
            GetPeers gpmsg = new GetPeers(engine.RoutingTable.LocalNode.Id, infoHash);
            engine.MessageLoop.EnqueueSend(gpmsg, e.Node);
        }

        protected override void RaiseComplete(TaskCompleteEventArgs e)
        {
            engine.PeersFound -= PeerFound;
            base.RaiseComplete(e);
        }
    }
}
