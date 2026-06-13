using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.BEncoding;
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

            while (engine.State != DhtState.Ready) {
                if (engine.State == DhtState.Initialising)
                    Console.WriteLine ("The engine is initialising...");
                else
                    Console.WriteLine ("Initialisation failed...");
                await Task.Delay (1000);
            }

            DumpStats (engine);
            static async void DumpStats (DhtEngine engine)
            {
                while (true) {
                    Console.WriteLine ("Nodes: " + engine.NodeCount);
                    await Task.Delay (1500);
                }
            }

            var infoHashes = new[] { InfoHash.FromHex ("d160b8d8ea35a5b4e52837468fc8f03d55cef1f7") };

            var tasks = new List<Task> ();
            var s = new SemaphoreSlim (50, 50);
            foreach (var hash in infoHashes) {
                if (!s.Wait (0)) {
                    var done = await Task.WhenAny (tasks);
                    tasks.Remove (done);
                    s.Release ();
                    Console.WriteLine ("Done one");
                    await done;
                }

                tasks.Add (engine.GetPeersAsync (hash).AsTask ());
                Console.WriteLine ("Starting one");
            }


            Console.WriteLine ("all done");
            Console.ReadLine ();
        }
    }
}
