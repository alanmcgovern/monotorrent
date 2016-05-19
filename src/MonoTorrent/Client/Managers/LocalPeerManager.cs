using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MonoTorrent.Client
{
    internal class LocalPeerManager : IDisposable
    {
        private const int port = 6771;
        private readonly IPEndPoint ep;

        private readonly UdpClient socket;

        public LocalPeerManager()
        {
            socket = new UdpClient();
            ep = new IPEndPoint(IPAddress.Broadcast, port);
        }

        public void Dispose()
        {
            socket.Close();
        }

        public void Broadcast(TorrentManager manager)
        {
            if (manager.HasMetadata && manager.Torrent.IsPrivate)
                return;

            var message =
                string.Format(
                    "BT-SEARCH * HTTP/1.1\r\nHost: 239.192.152.143:6771\r\nPort: {0}\r\nInfohash: {1}\r\n\r\n\r\n",
                    manager.Engine.Settings.ListenPort, manager.InfoHash.ToHex());
            var data = Encoding.ASCII.GetBytes(message);
            try
            {
                socket.Send(data, data.Length, ep);
            }
            catch
            {
                // If data can't be sent, just ignore the error
            }
        }
    }
}