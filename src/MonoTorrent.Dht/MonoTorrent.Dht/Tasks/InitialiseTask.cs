using MonoTorrent.Dht.Messages;
using System;

namespace MonoTorrent.Dht
{
    internal class InitialiseTask : Task<TaskCompleteEventArgs>
    {
        DhtEngine engine;
        private int bootstrapCount;
        public InitialiseTask(DhtEngine engine)
        {
            this.engine = engine;
            bootstrapCount = 0;
        }
        
        public void MessageReceivedOrTimedout(object sender, EventArgs e)
        {
            bootstrapCount--;
            if (bootstrapCount == 0 || !Active)
                Complete(new TaskCompleteEventArgs(true));
        }
        
        public override void Execute ()
        {
            if (!Active)
                return;
            
            engine.NodeFound += FirstNodeAdded;
        }
        void FirstNodeAdded(object sender, NodeFoundEventArgs e)
        {
            engine.NodeFound -= FirstNodeAdded;
            NodeAdded(sender, e);
        }
        
        void NodeAdded(object sender, NodeFoundEventArgs e)
        {
            bootstrapCount++;
            FindNode msg = new FindNode(engine.RoutingTable.LocalNode.Id, engine.RoutingTable.LocalNode.Id);
            msg.QueryTimedOut += MessageReceivedOrTimedout;
            msg.ResponseReceived += MessageReceivedOrTimedout;
            msg.NodeFound += NodeAdded;
            engine.MessageLoop.EnqueueSend(msg, e.Node);
        }
    }
}