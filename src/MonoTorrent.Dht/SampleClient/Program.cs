using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht;
using MonoTorrent.Dht.Listeners;

namespace SampleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            UdpListener listener = new UdpListener(15000);
            DhtEngine engine = new DhtEngine(listener);
            // blah
            // blah
            // blah
        }
    }
}
