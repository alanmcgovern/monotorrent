using MonoTorrent.Dht.Messages;
using System;
using System.Net;

namespace MonoTorrent.Dht.Tasks
{
    internal class InitialiseTask : Task
    {
        int QuestionedNodeCount;
        DhtEngine engine;
        bool ok;
            
        public InitialiseTask(DhtEngine engine)
        {
            this.engine = engine;
        }
        
        public override void Execute ()
        {
            if (Active)
                return;

            QuestionedNodeCount = 0;
            ok = false;
            engine.RoutingTable.NodeAdded += NodeAdded;
            Node utorrent = new Node(NodeId.Create(), new System.Net.IPEndPoint(Dns.GetHostEntry("router.bittorrent.com").AddressList[0], 6881));
            Node node2 = new Node(NodeId.Create(), new IPEndPoint(Dns.GetHostEntry("router.utorrent.com").AddressList[0], 6881));
            engine.Add(utorrent);
            engine.Add(node2);
            //issue here id this 2 node never answer we will never raise complete!
            //but it seems logical because we will never init until we get a node to bootstrap!
        }
        
        void TaskComplete(object sender, TaskCompleteEventArgs e)
        {
            //we need to have at least one node who answer new nodes...
            SendQueryEventArgs args = (SendQueryEventArgs)e;
            if (!args.TimedOut)
                ok = true;
            
            QuestionedNodeCount--;
            //node added is call before this because handle is done before message sent
            //so if we are back to 0, this mean that we have no node that we wait answer 
            if (ok && (QuestionedNodeCount == 0))
            {
                RaiseComplete(e);
            }
        }
        
        private void NodeAdded(object o, NodeAddedEventArgs e)
        {
            QuestionedNodeCount++;
            // If we reached our target amount of nodes or we've run out of time, complete the task
            // Otherwise keep firing off FindNode requests to find nodes close to our own.
             SendQueryTask task = new SendQueryTask(engine, new FindNode(engine.LocalId, engine.LocalId), e.Node);
             task.Completed += TaskComplete;
             task.Execute();            
          }

        protected override void RaiseComplete(TaskCompleteEventArgs e)
        {
            if (Active)
                engine.RoutingTable.NodeAdded -= NodeAdded;
            base.RaiseComplete(e);
        }
    }
}