using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht;
using MonoTorrent.Dht.Listeners;
using System.Net;
using System.IO;

namespace SampleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            UdpListener listener = new UdpListener(15000);
            DhtEngine engine = new DhtEngine(listener);

            if (File.Exists("mynodes"))
                engine.LoadNodes(File.ReadAllBytes("mynodes"));

            Console.CancelKeyPress += delegate { File.WriteAllBytes("mynodes", engine.SaveNodes()); };

            engine.Start();
            listener.Start();
            
            Random random = new Random();
            byte[] b = new byte[20];
            lock (random)
                random.NextBytes(b);
            System.Threading.Thread.Sleep(10000);
            engine.Announce(b);
            
            while (true)
                System.Threading.Thread.Sleep(100);
        }
    }
}
