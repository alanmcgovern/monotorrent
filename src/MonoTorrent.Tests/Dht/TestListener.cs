using System;
using System.Net;

using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht
{
    internal class TestListener : DhtListener
    {
        private bool started;

        public event Action<Message, IPEndPoint> MessageSent;

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
            Message message;
            MessageFactory.TryDecodeMessage (BEncodedValue.Decode<BEncodedDictionary> (buffer), out message);
            MessageSent?.Invoke (message, endpoint);
        }

        public void RaiseMessageReceived(Message message, IPEndPoint endpoint)
        {
            OnMessageReceived(message.Encode(), endpoint);
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
