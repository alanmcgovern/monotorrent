using MonoTorrent.Dht.Messages;
using System;

namespace MonoTorrent.Dht
{
    internal class BucketRefreshTask : Task<TaskCompleteEventArgs>, IMessageTask
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
    	
    	public void MessageReceive(ResponseMessage m)
    	{
            Complete(new TaskCompleteEventArgs(true));
    	}
    	
    	public void MessageTimedout(QueryMessage m)
    	{
            Complete(new TaskCompleteEventArgs(false));    		
    	}
    
        public override void Execute ()
    	{
            if (!Active)
                return;
    			
            Node n = bucket.Nodes[rand.Next(bucket.Nodes.Count-1)];
            FindNode msg = new FindNode(engine.RoutingTable.LocalNode.Id, n.Id);
            msg.Task = this;
            engine.MessageLoop.EnqueueSend(msg, n);
    	}
    }
}
