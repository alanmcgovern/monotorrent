using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Dht.Tasks
{
    class RefreshBucketTask : Task
    {
        private Bucket bucket;

        public RefreshBucketTask(Bucket bucket)
        {
            this.bucket = bucket;
        }

        public override void Execute(DhtEngine engine)
        {
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
}
