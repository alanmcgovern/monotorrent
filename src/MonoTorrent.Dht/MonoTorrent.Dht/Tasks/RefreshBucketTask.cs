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

            Console.WriteLine("Choosing first from: {0}", bucket.Nodes.Count);
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
    }
}