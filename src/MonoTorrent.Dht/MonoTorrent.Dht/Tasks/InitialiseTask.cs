using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht
{
    internal class InitialiseTask : Task<TaskCompleteEventArgs>, IMessageTask
    {
    	DhtEngine engine;
    	private int bootstrapCount;
    	public InitialiseTask(DhtEngine engine)
    	{
            this.engine = engine;
            bootstrapCount = 0;
    	}
    
    	public void MessageReceive(ResponseMessage m)
    	{
            bootstrapCount--;
            if (bootstrapCount == 0 || !Active)
            {
                engine.NodeFound -= NodeAdded;
                Complete(new TaskCompleteEventArgs(true));
            }
    	}
    	
    	public void MessageTimedout(QueryMessage m)
    	{
            bootstrapCount--;
            if (bootstrapCount == 0 || !Active)
            {
                engine.NodeFound -= NodeAdded;
                Complete(new TaskCompleteEventArgs(true));
            }
    	}
    	
        public override void Execute ()
    	{
            if (!Active)
                return;
   		
            engine.NodeFound += NodeAdded;
    	}
    	
    	void NodeAdded(object sender, NodeFoundEventArgs e)
        {
            bootstrapCount++;
            FindNode msg = new FindNode(engine.RoutingTable.LocalNode.Id, engine.RoutingTable.LocalNode.Id);
            msg.Task = this;
            engine.MessageLoop.EnqueueSend(msg, e.Node);
    	}
    	
    }
}
