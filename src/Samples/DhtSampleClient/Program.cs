using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;
using MonoTorrent.Messages;

namespace ClientSample
{
    class Program
    {
        static void Main (string[] args)
        {
            MainAsync ().Wait ();
        }

        static async Task MainAsync ()
        {
            // Create a DHT engine, and register a listener on port 15000
            var engine = new DhtEngine ();
            var listener = new DhtListener (new IPEndPoint (IPAddress.Any, 15000));
            await engine.SetListenerAsync (listener);

            // Load up the node cache from the prior invocation (if there is any)
            var nodes = ReadOnlyMemory<byte>.Empty;
            if (File.Exists ("mynodes")) {
                nodes = File.ReadAllBytes ("mynodes");
            }

            // Listen to some events
            engine.PeersFound += (o, e) => {
                Console.WriteLine ("Found peers: {0}", e.Peers.Count);
            };

            // Whenever the table has been initialised, store the node data on-disk.
            // This makes rejoining the DHT table in future significantly easier and faster.
            engine.StateChanged += async (o, e) => {
                Console.WriteLine ("Current state: {0}", engine.State);

                if (engine.State == DhtState.Ready)
                    File.WriteAllBytes ("mynodes", (await engine.SaveNodesAsync ()).ToArray ());
            };

            // Bootstrap into the DHT engine.
            // If a custom router is available, you can pass it in addition to, or instead of, the list
            // of nodes. e.g.
            //      await engine.StartAsync ("router.yourproject.com", "other_router.backup.yourproject.org");
            //      await engine.StartAsync (nodes, "router.yourproject.com", "other_router.backup.yourproject.org");
            //
            await engine.StartAsync (nodes);

            // Begin querying for a ubuntu torrent
            var infoHash = InfoHash.FromBase32 ("FKSPLJ7CBHSUWMUAHVBWOCLRYTEMVKQF");

            // Kick off the firs search. Discovered peers will be returned via the 'PeersFound'
            // event in batches, as they're discovered.
            engine.GetPeers (infoHash);

            Console.Write ("Press enter to fetch some peers. press 'q' and enter to quit");
            while (Console.ReadLine () != "q") {
                Console.WriteLine ("Getting some peers...");

                // Get some peers for the torrent
                engine.GetPeers (infoHash);
                for (int i = 0; i < 5; i++) {
                    Console.WriteLine ("Waiting: {0} seconds left", (30 - i));
                    System.Threading.Thread.Sleep (1000);
                }
                Console.Write ("Press enter to fetch some peers. press 'q' and enter to quit");
            }
        }
    }
}
