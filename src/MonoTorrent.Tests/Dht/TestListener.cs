#if !DISABLE_DHT
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Dht.Messages;
using System.Net;

namespace MonoTorrent.Dht
{
    internal class TestListener : DhtListener
    {
        private bool started;

        public TestListener()
            : base(new IPEndPoint(IPAddress.Loopback, 0))
        {

        }

        public bool Started
        {
            get { return started; }
        }

        public override void Send(byte[] buffer, IPEndPoint endpoint)
        {
            // Do nothing
        }

        public void RaiseMessageReceived(Message message, IPEndPoint endpoint)
        {
            DhtEngine.MainLoop.Queue(delegate
            {
                OnMessageReceived(message.Encode(), endpoint);
            });
        }

        public override void Start()
        {
            started = true;
        }

        public override void Stop()
        {
            started = false;
        }
    }
}
#endif