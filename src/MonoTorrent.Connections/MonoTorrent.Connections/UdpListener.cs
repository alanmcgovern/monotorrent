//
// UdpListener.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
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
using System.Threading.Tasks;

namespace MonoTorrent.Connections
{
    public abstract class UdpListener : SocketListener, ISocketMessageListener
    {
        public event Action<ReadOnlyMemory<byte>, IPEndPoint>? MessageReceived;

        UdpClient? Client { get; set; }

        protected UdpListener (IPEndPoint endpoint)
            : base (endpoint)
        {
        }

        public async Task SendAsync (ReadOnlyMemory<byte> buffer, IPEndPoint endpoint)
        {
            if (Status == ListenerStatus.PortNotFree)
                throw new InvalidOperationException ($"The listener could not bind to ${LocalEndPoint}. Choose a new listening endpoint.");
            if (Status == ListenerStatus.NotListening || Client == null)
                throw new InvalidOperationException ("You must invoke StartAsync before sending or receiving a message with this listener.");
            await Client.SendAsync (buffer, buffer.Length, endpoint).ConfigureAwait (false);
        }

        protected override void Start (CancellationToken token)
        {
            base.Start (token);

            UdpClient client = Client = new UdpClient (PreferredLocalEndPoint);
            LocalEndPoint = (IPEndPoint?) client.Client.LocalEndPoint;
            token.Register (() => {
                client.Dispose ();
                Client = null;
            });

            ReceiveAsync (client, token);
        }

        async void ReceiveAsync (UdpClient client, CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                try {
#if NET6_0
                    UdpReceiveResult result = await client.ReceiveAsync (token).ConfigureAwait (false);
#else
                    UdpReceiveResult result = await client.ReceiveAsync ().ConfigureAwait (false);
#endif
                    if (!token.IsCancellationRequested)
                        MessageReceived?.Invoke (result.Buffer, result.RemoteEndPoint);
                } catch (SocketException ex) {
                    // If the destination computer closes the connection
                    // we get error code 10054. We need to keep receiving on
                    // the socket until we clear all the error states
                    if (ex.ErrorCode == 10054)
                        continue;
                } catch {
                    // Do nothing.
                }
            }
        }
    }
}
