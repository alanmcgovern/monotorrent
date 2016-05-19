#if !DISABLE_DHT
using System.Net;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht
{
    internal class TestListener : DhtListener
    {
        public TestListener()
            : base(new IPEndPoint(IPAddress.Loopback, 0))
        {
        }

        public bool Started { get; private set; }

        public override void Send(byte[] buffer, IPEndPoint endpoint)
        {
            // Do nothing
        }

        public void RaiseMessageReceived(Message message, IPEndPoint endpoint)
        {
            DhtEngine.MainLoop.Queue(delegate { OnMessageReceived(message.Encode(), endpoint); });
        }

        public override void Start()
        {
            Started = true;
        }

        public override void Stop()
        {
            Started = false;
        }
    }
}

#endif