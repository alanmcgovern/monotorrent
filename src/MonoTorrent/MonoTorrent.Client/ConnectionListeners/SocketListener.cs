 //
// ConnectionListener.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//



using System;
using System.Net;
using System.Net.Sockets;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Common;
using MonoTorrent.Client.Connections;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Accepts incoming connections and passes them off to the right TorrentManager
    /// </summary>
    public class SocketListener : PeerListener
    {
        private AsyncCallback endAcceptCallback;
        private Socket listener;

        public SocketListener(IPEndPoint endpoint)
            : base(endpoint)
        {
            this.endAcceptCallback = EndAccept;
        }

        private void EndAccept(IAsyncResult result)
        {
            Socket peerSocket = null;
            try
            {
                Socket listener = (Socket)result.AsyncState;
                peerSocket = listener.EndAccept(result);

                IPEndPoint endpoint = (IPEndPoint)peerSocket.RemoteEndPoint;
                Uri uri = new Uri("tcp://" + endpoint.Address.ToString() + ':' + endpoint.Port);
                Peer peer = new Peer("", uri, EncryptionTypes.All);
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