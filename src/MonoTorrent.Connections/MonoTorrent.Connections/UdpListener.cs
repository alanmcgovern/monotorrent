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
using System.Buffers.Binary;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using ReusableTasks;

namespace MonoTorrent.Connections
{
    public abstract class UdpListener : SocketListener, ISocketMessageListener
    {
        public event Action<ReadOnlyMemory<byte>, CompactEndPoint>? MessageReceived;

        Socket? Client { get; set; }
        SocketAddress? sendAddress;
        SocketAddress? receiveAddress;

        protected UdpListener (IPEndPoint endpoint)
            : base (endpoint)
        {
        }

        public async ReusableTask SendAsync (ReadOnlyMemory<byte> buffer, CompactEndPoint endpoint)
        {
            if (Status == ListenerStatus.PortNotFree)
                throw new InvalidOperationException ($"The listener could not bind to ${LocalEndPoint}. Choose a new listening endpoint.");
            if (Status == ListenerStatus.NotListening || Client == null)
                throw new InvalidOperationException ("You must invoke StartAsync before sending or receiving a message with this listener.");

            if (!endpoint.TryWriteBytes (sendAddress!))
                throw new InvalidOperationException ("Couldn't write compact endpoint to socketaddress");
            await Client.SendToAsync (buffer, SocketFlags.None, sendAddress!).ConfigureAwait (false);
        }

        protected override void Start (CancellationToken token)
        {
            base.Start (token);

            sendAddress = new SocketAddress (PreferredLocalEndPoint.AddressFamily);
            receiveAddress = new SocketAddress (PreferredLocalEndPoint.AddressFamily);
            var socket = new Socket (
                PreferredLocalEndPoint.AddressFamily,
                SocketType.Dgram,
                ProtocolType.Udp);

            socket.Bind (PreferredLocalEndPoint);

            Client = socket;
            LocalEndPoint = (IPEndPoint?) socket.LocalEndPoint;
            token.Register (() => {
                Client.Dispose ();
                Client = null;
            });

            ReceiveAsync (Client, token);
        }

        async void ReceiveAsync (Socket client, CancellationToken token)
        {
            Memory<byte> buffer = new byte[4096];
            while (!token.IsCancellationRequested) {
                try {
                    var bytesReceived = await client.ReceiveFromAsync (
                        buffer,
                        SocketFlags.None,
                        receiveAddress!).ConfigureAwait (false);

                    var msg = buffer.Slice (0, bytesReceived).ToArray ();
                    var endPoint = new CompactEndPoint (receiveAddress!);
                    if (!token.IsCancellationRequested)
                        MessageReceived?.Invoke (msg, endPoint);
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
