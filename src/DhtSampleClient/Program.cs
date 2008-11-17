using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht;
using MonoTorrent.Dht.Listeners;
using System.Net;
using System.IO;
using MonoTorrent.Common;

namespace SampleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            UdpListener listener = new UdpListener(new IPEndPoint(IPAddress.Parse("192.168.0.6"), 15000));
            DhtEngine engine = new DhtEngine(listener);

            byte[] nodes = null;
            if (File.Exists("mynodes"))
                nodes = File.ReadAllBytes("mynodes");

            listener.Start();
            engine.Start(nodes);            
            
            Random random = new Random(5);
            byte[] b = new byte[20];
            lock (random)
                random.NextBytes(b);

            for (int i = 0; i < 30; i++)
            {
                Console.WriteLine("Waiting: {0} seconds left", (30 - i));
                System.Threading.Thread.Sleep(1000);
            }

            // Get some peers for the torrent
            engine.GetPeers(b);

            engine.PeersFound += delegate(object o, PeersFoundEventArgs e) {
                Console.WriteLine("I FOUND PEERS: {0}", e.Peers.Count);
            };

            for (int i = 0; i < 30; i++)
            {
                Console.WriteLine("Waiting: {0} seconds left", (30 - i));
                System.Threading.Thread.Sleep(1000);
            }

            // Add ourself to the DHT so people know we have the torrent too
            //engine.Announce(t.infoHash, 25000);

            while (Console.ReadLine() != "q")
            {
                random.NextBytes(b);
                engine.GetPeers(b);
            }
            File.WriteAllBytes("mynodes", engine.SaveNodes());
        }
    }
}
