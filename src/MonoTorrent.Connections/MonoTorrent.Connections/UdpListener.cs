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
    public class UdpListener : SocketListener, ISocketMessageListener
    {
        public event Action<ReadOnlyMemory<byte>, CompactEndPoint>? MessageReceived;

        Socket? Client { get; set; }
        SocketAddress? receiveAddress;
        readonly SemaphoreSlim sendLocker = new SemaphoreSlim (1, 1);

        public int? ReceiveBufferSize { get; set; }

        public int? SendBufferSize { get; set; }

        public UdpListener (IPEndPoint endpoint)
            : base (endpoint)
        {
        }

        public ReusableTask SendAsync (ReadOnlyMemory<byte> buffer, CompactEndPoint endpoint)
            => SendAsync (buffer, endpoint, dontFragment: false);

        internal async ReusableTask SendAsync (ReadOnlyMemory<byte> buffer, CompactEndPoint endpoint, bool dontFragment)
        {
            await sendLocker.WaitAsync ().ConfigureAwait (false);
            try {
                var client = Client;
                if (Status == ListenerStatus.PortNotFree)
                    throw new InvalidOperationException ($"The listener could not bind to ${LocalEndPoint}. Choose a new listening endpoint.");
                if (Status == ListenerStatus.NotListening || client == null)
                    throw new InvalidOperationException ("You must invoke StartAsync before sending or receiving a message with this listener.");

                var sendAddress = new SocketAddress (PreferredLocalEndPoint.AddressFamily);
                if (!endpoint.TryWriteBytes (sendAddress))
                    throw new InvalidOperationException ("Couldn't write compact endpoint to socketaddress");

                if (!dontFragment) {
                    await client.SendToAsync (buffer, SocketFlags.None, sendAddress).ConfigureAwait (false);
                    return;
                }

                bool restoreDontFragment = false;
                try {
                    if (!client.DontFragment) {
                        client.DontFragment = true;
                        restoreDontFragment = true;
                    }
                    await client.SendToAsync (buffer, SocketFlags.None, sendAddress).ConfigureAwait (false);
                } finally {
                    if (restoreDontFragment) {
                        try {
                            client.DontFragment = false;
                        } catch (ObjectDisposedException) {
                        }
                    }
                }
            } finally {
                sendLocker.Release ();
            }
        }

        protected override void Start (CancellationToken token)
        {
            base.Start (token);

            receiveAddress = new SocketAddress (PreferredLocalEndPoint.AddressFamily);
            var socket = new Socket (
                PreferredLocalEndPoint.AddressFamily,
                SocketType.Dgram,
                ProtocolType.Udp);

            if (ReceiveBufferSize.HasValue)
                socket.ReceiveBufferSize = ReceiveBufferSize.Value;
            if (SendBufferSize.HasValue)
                socket.SendBufferSize = SendBufferSize.Value;

            // Suppress Windows ICMP port-unreachable errors terminating the receive loop.
            if (OperatingSystem.IsWindows ()) {
                const int SIO_UDP_CONNRESET = unchecked((int) 0x9800000C);
                socket.IOControl (SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }

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
            Memory<byte> buffer = new byte[65_536];
            while (!token.IsCancellationRequested && receiveAddress is not null) {
                try {
                    var bytesReceived = await client.ReceiveFromAsync (
                        buffer,
                        SocketFlags.None,
                        receiveAddress).ConfigureAwait (false);

                    if (bytesReceived == 0)
                        continue;

                    var msg = buffer.Slice (0, bytesReceived).ToArray ();
                    var endPoint = new CompactEndPoint (receiveAddress);
                    if (!token.IsCancellationRequested)
                        RaiseMessageReceived (msg, endPoint);
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

        internal void ProcessDatagram (byte[] buffer, CompactEndPoint endpoint)
            => RaiseMessageReceived (buffer.ToArray (), endpoint);

        void RaiseMessageReceived (ReadOnlyMemory<byte> buffer, CompactEndPoint endpoint)
            => MessageReceived?.Invoke (buffer, endpoint);
    }
}
