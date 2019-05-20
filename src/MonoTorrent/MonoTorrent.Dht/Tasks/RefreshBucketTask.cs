using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Dht.Tasks;

namespace MonoTorrent.Dht
{
    class RefreshBucketTask
    {
        private Bucket bucket;
        private DhtEngine engine;

        public RefreshBucketTask(DhtEngine engine, Bucket bucket)
        {
            this.engine = engine;
            this.bucket = bucket;
        }

        public async void Execute ()
        {
            if (bucket.Nodes.Count == 0)
                return;

            bucket.SortBySeen();

            foreach (var node in bucket.Nodes.ToArray ()) {
                var message = new FindNode (engine.LocalId, node.Id);

                var args = await engine.SendQueryAsync (message, node);
                if (!args.TimedOut)
                    return;
            }
        }
    }
}
