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
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client;

namespace MonoTorrent
{
    abstract class UdpListener : SocketListener
    {
        CancellationTokenSource Cancellation { get; set; }
        UdpClient Client { get; set; }

        protected UdpListener(IPEndPoint endpoint)
            :base(endpoint)
        {
            Cancellation = new CancellationTokenSource ();
        }

		protected abstract void OnMessageReceived(byte[] buffer, IPEndPoint endpoint);

        public virtual async Task SendAsync (byte[] buffer, IPEndPoint endpoint)
        {
            try
            {
               if (endpoint.Address != IPAddress.Any)
                    await Client.SendAsync (buffer, buffer.Length, endpoint);
            }
            catch(Exception ex)
            {
                Logger.Log (null, "UdpListener could not send message: {0}", ex);
            }
        }

        public override void Start()
        {
            if (Status == ListenerStatus.Listening)
                return;

            Cancellation?.Cancel ();
            Cancellation = new CancellationTokenSource ();
            try {
                var client = Client = new UdpClient(EndPoint);
                Cancellation.Token.Register (() => {
                    client.SafeDispose ();
                    Client = null;
                });

                ReceiveAsync (client, Cancellation.Token);
                RaiseStatusChanged(ListenerStatus.Listening);
            }
            catch (SocketException)
            {
                Cancellation?.Cancel ();
                Cancellation = null;

                RaiseStatusChanged(ListenerStatus.PortNotFree);
            }
        }

        public override void Stop()
        {
            Cancellation?.Cancel ();
            Cancellation = null;
        }

        async void ReceiveAsync (UdpClient client, CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                try {
                    var result = await client.ReceiveAsync ().ConfigureAwait (false);
                    OnMessageReceived(result.Buffer, result.RemoteEndPoint);
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
