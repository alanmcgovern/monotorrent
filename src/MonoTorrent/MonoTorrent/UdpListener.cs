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

namespace MonoTorrent
{
    abstract class UdpListener : SocketListener, ISocketMessageListener
    {
        public event Action<byte[], IPEndPoint> MessageReceived;

        UdpClient Client { get; set; }

        protected UdpListener (IPEndPoint endpoint)
            : base (endpoint)
        {
        }

        public async Task SendAsync (byte[] buffer, IPEndPoint endpoint)
        {
            try {
                if (endpoint.Address != IPAddress.Any)
                    await Client.SendAsync (buffer, buffer.Length, endpoint).ConfigureAwait (false);
            } catch (Exception ex) {
                Logger.Log (null, "UdpListener could not send message: {0}", ex);
            }
        }

        protected override void Start (CancellationToken token)
        {
            UdpClient client = Client = new UdpClient (OriginalEndPoint);
            EndPoint = (IPEndPoint) client.Client.LocalEndPoint;
            token.Register (() => {
                client.SafeDispose ();
                Client = null;
            });

            ReceiveAsync (client, token);
        }

        async void ReceiveAsync (UdpClient client, CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                try {
                    UdpReceiveResult result = await client.ReceiveAsync ().ConfigureAwait (false);
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
