#if !DISABLE_DHT
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    class AnnounceTask
    {
        readonly NodeId infoHash;
        readonly DhtEngine engine;
        readonly int port;

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

        public async Task Execute()
        {
            GetPeersTask getpeers = new GetPeersTask(engine, infoHash);
            var nodes = await getpeers.Execute();

            foreach (Node n in nodes)
            {
                if (n.Token == null)
                    continue;
                AnnouncePeer query = new AnnouncePeer(engine.LocalId, infoHash, port, n.Token);
                await engine.SendQueryAsync (query, n);
            }
        }
    }
}
#endif