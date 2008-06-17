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
    public class SocketListener : ConnectionListenerBase
    {
        private AsyncCallback endAcceptCallback;
        private IPEndPoint listenEndPoint;
        private Socket listener;

        public override int ListenPort
        {
            get { return listenEndPoint.Port; }
        }
        public SocketListener(IPEndPoint endpoint)
        {
            if (endpoint == null)
                throw new ArgumentNullException("endpoint");

            this.listenEndPoint = endpoint;
            this.endAcceptCallback = new AsyncCallback(EndAccept);
        }


        public override void ChangePort(int port)
        {
            bool listening = State == ListenerStatus.Listening;

            Stop();
            listenEndPoint.Port = port;

            if (listening)
                Start();
        }

        public override void Start()
        {
            if (State == ListenerStatus.Listening)
                return;

            try
            {
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listener.Bind(listenEndPoint);
                listener.Listen(6);
                listener.BeginAccept(endAcceptCallback, this.listener);
                RaiseStateChanged(ListenerStatus.Listening);
            }
            catch (SocketException)
            {
                RaiseStateChanged(ListenerStatus.PortNotFree);
            }
        }

        public override void Stop()
        {
            if (State == ListenerStatus.Listening)
                RaiseStateChanged(ListenerStatus.NotListening);

            if (listener != null)
                listener.Close();
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
                    if (State == ListenerStatus.Listening)
                        listener.BeginAccept(endAcceptCallback, null);
                }
                catch(ObjectDisposedException)
                {
                }
            }
        }
    }
}