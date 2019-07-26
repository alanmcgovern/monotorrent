using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Client;

namespace MonoTorrent.Dht.Tasks
{
    class ReplaceNodeTask
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

        public async System.Threading.Tasks.Task Execute ()
        {
            if (bucket.Nodes.Count == 0)
                return;

            bucket.LastChanged = DateTime.UtcNow;
            bucket.SortBySeen();

            if (bucket.Nodes[0].LastSeen < TimeSpan.FromMinutes(3))
            {
                return;
            }
            else
            {
                Node oldest = bucket.Nodes[0];
                var args = await engine.SendQueryAsync (new Ping(engine.LocalId), oldest);

                if (args.TimedOut) {
                    // If the node didn't respond and it's no longer in our bucket,
                    // we need to send a ping to the oldest node in the bucket
                    // Otherwise if we have a non-responder and it's still there, replace it!
                    int index = bucket.Nodes.IndexOf (oldest);
                    if (index < 0) {
                        await Execute ();
                    } else {
                        bucket.Nodes [index] = newNode;
                        return;
                    }
                } else {
                    await Execute ();
                }
            }
        }
        
    }
}
