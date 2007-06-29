using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace MonoTorrent.Tests.Encryption.Tests
{
    public class SocketPipe
    {
        static Random random = null;
        public Socket ServerSocket;
        public Socket ClientSocket;

        public SocketPipe()
        {
            if (random == null)
                random = new Random();

            int port = random.Next(1000, 32000);

            TcpListener serverListener = new TcpListener(System.Net.IPAddress.Any, port);
            serverListener.Start();
            IAsyncResult serverResult = serverListener.BeginAcceptSocket(null, null);

            TcpClient tcpClient = new TcpClient();
            IAsyncResult clientResult = tcpClient.BeginConnect(System.Net.IPAddress.Loopback, port, null, null);


            ServerSocket = serverListener.EndAcceptSocket(serverResult);
            tcpClient.EndConnect(clientResult);

            ClientSocket = tcpClient.Client;
        }

    }
}
