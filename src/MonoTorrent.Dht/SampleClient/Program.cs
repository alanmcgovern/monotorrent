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

            listener.Start();
            engine.Start();            
            
            Random random = new Random();
            byte[] b = new byte[20];
            lock (random)
                random.NextBytes(b);
            System.Threading.Thread.Sleep(10000);
            engine.Announce(b);
            
            while (Console.ReadLine() != "q")
            {}
            File.WriteAllBytes("mynodes", engine.SaveNodes());
        }
    }
}
