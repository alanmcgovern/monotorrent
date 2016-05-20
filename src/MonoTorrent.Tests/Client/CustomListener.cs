using System;
using System.Net;
using MonoTorrent.Client;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Tests.Client
{
    public class CustomListener : PeerListener
    {
        public CustomListener()
            : base(new IPEndPoint(IPAddress.Any, 0))
        {
        }

        public override void Start()
        {
        }

        public override void Stop()
        {
        }

        public void Add(TorrentManager manager, IConnection connection)
        {
            var p = new Peer("", new Uri("tcp://12.123.123.1:2342"),
                EncryptionTypes.All);
            RaiseConnectionReceived(p, connection, manager);
        }
    }
}