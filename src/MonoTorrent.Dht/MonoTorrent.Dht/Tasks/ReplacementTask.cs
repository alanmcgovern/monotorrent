using MonoTorrent.Dht.Messages;
using System;

namespace MonoTorrent.Dht
{
    internal class ReplacementTask : Task<TaskCompleteEventArgs>, IMessageTask
    {
    	DhtEngine engine;
        Node replacement;
    	Bucket bucket;
        Node current;
    	
    	public ReplacementTask(DhtEngine engine, Node replacement, Bucket bucket)
    	{
    		this.engine = engine;
    		this.replacement = replacement;
    		this.bucket = bucket;
    	}
    	
    	public void MessageReceive(ResponseMessage m)
    	{
            bucket.LastChanged = DateTime.Now;
       		PingForReplace();
    	}
    	
    	public void MessageTimedout(QueryMessage m)
    	{
   			Replace ();
    	}
    
        public override void Execute ()
    	{
    		if (!Active)
    			return;
    		PingForReplace();
    	}
    	
    	internal void PingForReplace()
    	{
    		bucket.Nodes.Sort();//max to min last seen
    		foreach (Node n in bucket.Nodes)
    		{
    			if (!n.CurrentlyPinging && (n.State == NodeState.Unknown || n.State == NodeState.Questionable))
    			{
                    current = n;
    				n.CurrentlyPinging = true;
    				Ping msg = new Ping(engine.RoutingTable.LocalNode.Id);
    				msg.Task = this;
    				engine.MessageLoop.EnqueueSend(msg, n);
    				return;//ping only the first questionnable of bucket
    			}
    		}
    		Complete(new TaskCompleteEventArgs(false));//do not succeed
    	}

        public void Replace ()
        {
            bucket.Nodes.Remove(current);
            bucket.Nodes.Add(replacement);
            bucket.LastChanged = DateTime.Now;
            Complete(new TaskCompleteEventArgs(true));
        }
    }
}