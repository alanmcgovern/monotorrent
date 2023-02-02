//
// PeerListener.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Threading;

namespace MonoTorrent.Connections.Peer
{
    /// <summary>
    /// Accepts incoming connections and passes them off to the right TorrentManager
    /// </summary>
    public sealed class PeerConnectionListener : SocketListener, IPeerConnectionListener
    {
        public event EventHandler<PeerConnectionEventArgs>? ConnectionReceived;

        public PeerConnectionListener (IPEndPoint endpoint)
            : base (endpoint)
        {
        }

        protected override void Start (CancellationToken token)
        {
            base.Start (token);

            var listener = new Socket (PreferredLocalEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            var connectArgs = new SocketAsyncEventArgs ();
            token.Register (() => {
                listener.Close ();
                connectArgs.Dispose ();
            });

            listener.Bind (PreferredLocalEndPoint);
            LocalEndPoint = (IPEndPoint?) listener.LocalEndPoint;

            listener.Listen (6);

            connectArgs.Completed += OnSocketReceived;

            if (!listener.AcceptAsync (connectArgs))
                OnSocketReceived (listener, connectArgs);
        }

        void OnSocketReceived (object? sender, SocketAsyncEventArgs e)
        {
            Socket? socket = null;
            try {
                socket = e.AcceptSocket;
                // Capture the socket (if any) and prepare the args for reuse
                // by ensuring AcceptSocket is null.
                e.AcceptSocket = null;

                if (e.SocketError != SocketError.Success)
                    throw new SocketException ((int) e.SocketError);

                var connection = new SocketPeerConnection (socket!, true);
                ConnectionReceived?.Invoke (this, new PeerConnectionEventArgs (connection, null));
            } catch {
                socket?.Close ();
            }

            try {
                if (!((Socket) sender!).AcceptAsync (e))
                    OnSocketReceived (sender, e);
            } catch {
                return;
            }
        }
    }
}
