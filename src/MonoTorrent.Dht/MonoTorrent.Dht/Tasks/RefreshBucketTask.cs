using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Dht.Tasks;

namespace MonoTorrent.Dht
{
    class RefreshBucketTask : Task
    {
        private Bucket bucket;
        private DhtEngine engine;
        private FindNode message;
        private Node node;
        private SendQueryTask task;

        public RefreshBucketTask(DhtEngine engine, Bucket bucket)
        {
            this.engine = engine;
            this.bucket = bucket;
        }

        public override void Execute()
        {
            if (bucket.Nodes.Count == 0)
            {
                RaiseComplete(new TaskCompleteEventArgs(this));
                return;
            }

            bucket.SortBySeen();
            QueryNode(bucket.Nodes[0]);
        }

        private void TaskComplete(object o, TaskCompleteEventArgs e)
        {
            task.Completed -= TaskComplete;

            SendQueryEventArgs args = (SendQueryEventArgs)e;
            if (args.TimedOut)
            {
                bucket.SortBySeen();
                int index = bucket.Nodes.IndexOf(node);
                if (index == -1 || (++index < bucket.Nodes.Count))
                {
                    QueryNode(bucket.Nodes[0]);
                }
                else
                {
                    RaiseComplete(new TaskCompleteEventArgs(this));
                }
            }
            else
            {
                RaiseComplete(new TaskCompleteEventArgs(this));
            }
        }

        private void QueryNode(Node node)
        {
            this.node = node;
            message = new FindNode(engine.LocalId, node.Id);
            task = new SendQueryTask(engine, message, node);
            task.Completed += TaskComplete;
            task.Execute();
        }

        // 1) Choose the oldest peer and ping them
        // 2) Wait for the message to succeed or fail
        // 3) If it fails, we can discard that node and add the new node.
        //    If it succeeds, we choose the new 'oldest peer' and keep going
        // 4) If we ping all the nodes and they're all alive, add the new node to the replacement cache
        //    unless it's already full.


        // The code below was cut/pasted from another function, it's probably not relevent to this.
        //Node node = engine.RoutingTable.FindNode(Id);
        //if (node != null)
        //{
        //    node.CurrentlyPinging = false;
        //    if (node.Bucket != null)
        //    {
        //        node.Bucket.LastChanged = DateTime.Now;
        //        if (node.Bucket.Replacement != null)
        //            node.Bucket.PingForReplace(engine);
        //    }

        //}
    }

    /* More copied and pasted code - this came from the Bucket class */
    /* This stuff should all be handled from within here             */

    //public void Replace(Node oldNode)
    //{
    //    nodes.Remove(oldNode);
    //    nodes.Add(replacement);
    //    lastChanged = DateTime.Now;
    //    this.replacement = null;
    //}

    //internal void PingForReplace(DhtEngine engine)
    //{
    //    Nodes.Sort();//max to min last seen
    //    foreach (Node n in Nodes)
    //    {
    //        if (!n.CurrentlyPinging && (n.State == NodeState.Unknown || n.State == NodeState.Questionable))
    //        {
    //            n.CurrentlyPinging = true;
    //            engine.MessageLoop.EnqueueSend(new Messages.Ping(engine.RoutingTable.LocalNode.Id), n);
    //            return;//ping only the first questionnable of bucket
    //        }
    //    }
    //}
}