#if !DISABLE_DHT
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    internal class AnnounceTask : Task
    {
        private int activeAnnounces;
        private readonly DhtEngine engine;
        private readonly NodeId infoHash;
        private readonly int port;

        public AnnounceTask(DhtEngine engine, InfoHash infoHash, int port)
            : this(engine, new NodeId(infoHash), port)
        {
        }

        public AnnounceTask(DhtEngine engine, NodeId infoHash, int port)
        {
            this.engine = engine;
            this.infoHash = infoHash;
            this.port = port;
        }

        public override void Execute()
        {
            var task = new GetPeersTask(engine, infoHash);
            task.Completed += GotPeers;
            task.Execute();
        }

        private void GotPeers(object o, TaskCompleteEventArgs e)
        {
            e.Task.Completed -= GotPeers;
            var getpeers = (GetPeersTask) e.Task;
            foreach (var n in getpeers.ClosestActiveNodes.Values)
            {
                if (n.Token == null)
                    continue;
                var query = new AnnouncePeer(engine.LocalId, infoHash, port, n.Token);
                var task = new SendQueryTask(engine, query, n);
                task.Completed += SentAnnounce;
                task.Execute();
                activeAnnounces++;
            }

            if (activeAnnounces == 0)
                RaiseComplete(new TaskCompleteEventArgs(this));
        }

        private void SentAnnounce(object o, TaskCompleteEventArgs e)
        {
            e.Task.Completed -= SentAnnounce;
            activeAnnounces--;

            if (activeAnnounces == 0)
                RaiseComplete(new TaskCompleteEventArgs(this));
        }
    }
}

#endif