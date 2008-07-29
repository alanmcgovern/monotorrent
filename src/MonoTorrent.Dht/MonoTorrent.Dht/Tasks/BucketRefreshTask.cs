using MonoTorrent.Dht.Messages;
using System;

namespace MonoTorrent.Dht
{
    internal class BucketRefreshTask : Task<TaskCompleteEventArgs>
    {
        Random rand;
    	DhtEngine engine;
    	Bucket bucket;
    	
    	public BucketRefreshTask(DhtEngine engine, Bucket bucket)
    	{
            rand = new Random();
            this.engine = engine;
            this.bucket = bucket;
    	}
    	
    	public void MessageReceive(object sender, EventArgs e)
    	{
            Complete(new TaskCompleteEventArgs(true));
    	}
    	
    	public void MessageTimedout(object sender, EventArgs e)
    	{
            Complete(new TaskCompleteEventArgs(false));    		
    	}
    
        public override void Execute ()
    	{
            if (!Active)
                return;
    			
            Node n = bucket.Nodes[rand.Next(bucket.Nodes.Count-1)];
            FindNode msg = new FindNode(engine.RoutingTable.LocalNode.Id, n.Id);
            msg.QueryTimedOut += MessageTimedout;
            msg.ResponseReceived += MessageReceive;
            engine.MessageLoop.EnqueueSend(msg, n);
    	}
    }
}
