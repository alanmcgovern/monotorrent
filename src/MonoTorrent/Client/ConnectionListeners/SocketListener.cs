using System;
using System.Net;
using System.Net.Sockets;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    ///     Accepts incoming connections and passes them off to the right TorrentManager
    /// </summary>
    public class SocketListener : PeerListener
    {
        private readonly AsyncCallback endAcceptCallback;
        private Socket listener;

        public SocketListener(IPEndPoint endpoint)
            : base(endpoint)
        {
            endAcceptCallback = EndAccept;
        }

        private void EndAccept(IAsyncResult result)
        {
            Socket peerSocket = null;
            try
            {
                var listener = (Socket) result.AsyncState;
                peerSocket = listener.EndAccept(result);

                var endpoint = (IPEndPoint) peerSocket.RemoteEndPoint;
                var uri = new Uri("tcp://" + endpoint.Address + ':' + endpoint.Port);
                var peer = new Peer("", uri, EncryptionTypes.All);
                IConnection connection = null;
                if (peerSocket.AddressFamily == AddressFamily.InterNetwork)
                    connection = new IPV4Connection(peerSocket, true);
                else
                    connection = new IPV6Connection(peerSocket, true);


                RaiseConnectionReceived(peer, connection, null);
            }
            catch (SocketException)
            {
                // Just dump the connection
                if (peerSocket != null)
                    peerSocket.Close();
            }
            catch (ObjectDisposedException)
            {
                // We've stopped listening
            }
            finally
            {
                try
                {
                    if (Status == ListenerStatus.Listening)
                        listener.BeginAccept(endAcceptCallback, listener);
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        public override void Start()
        {
            if (Status == ListenerStatus.Listening)
                return;

            try
            {
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listener.Bind(Endpoint);
                listener.Listen(6);
                listener.BeginAccept(endAcceptCallback, listener);
                RaiseStatusChanged(ListenerStatus.Listening);
            }
            catch (SocketException)
            {
                RaiseStatusChanged(ListenerStatus.PortNotFree);
            }
        }

        public override void Stop()
        {
            RaiseStatusChanged(ListenerStatus.NotListening);

            if (listener != null)
                listener.Close();
        }
    }
}