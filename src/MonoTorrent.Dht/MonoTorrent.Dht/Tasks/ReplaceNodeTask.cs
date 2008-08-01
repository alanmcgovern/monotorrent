using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    class ReplaceNodeTask : Task
    {
        private Bucket bucket;
        private DhtEngine engine;
        private Node newNode;
        private SendQueryTask task;

        public ReplaceNodeTask(DhtEngine engine, Bucket bucket, Node newNode)
        {
            this.engine = engine;
            this.bucket = bucket;
            this.newNode = newNode;
        }

        public override void Execute()
        {
            this.engine = engine;
            if (bucket.Nodes.Count == 0)
            {
                RaiseComplete(new TaskCompleteEventArgs(this));
                return;
            }
            SendPingToOldest();
        }

        private void SendPingToOldest()
        {
            bucket.LastChanged = DateTime.UtcNow;
            bucket.SortBySeen();

            Node oldest = bucket.Nodes[0];
            oldest.FailedCount++;
            task = new SendQueryTask(engine, new Ping(engine.RoutingTable.LocalNode.Id), oldest);
            task.Completed += new EventHandler<TaskCompleteEventArgs>(TaskComplete);
            task.Execute();
        }

        void TaskComplete(object sender, TaskCompleteEventArgs e)
        {
            // I should raise the event with some eventargs saying which node was dead
            SendQueryEventArgs args = (SendQueryEventArgs)e;
            task.Completed -= new EventHandler<TaskCompleteEventArgs>(TaskComplete);
            if (args.TimedOut)
            {
                int index = bucket.Nodes.IndexOf(task.Target);
                if(index < 0)
                {
                    SendPingToOldest();
                }
                else
                {
                bucket.Nodes[bucket.Nodes.IndexOf(((SendQueryTask)e.Task).Target)] = newNode;
                RaiseComplete(new TaskCompleteEventArgs(this));
                }
            }
            else
            {
                SendPingToOldest();
            }
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
