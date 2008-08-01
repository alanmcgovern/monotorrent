using MonoTorrent.Dht.Messages;
using System;

namespace MonoTorrent.Dht
{
    internal class ReplacementTask : Task
    {
    	DhtEngine engine;
        Node replacement;
    	Bucket bucket;
        Node current;
    	
    	public ReplacementTask(DhtEngine engine, Node replacement)
    	{
            this.engine = engine;
            this.replacement = replacement;
            this.bucket = engine.RoutingTable.Buckets.Find(delegate(Bucket b) { return b.CanContain(replacement); });
    	}
    	
    	public void MessageReceive(object sender, EventArgs e)
    	{
            bucket.LastChanged = DateTime.Now;
       		PingForReplace();
    	}
    	
    	public void MessageTimedout(object sender, EventArgs e)
    	{
            Replace ();
    	}

        public override void Execute()
    	{
            if (!Active)
                return;
            PingForReplace();
    	}
    	
    	internal void PingForReplace()
    	{
            // FIXME: This is broke too
            /*
            bucket.Nodes.Sort();//max to min last seen
            foreach (Node n in bucket.Nodes)
            {
                if (!n.CurrentlyPinging && (n.State == NodeState.Unknown || n.State == NodeState.Questionable))
                {
                    current = n;
                    n.CurrentlyPinging = true;
                    Ping msg = new Ping(engine.RoutingTable.LocalNode.Id);
                    msg.QueryTimedOut += MessageTimedout;
                    msg.ResponseReceived += MessageReceive;
                    engine.MessageLoop.EnqueueSend(msg, n);
                    return;//ping only the first questionnable of bucket
                }
            }
            Complete(new TaskCompleteEventArgs(this));//do not succeed*/
    	}

        public void Replace ()
        {
            bucket.Nodes.Remove(current);
            bucket.Nodes.Add(replacement);
            bucket.LastChanged = DateTime.Now;
            RaiseComplete(new TaskCompleteEventArgs(this));
        }
    }
}
