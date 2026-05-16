//
// SocketConnection.cs
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
using System.Runtime.InteropServices;
using System.Threading;

using ReusableTasks;

namespace MonoTorrent.Connections.Peer
{
    public sealed class SocketPeerConnection : IPeerConnection
    {
        public ReadOnlyMemory<byte> AddressBytes { get; }

        public bool CanReconnect => !IsIncoming;

        CancellationTokenSource Cancellation { get; }

        ISocketConnector? Connector { get; }

        public bool Disposed { get; private set; }

        public IPEndPoint EndPoint { get; }

        public bool IsIncoming { get; }

        Socket? Socket { get; set; }

        public Uri Uri { get; }

        #region Constructors

        public SocketPeerConnection (Socket socket, bool isIncoming)
            : this (null, null, socket, isIncoming)
        {

        }

        public SocketPeerConnection (Uri uri, ISocketConnector connector)
            : this (uri, connector, null, false)
        {

        }

        SocketPeerConnection (Uri? uri, ISocketConnector? connector, Socket? socket, bool isIncoming)
        {
            if (uri == null) {
                var endpoint = (IPEndPoint) socket!.RemoteEndPoint!;
                uri = socket.AddressFamily switch {
                    AddressFamily.InterNetwork => new Uri ($"ipv4://{endpoint}"),
                    AddressFamily.InterNetworkV6 => new Uri ($"ipv6://{endpoint}"),
                    _ => throw new NotSupportedException ($"AddressFamily.{socket.AddressFamily} is unsupported")
                };
            }

            Cancellation = new CancellationTokenSource ();
            Connector = connector;
            EndPoint = new IPEndPoint (IPAddress.Parse (uri.Host), uri.Port);
            AddressBytes = EndPoint.Address.GetAddressBytes ();
            IsIncoming = isIncoming;
            Socket = socket;
            Uri = uri;
        }

        #endregion


        #region Async Methods

        public async ReusableTask<bool> ConnectAsync ()
        {
            if (Connector == null)
                throw new InvalidOperationException ("This connection represents an incoming connection");

            try {
                Socket = await Connector.ConnectAsync (Uri, Cancellation.Token).ConfigureAwait (false);
                if (Disposed) {
                    Socket.Dispose ();
                    return false;
                }
            } catch {
                return false;
            }
            return true;
        }

        public async ReusableTask<int> ReceiveAsync (Memory<byte> buffer)
            => await (Socket?.ReceiveAsync (buffer).ConfigureAwait (false) ?? throw new InvalidOperationException ("The underlying socket is not connected"));

        public async ReusableTask<int> SendAsync (ReadOnlyMemory<byte> buffer)
            => await (Socket?.SendAsync (buffer).ConfigureAwait (false) ?? throw new InvalidOperationException ("The underlying socket is not connected"));

        public void Dispose ()
        {
            Disposed = true;
            Cancellation.Cancel ();
            Socket?.Dispose ();
        }
    }

#endregion
}
