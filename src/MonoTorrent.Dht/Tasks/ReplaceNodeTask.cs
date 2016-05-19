#if !DISABLE_DHT
using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Client;

namespace MonoTorrent.Dht.Tasks
{
    class ReplaceNodeTask : Task
    {
        private Bucket bucket;
        private DhtEngine engine;
        private Node newNode;

        public ReplaceNodeTask(DhtEngine engine, Bucket bucket, Node newNode)
        {
            this.engine = engine;
            this.bucket = bucket;
            this.newNode = newNode;
        }

        public override void Execute()
        {
            DhtEngine.MainLoop.Queue((MainLoopTask) delegate
            {
                if (bucket.Nodes.Count == 0)
                {
                    RaiseComplete(new TaskCompleteEventArgs(this));
                    return;
                }

                SendPingToOldest();
            });
        }

        private void SendPingToOldest()
        {
            bucket.LastChanged = DateTime.UtcNow;
            bucket.SortBySeen();

            if ((DateTime.UtcNow - bucket.Nodes[0].LastSeen) < TimeSpan.FromMinutes(3))
            {
                RaiseComplete(new TaskCompleteEventArgs(this));
            }
            else
            {
                Node oldest = bucket.Nodes[0];
                SendQueryTask task = new SendQueryTask(engine, new Ping(engine.LocalId), oldest);
                task.Completed += TaskComplete;
                task.Execute();
            }
        }

        void TaskComplete(object sender, TaskCompleteEventArgs e)
        {
            e.Task.Completed -= TaskComplete;

            // I should raise the event with some eventargs saying which node was dead
            SendQueryEventArgs args = (SendQueryEventArgs) e;

            if (args.TimedOut)
            {
                // If the node didn't respond and it's no longer in our bucket,
                // we need to send a ping to the oldest node in the bucket
                // Otherwise if we have a non-responder and it's still there, replace it!
                int index = bucket.Nodes.IndexOf(((SendQueryTask) e.Task).Target);
                if (index < 0)
                {
                    SendPingToOldest();
                }
                else
                {
                    bucket.Nodes[index] = newNode;
                    RaiseComplete(new TaskCompleteEventArgs(this));
                }
            }
            else
            {
                SendPingToOldest();
            }
        }
    }
}

#endif