using System;
using System.Net;
using System.Threading.Tasks;
using MonoTorrent.BEncoding;
using MonoTorrent.Common;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht
{
    class TestListener : IDhtListener
    {
        public event Action<Message, IPEndPoint> MessageSent;
        public event MessageReceived MessageReceived;
        public event EventHandler<EventArgs> StatusChanged;

        public IPEndPoint Endpoint { get; private set; } = new IPEndPoint(IPAddress.Loopback, 0);
        public ListenerStatus Status { get; private set; }

        public void RaiseMessageReceived(Message message, IPEndPoint endpoint)
            => MessageReceived?.Invoke (message.Encode (), endpoint);

        public void ChangeEndpoint (IPEndPoint endpoint)
            => Endpoint = endpoint;

        public Task SendAsync(byte[] buffer, IPEndPoint endpoint)
        {
            Message message;
            MessageFactory.TryDecodeMessage (BEncodedValue.Decode<BEncodedDictionary> (buffer), out message);
            MessageSent?.Invoke (message, endpoint);
            return Task.CompletedTask;
        }

        public void Start ()
            => SetStatus (ListenerStatus.Listening);

        public void Stop ()
            => SetStatus (ListenerStatus.NotListening);

        void SetStatus (ListenerStatus status)
        {
            Status = status;
            StatusChanged?.Invoke (this, EventArgs.Empty);
        }
    }
}
