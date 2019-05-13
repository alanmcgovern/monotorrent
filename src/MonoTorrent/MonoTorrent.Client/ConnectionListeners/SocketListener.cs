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
    public sealed class SocketListener : PeerListener
    {
        Socket listener;
        SocketAsyncEventArgs connectArgs;

        public SocketListener(IPEndPoint endpoint)
            : base(endpoint)
        {
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

                connectArgs = new SocketAsyncEventArgs ();
                connectArgs.Completed += OnSocketReceived;

                if (!listener.AcceptAsync(connectArgs))
                    OnSocketReceived (listener, connectArgs);
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

            listener?.Close ();
            connectArgs = null;
            listener = null;
        }
        
        void OnSocketReceived (object sender, SocketAsyncEventArgs e)
        {
            Socket socket = null;
            try {
                if (e.SocketError != SocketError.Success)
                    throw new SocketException ((int)e.SocketError);
                socket = e.AcceptSocket;
                // This is a crazy quirk of the API. We need to null this
                // out if we re-use the args.
                e.AcceptSocket = null;

                IConnection connection;
                if (socket.AddressFamily == AddressFamily.InterNetwork)
                    connection = new IPV4Connection(socket, true);
                else
                    connection = new IPV6Connection(socket, true);

                var peer = new Peer("", connection.Uri, EncryptionTypes.All);
                RaiseConnectionReceived(peer, connection, null);
            } catch {
                socket?.Close();
            }

            try {
                if (!((Socket)sender).AcceptAsync(e))
                    OnSocketReceived (sender, e);
            } catch {
                return;
            }
        }
    }
}