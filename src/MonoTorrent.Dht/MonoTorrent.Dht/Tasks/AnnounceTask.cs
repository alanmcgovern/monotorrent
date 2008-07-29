using MonoTorrent.Dht.Messages;
using System.Collections.Generic;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht
{
    internal class AnnounceTask : Task<TaskCompleteEventArgs>
    {
    	NodeId infoHash;
    	DhtEngine engine;
    	
    	public AnnounceTask(byte[] infohash, DhtEngine engine)
    	{
    		this.infoHash = new NodeId(infohash);
    		this.engine = engine;
    	}
    	

        //task timeout?
        
        public override void Execute ()
    	{
    		if (!Active)
    			return;
    			
            engine.PeersFound += PeerFound;
            engine.NodeGot += NodeFound;
            
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
            engine.PeersFound -= PeerFound;
            engine.NodeGot -= NodeFound;
            Complete(new TaskCompleteEventArgs(true));
        }
        
        public void NodeFound(object sender, NodeFoundEventArgs e)
        {
            GetPeers gpmsg = new GetPeers(engine.RoutingTable.LocalNode.Id, infoHash);
            engine.MessageLoop.EnqueueSend(gpmsg, e.Node);
        }    
    }
}
