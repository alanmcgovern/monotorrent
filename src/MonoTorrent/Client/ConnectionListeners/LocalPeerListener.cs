using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class LocalPeerListener : Listener
    {
        private const int MulticastPort = 6771;
        private static readonly IPAddress multicastIpAddress = IPAddress.Parse("239.192.152.143");

        private readonly ClientEngine engine;
        private UdpClient udpClient;

        public LocalPeerListener(ClientEngine engine)
            : base(new IPEndPoint(IPAddress.Any, 6771))
        {
            this.engine = engine;
        }

        public override void Start()
        {
            if (Status == ListenerStatus.Listening)
                return;
            try
            {
                udpClient = new UdpClient(MulticastPort);
                udpClient.JoinMulticastGroup(multicastIpAddress);
                udpClient.BeginReceive(OnReceiveCallBack, udpClient);
                RaiseStatusChanged(ListenerStatus.Listening);
            }
            catch
            {
                RaiseStatusChanged(ListenerStatus.PortNotFree);
            }
        }

        public override void Stop()
        {
            if (Status == ListenerStatus.NotListening)
                return;

            RaiseStatusChanged(ListenerStatus.NotListening);
            var c = udpClient;
            udpClient = null;
            if (c != null)
                c.Close();
        }

        private void OnReceiveCallBack(IAsyncResult ar)
        {
            var u = (UdpClient) ar.AsyncState;
            var e = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                var receiveBytes = u.EndReceive(ar, ref e);
                var receiveString = Encoding.ASCII.GetString(receiveBytes);

                var exp =
                    new Regex(
                        "BT-SEARCH \\* HTTP/1.1\\r\\nHost: 239.192.152.143:6771\\r\\nPort: (?<port>[^@]+)\\r\\nInfohash: (?<hash>[^@]+)\\r\\n\\r\\n\\r\\n");
                var match = exp.Match(receiveString);

                if (!match.Success)
                    return;

                var portcheck = Convert.ToInt32(match.Groups["port"].Value);
                if (portcheck < 0 || portcheck > 65535)
                    return;

                TorrentManager manager = null;
                var matchHash = InfoHash.FromHex(match.Groups["hash"].Value);
                for (var i = 0; manager == null && i < engine.Torrents.Count; i ++)
                    if (engine.Torrents[i].InfoHash == matchHash)
                        manager = engine.Torrents[i];

                if (manager == null)
                    return;

                var uri = new Uri("tcp://" + e.Address + ':' + match.Groups["port"].Value);
                var peer = new Peer("", uri, EncryptionTypes.All);

                // Add new peer to matched Torrent
                if (!manager.HasMetadata || !manager.Torrent.IsPrivate)
                {
                    ClientEngine.MainLoop.Queue(delegate
                    {
                        var count = manager.AddPeersCore(peer);
                        manager.RaisePeersFound(new LocalPeersAdded(manager, count, 1));
                    });
                }
            }
            catch
            {
                // Failed to receive data, ignore
            }
            finally
            {
                try
                {
                    u.BeginReceive(OnReceiveCallBack, ar.AsyncState);
                }
                catch
                {
                    // It's closed
                }
            }
        }
    }
}