using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    class AnnounceTask : Task
    {
        private int activeAnnounces;
        private NodeId infoHash;
        private DhtEngine engine;
        private int port;

        public AnnounceTask(DhtEngine engine, NodeId infoHash, int port)
        {
            this.engine = engine;
            this.infoHash = infoHash;
            this.port = port;
        }

        public override void Execute()
        {
            GetPeersTask task = new GetPeersTask(engine, infoHash);
            task.Completed += GotPeers;
            task.Execute();
        }

        private void GotPeers(object o, TaskCompleteEventArgs e)
        {
            e.Task.Completed -= GotPeers;
            GetPeersTask getpeers = (GetPeersTask)e.Task;
            foreach (Node n in getpeers.ClosestActiveNodes.Values)
            {
                AnnouncePeer query = new AnnouncePeer(engine.LocalId, infoHash, port, n.Token);
                SendQueryTask task = new SendQueryTask(engine, query, n);
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
