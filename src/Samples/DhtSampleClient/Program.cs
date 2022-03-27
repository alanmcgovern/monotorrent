using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;

namespace ClientSample
{
    class Program
    {
        static async Task Main (string[] args)
        {
            // Create a DHT engine, and register a listener on port 15000
            var engine = new DhtEngine ();
            var listener = new DhtListener (new IPEndPoint (IPAddress.Any, 15000));
            await engine.SetListenerAsync (listener);

            // Load up the node cache from the prior invocation (if there is any)
            var nodes = ReadOnlyMemory<byte>.Empty;
            if (File.Exists ("mynodes"))
                nodes = File.ReadAllBytes ("mynodes");

            // Bootstrap into the DHT engine.
            await engine.StartAsync (nodes);

            // Begin querying for random 20 byte infohashes
            Random random = new Random (5);
            byte[] b = new byte[20];
            lock (random)
                random.NextBytes (b);

            // Kick off the firs search. Discovered peers will be returned via the 'PeersFound'
            // event in batches, as they're discovered.
            engine.GetPeers (new InfoHash (b));

            engine.PeersFound += async delegate (object o, PeersFoundEventArgs e) {
                Console.WriteLine ("Found peers: {0}", e.Peers.Count);
                while (Console.ReadLine () != "q") {
                    for (int i = 0; i < 30; i++) {
                        Console.WriteLine ("Waiting: {0} seconds left", (30 - i));
                        System.Threading.Thread.Sleep (1000);
                    }
                    // Get some peers for the torrent
                    engine.GetPeers (new InfoHash (b));
                    random.NextBytes (b);
                }
                File.WriteAllBytes ("mynodes", (await engine.SaveNodesAsync ()).ToArray ());
            };
        }
    }
}
